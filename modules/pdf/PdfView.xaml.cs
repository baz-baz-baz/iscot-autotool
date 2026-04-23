using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Modules.Pdf.Models;

namespace PersonalAutomationTool.Modules.Pdf
{
    public partial class PdfView : UserControl
    {
        public ObservableCollection<TrainCardModel> TrainCards { get; set; } = [];

        public PdfView()
        {
            InitializeComponent();
            ItemsControlCards.ItemsSource = TrainCards;
            LoadFolders();

            PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged += RefreshData;
            this.Unloaded += (s, e) => PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged -= RefreshData;
        }

        private void RefreshData()
        {
            // Debounce load or just reload
            LoadFolders();
        }

        private async void LoadFolders()
        {
            string logDumpFolder = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
            if (!Directory.Exists(logDumpFolder)) return;

            // Leggi il file system in background
            var newCards = await System.Threading.Tasks.Task.Run(() =>
            {
                var cardsList = new System.Collections.Generic.List<TrainCardModel>();
                string[] parentDirectories = Directory.GetDirectories(logDumpFolder);
                foreach (string parentDir in parentDirectories)
                {
                    var card = new TrainCardModel
                    {
                        Title = Path.GetFileName(parentDir),
                        FullPath = parentDir,
                        IsND = false
                    };

                    // Add SubDirectories
                    string[] subDirs = Directory.GetDirectories(parentDir);
                    foreach (string sub in subDirs)
                    {
                        card.Children.Add(new FolderItemModel
                        {
                            Name = Path.GetFileName(sub),
                            FullPath = sub,
                            IsDirectory = true
                        });
                    }

                    // Add Files
                    string[] files = Directory.GetFiles(parentDir);
                    foreach (string file in files)
                    {
                        card.Children.Add(new FolderItemModel
                        {
                            Name = Path.GetFileName(file),
                            FullPath = file,
                            IsDirectory = false,
                            Extension = Path.GetExtension(file).ToLower()
                        });
                    }

                    cardsList.Add(card);
                }
                return cardsList;
            });

            // Aggiorna l'interfaccia sul thread UI
            TrainCards.Clear();
            foreach (var card in newCards)
            {
                TrainCards.Add(card);
            }
        }

