using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Core.Naming;
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

            this.Loaded += (s, e) =>
            {
                PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged -= RefreshData;
                PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged += RefreshData;
                LoadFolders();
            };

            this.Unloaded += (s, e) =>
            {
                PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged -= RefreshData;
            };
        }

        private void RefreshData()
        {
            Dispatcher.InvokeAsync(() =>
            {
                LoadFolders();
            });
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
                var parsedInfos = new System.Collections.Generic.List<LogDumpFolderName>();
                foreach (var logDir in logFolders)
                {
                    // Migrato al parser condiviso LogDumpFolderName (PersonalAutomationTool.Core.Naming):
                    // prima di questa migrazione, questa stessa grammatica di nomi veniva
                    // ridecodificata in modo indipendente in almeno altri sette punti del codice.
                    // Vedi PROJECT_MEMORY.md §6 per la lista dei chiamanti ancora da migrare.
                    if (LogDumpFolderName.TryParse(logDir.Name, tipi, out var info))
                    {
                        parsedInfos.Add(info!);
                    }
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
                            currentNcName = SrTicketRegex().Replace(currentNcName, m =>
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

                    foreach (var (oldPath, newPath) in moveOperations)
                    {
                        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase) && File.Exists(newPath))
                        {
                            bool isOneOfOriginals = moveOperations.Any(m => string.Equals(m.OldPath, newPath, StringComparison.OrdinalIgnoreCase));
                            if (!isOneOfOriginals)
                            {
                                MessageBox.Show("Esiste già un altro file di destinazione nel percorso:\n" + Path.GetFileName(newPath), "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                    }

                    var tempOps = new System.Collections.Generic.List<(string Old, string Temp, string New)>();
                    foreach (var (oldPath, newPath) in moveOperations)
                    {
                        if (string.Equals(oldPath, newPath, StringComparison.Ordinal)) continue;
                        string tempPath = string.Concat(newPath, ".tmp", Guid.NewGuid().ToString().AsSpan(0, 8));
                        tempOps.Add((oldPath, tempPath, newPath));
                    }

                    if (tempOps.Count == 0)
                    {
                        // MessageBox.Show("I file hanno già i nomi corretti.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    foreach (var (oldPath, tempPath, newPath) in tempOps) File.Move(oldPath, tempPath);
                    foreach (var (oldPath, tempPath, newPath) in tempOps)
                    {
                        if (File.Exists(newPath)) File.Delete(newPath);
                        File.Move(tempPath, newPath);
                    }

                    string successMsg = "File rinominati con successo:\n\n" + string.Join("\n", tempOps.Select(o => Path.GetFileName(o.New)));
                    // MessageBox.Show(successMsg, "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
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
                    string dbPath = Path.Combine(baseDir, "modules", "database", "train_software.db");
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
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error retrieving train types: {ex.Message}"); }
                return tipi;
            });
        }

        [System.Text.RegularExpressions.GeneratedRegex(@"SR(\d+)")]
        private static partial System.Text.RegularExpressions.Regex SrTicketRegex();

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
