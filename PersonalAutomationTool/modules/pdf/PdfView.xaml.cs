using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Core;
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
            if (sender is not Button btn || btn.CommandParameter is not TrainCardModel card)
            {
                return;
            }

            // La decisione (quali file spostare dove) vive in PdfRenamePlanner, completamente
            // disaccoppiato da WPF: qui restano solo il recupero dei dati necessari alla
            // pianificazione (tipi noti dal DB) e l'esecuzione effettiva del piano.
            try
            {
                var tipi = await GetTipiFromDbAsync();
                var plan = PdfRenamePlanner.CreatePlan(card, tipi, GetPdfPageCount);

                switch (plan.Outcome)
                {
                    case PdfRenameOutcome.Error:
                        var icon = plan.Severity == PdfRenameErrorSeverity.Warning ? MessageBoxImage.Warning : MessageBoxImage.Error;
                        var title = plan.Severity == PdfRenameErrorSeverity.Warning ? "Attenzione" : "Errore";
                        MessageBox.Show(plan.ErrorMessage ?? "Errore sconosciuto.", title, MessageBoxButton.OK, icon);
                        return;

                    case PdfRenameOutcome.NothingToDo:
                        // MessageBox.Show("I file hanno già i nomi corretti.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                }

                // Il dialog di anteprima (intervento 4.1, Sprint 3) è stato rimosso su richiesta
                // esplicita del committente: la rinomina parte subito alla pressione del pulsante,
                // senza passaggio di conferma intermedio. La difesa contro un parsing sbagliato resta
                // "Annulla ultima rinomina" (BtnAnnullaRinomina_Click), che si appoggia allo stesso
                // storico scritto qui sotto — vedi PROJECT_MEMORY.md §6.1-decies.
                var tempOps = new System.Collections.Generic.List<(string Old, string Temp, string New)>();
                foreach (var (oldPath, newPath) in plan.MoveOperations)
                {
                    string tempPath = string.Concat(newPath, ".tmp", Guid.NewGuid().ToString().AsSpan(0, 8));
                    tempOps.Add((oldPath, tempPath, newPath));
                }

                int totalSteps = tempOps.Count * 2;
                int step = 0;
                LoadingOverlay.IsBusy = true;
                try
                {
                    foreach (var (oldPath, tempPath, _) in tempOps)
                    {
                        File.Move(oldPath, tempPath);
                        LoadingOverlay.Report(++step, totalSteps, "Rinomina");
                    }
                    foreach (var (_, tempPath, newPath) in tempOps)
                    {
                        if (File.Exists(newPath)) File.Delete(newPath);
                        File.Move(tempPath, newPath);
                        LoadingOverlay.Report(++step, totalSteps, "Rinomina");
                    }
                }
                finally
                {
                    LoadingOverlay.IsBusy = false;
                }

                // Storico inverso per l'annulla (intervento 4.3, Sprint 3): registrato solo dopo che
                // entrambe le fasi sono andate a buon fine, con gli stessi percorsi finali risultanti
                // dal piano (non quelli temporanei, mai visibili all'esterno di questo metodo).
                RenamerLog.RecordBatch(RenameBatchKind.PdfRename, plan.MoveOperations);

                // string successMsg = "File rinominati con successo:\n\n" + string.Join("\n", tempOps.Select(o => Path.GetFileName(o.New)));
                // MessageBox.Show(successMsg, "Successo", MessageBoxButton.OK, MessageBoxImage.Information);

                // L'aggiornamento UI avverrà automaticamente tramite AppWatcher
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la rinomina: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnAnnullaRinomina_Click(object sender, RoutedEventArgs e)
        {
            var result = await System.Threading.Tasks.Task.Run(() => RenamerLog.UndoLastBatch(RenameBatchKind.PdfRename));

            if (!result.BatchFound)
            {
                MessageBox.Show("Nessuna rinomina PDF da annullare.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (result.Errors.Count > 0)
            {
                string msg = $"Ripristinati {result.Restored} file. Alcuni file non sono stati ripristinati:\n\n" + string.Join("\n", result.Errors);
                MessageBox.Show(msg, "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show($"Rinomina annullata: {result.Restored} file ripristinati al nome precedente.", "Fatto", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // L'aggiornamento UI avverrà automaticamente tramite AppWatcher
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
            // FlotteCache (core/FlotteCache.cs): stessa query (DISTINCT tipo, ordinata per
            // lunghezza decrescente — l'ordine da cui dipende LogDumpFolderName.TryParse per
            // distinguere "ETR1000 I-F" da "ETR1000"), ora servita dalla cache in memoria.
            return await System.Threading.Tasks.Task.Run(() => PersonalAutomationTool.Core.FlotteCache.GetDistinctTipiOrderByLengthDesc());
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