        private async void BtnRinomina_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is TrainCardModel card)
            {
                var pdfFiles = card.Children.Where(c => !c.IsDirectory && c.Extension == ".pdf").ToList();
                if (pdfFiles.Count == 0)
                {
                    MessageBox.Show("È richiesto almeno 1 file PDF nella cartella per questa operazione.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var uncheckedFiles = pdfFiles.Where(p => !p.IsNC).ToList();
                var checkedFiles = pdfFiles.Where(p => p.IsNC).ToList();

                if (uncheckedFiles.Count > 2)
                {
                    MessageBox.Show("Sono permessi al massimo 2 file PDF non spuntati (normali) per questa operazione.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var logFolders = card.Children.Where(c => c.IsDirectory && c.Name.Contains(" LOG ")).ToList();
                if (logFolders.Count == 0)
                {
                    MessageBox.Show("Nessuna cartella LOG trovata per estrarre le informazioni.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var tipi = await GetTipiFromDbAsync();
                var parsedInfos = new System.Collections.Generic.List<ParsedFolderInfo>();
                foreach (var logDir in logFolders)
                {
                    var info = ParseLogFolderName(logDir.Name, tipi);
                    if (info != null) parsedInfos.Add(info);
                }

                if (parsedInfos.Count == 0)
                {
                    MessageBox.Show("Impossibile analizzare i nomi delle cartelle LOG.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var first = parsedInfos.First();
                string newName = "";
                string prefix = card.IsND ? "ND FL" : "FL";

                if (parsedInfos.Count == 1)
                {
                    newName = $"{prefix} SR{first.Ticket} {first.Tipo} {first.Loco} IMC AV Milano {first.Data} {first.Utente}.pdf";
                }
                else
                {
                    var second = parsedInfos[1];
                    string locoPart = first.Loco == second.Loco ? first.Loco : $"{first.Loco} - {second.Loco}";
                    newName = $"{prefix} SR{first.Ticket} - SR{second.Ticket} {first.Tipo} {locoPart} IMC AV Milano {first.Data} {first.Utente}.pdf";
                }

                try
                {
                    var moveOperations = new System.Collections.Generic.List<(string OldPath, string NewPath)>();

                    if (uncheckedFiles.Count == 1)
                    {
                        moveOperations.Add((uncheckedFiles[0].FullPath, Path.Combine(card.FullPath, newName)));
                    }
                    else if (uncheckedFiles.Count == 2)
                    {
                        int pages1 = GetPdfPageCount(uncheckedFiles[0].FullPath);
                        int pages2 = GetPdfPageCount(uncheckedFiles[1].FullPath);

                        var smallerPdf = pages1 <= pages2 ? uncheckedFiles[0] : uncheckedFiles[1];
                        var largerPdf = pages1 <= pages2 ? uncheckedFiles[1] : uncheckedFiles[0];

                        string newNameNdL = "";

                        var txtFiles = card.Children.Where(c => !c.IsDirectory && c.Extension == ".txt").ToList();
                        if (txtFiles.Count > 0)
                        {
                            string txtNameBase = Path.GetFileNameWithoutExtension(txtFiles[0].Name);
                            string locoStr = parsedInfos.Count == 1 ? parsedInfos[0].Loco :
                                             (parsedInfos[0].Loco == parsedInfos[1].Loco ? parsedInfos[0].Loco : $"{parsedInfos[0].Loco} - {parsedInfos[1].Loco}");
                            newNameNdL = $"Checklist {txtNameBase} {parsedInfos[0].Tipo} {locoStr} IMC AV Milano {parsedInfos[0].Data} {parsedInfos[0].Utente}.pdf";
                        }
                        else
                        {
                            newNameNdL = newName.Replace("ND FL ", "NdL ").Replace("FL ", "NdL ");
                        }

                        moveOperations.Add((largerPdf.FullPath, Path.Combine(card.FullPath, newName)));
                        moveOperations.Add((smallerPdf.FullPath, Path.Combine(card.FullPath, newNameNdL)));
                    }

                    string baseNcName = newName.Replace("ND FL ", "NC ").Replace("FL ", "NC ");
                    for (int i = 0; i < checkedFiles.Count; i++)
                    {
                        string currentNcName = baseNcName;
                        if (i > 0)
                        {
                            currentNcName = System.Text.RegularExpressions.Regex.Replace(currentNcName, @"SR(\d+)", m =>
                            {
                                if (long.TryParse(m.Groups[1].Value, out long tic))
                                {
                                    return "SR" + (tic + i).ToString();
                                }
                                return m.Value;
                            });
                        }
                        moveOperations.Add((checkedFiles[i].FullPath, Path.Combine(card.FullPath, currentNcName)));
                    }

                    var dests = moveOperations.Select(m => m.NewPath).ToList();
                    if (dests.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dests.Count)
                    {
                        MessageBox.Show("Errore: la rinomina calcolata genererebbe file di destinazione duplicati.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    foreach (var op in moveOperations)
                    {
                        if (!string.Equals(op.OldPath, op.NewPath, StringComparison.OrdinalIgnoreCase) && File.Exists(op.NewPath))
                        {
                            bool isOneOfOriginals = moveOperations.Any(m => string.Equals(m.OldPath, op.NewPath, StringComparison.OrdinalIgnoreCase));
                            if (!isOneOfOriginals)
                            {
                                MessageBox.Show("Esiste già un altro file di destinazione nel percorso:\n" + Path.GetFileName(op.NewPath), "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }

                    var tempOps = new System.Collections.Generic.List<(string Old, string Temp, string New)>();
                    foreach (var op in moveOperations)
                    {
                        if (string.Equals(op.OldPath, op.NewPath, StringComparison.Ordinal)) continue;
                        string tempPath = op.NewPath + ".tmp" + Guid.NewGuid().ToString().Substring(0, 8);
                        tempOps.Add((op.OldPath, tempPath, op.NewPath));
                    }

                    if (tempOps.Count == 0)
                    {
                        MessageBox.Show("I file hanno già i nomi corretti.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    foreach (var op in tempOps) File.Move(op.Old, op.Temp);
                    foreach (var op in tempOps)
                    {
                        if (File.Exists(op.New)) File.Delete(op.New);
                        File.Move(op.Temp, op.New);
                    }

                    string successMsg = "File rinominati con successo:\n\n" + string.Join("\n", tempOps.Select(o => Path.GetFileName(o.New)));
                    MessageBox.Show(successMsg, "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                
                // L'aggiornamento UI avverrà automaticamente tramite AppWatcher
                catch (Exception ex)
                {
                    MessageBox.Show($"Errore durante la rinomina: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private static int GetPdfPageCount(string pdfPath)
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using var document = PdfSharp.Pdf.IO.PdfReader.Open(pdfPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                return document.PageCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura pagine PDF {pdfPath}: {ex.Message}");
                return int.MaxValue; // In caso di errore lo consideriamo più grande per non sovrascrivere erroneamente come piccolo
            }
        }

        private static async System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetTipiFromDbAsync()
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                var tipi = new System.Collections.Generic.List<string>();
                try
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    DirectoryInfo? dir = new(baseDir);
                    while (dir != null && dir.Name != "PersonalAutomationTool")
                    {
                        dir = dir.Parent;
                    }

                    if (dir != null)
                    {
                        string dbPath = Path.Combine(dir.FullName, "modules", "database", "train_software.db");
                        if (File.Exists(dbPath))
                        {
                            using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(dbPath);
                            var dt = db.ExecuteQuery("SELECT DISTINCT tipo FROM flotte ORDER BY LENGTH(tipo) DESC;");
                            foreach (System.Data.DataRow row in dt.Rows)
                            {
                                if (row["tipo"] != DBNull.Value)
                                    tipi.Add(row["tipo"].ToString()!);
                            }
                        }
                    }
                }
                catch { }
                return tipi;
            });
        }

        private class ParsedFolderInfo
        {
            public string Ticket { get; set; } = string.Empty;
            public string Tipo { get; set; } = string.Empty;
            public string Loco { get; set; } = string.Empty;
            public string Data { get; set; } = string.Empty;
            public string Utente { get; set; } = string.Empty;
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"^SR(?<ticket>\S+)\sLOG\s(?<rest>.*)$")]
        private static partial System.Text.RegularExpressions.Regex LogFolderRegex();

        [System.Text.RegularExpressions.GeneratedRegex(@"\s(?<data>\d{6})\s(?<utente>.*)$")]
        private static partial System.Text.RegularExpressions.Regex LogDateRegex();

        private static ParsedFolderInfo? ParseLogFolderName(string folderName, System.Collections.Generic.List<string> tipi)
        {
            var match = LogFolderRegex().Match(folderName);
            if (!match.Success) return null;

            string ticket = match.Groups["ticket"].Value;
            string rest = match.Groups["rest"].Value;

            var dateMatch = LogDateRegex().Match(rest);
            if (!dateMatch.Success) return null;

            string data = dateMatch.Groups["data"].Value;
            string utente = dateMatch.Groups["utente"].Value;
            string tipoLocoSoft = rest[..dateMatch.Index];

            string tipo = "";
            foreach (var t in tipi)
            {
                if (tipoLocoSoft.StartsWith(t + " "))
                {
                    tipo = t;
                    break;
                }
            }

            if (string.IsNullOrEmpty(tipo))
            {
                var parts = tipoLocoSoft.Split(' ');
                tipo = parts[0];
            }

            string remaining = tipoLocoSoft[tipo.Length..].Trim();
            string loco = remaining.Split(' ').FirstOrDefault() ?? "";

            return new ParsedFolderInfo
            {
                Ticket = ticket,
                Tipo = tipo,
                Loco = loco,
                Data = data,
                Utente = utente
            };
        }

        private void BtnApri_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is TrainCardModel card)
            {
                if (Directory.Exists(card.FullPath))
                {
                    Process.Start(new ProcessStartInfo()
                    {
                        FileName = card.FullPath,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                else
                {
                    MessageBox.Show("Cartella non trovata.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
