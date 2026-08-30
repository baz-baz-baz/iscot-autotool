using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Cartelle
{
    public partial class CartelleView : UserControl
    {
        private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;

        public CartelleView()
        {
            InitializeComponent();
            _ = InitializeDatabaseAsync();

            _debounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _debounceTimer.Tick += DebounceTimer_Tick;

            this.Loaded += (s, e) =>
            {
                PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged -= RefreshData;
                PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged += RefreshData;
                UpdatePreviews();
            };

            this.Unloaded += (s, e) =>
            {
                PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged -= RefreshData;
            };
        }

        private void RefreshData()
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UpdatePreviews();
            });
        }

        private async System.Threading.Tasks.Task InitializeDatabaseAsync()
        {
            try
            {
                await LoadTipiTrenoAsync();
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Errore DB: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private async System.Threading.Tasks.Task LoadTipiTrenoAsync()
        {
            // FlotteCache (core/FlotteCache.cs): la tabella flotte viene letta una sola volta e
            // tenuta in memoria invece di aprire una connessione SQLite per ogni ricerca tipo/loco.
            // Il Task.Run resta per non bloccare la UI sul primo caricamento (I/O su disco reale);
            // le letture successive, già in cache, sono comunque immediate.
            var tipi = await System.Threading.Tasks.Task.Run(() => FlotteCache.GetDistinctTipiOrderByName());

            CmbTipo.Items.Clear();
            foreach (var tipo in tipi)
            {
                CmbTipo.Items.Add(tipo);
            }
        }

        private void CmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RestartDebounceTimer();
        }

        private void TxtLoco1_TextChanged(object sender, TextChangedEventArgs e)
        {
            RestartDebounceTimer();
        }

        private void Input_Changed(object sender, TextChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            RestartDebounceTimer();
        }

        private void RestartDebounceTimer()
        {
            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();
            UpdateSoftwareField();
            UpdatePreviews();
        }

        private void ChkModificaSoftware_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkModificaSoftware.IsChecked == false)
            {
                // Modifica manuale (sbloccata)
                if (TxtSoftware != null)
                {
                    TxtSoftware.IsReadOnly = false;
                    TxtSoftware.Background = System.Windows.Media.Brushes.White;
                }
            }
            else
            {
                // Auto da database (bloccata)
                if (TxtSoftware != null)
                {
                    TxtSoftware.IsReadOnly = true;
                    TxtSoftware.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F8F9FA"));
                }
                UpdateSoftwareField(); // Ricalcola il valore dal DB
            }
        }

        /// <summary>
        /// "Non creare LOG" e "Non creare DUMP" sono mutuamente esclusive: selezionarne una deve
        /// deselezionare automaticamente l'altra, così non è mai possibile escludere entrambe (il
        /// pulsante "Crea" finirebbe per non creare nulla).
        /// </summary>
        private void ChkNoLog_Checked(object sender, RoutedEventArgs e)
        {
            if (ChkNoDump != null && ChkNoDump.IsChecked == true) ChkNoDump.IsChecked = false;
            UpdatePreviews();
        }

        private void ChkNoDump_Checked(object sender, RoutedEventArgs e)
        {
            if (ChkNoLog != null && ChkNoLog.IsChecked == true) ChkNoLog.IsChecked = false;
            UpdatePreviews();
        }

        private void ChkEsclusione_Unchecked(object sender, RoutedEventArgs e) => UpdatePreviews();

        private async System.Threading.Tasks.Task<string> GetTrenoFromDbAsync(string tipo, string loco)
        {
            if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(loco)) return "";
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    return FlotteCache.FindTreno(tipo, loco)?.Trim() ?? "";
                }
                catch { }
                return "";
            });
        }

        private void UpdatePreviews()
        {
            if (TxtPreviewLog1 == null || TxtTicket1 == null) return; // Se l'interfaccia non è ancora inizializzata

            bool noLog = ChkNoLog?.IsChecked == true;
            bool noDump = ChkNoDump?.IsChecked == true;

            string ticket1 = TxtTicket1.Text?.Trim() ?? "";
            string ticket2 = TxtTicket2?.Text?.Trim() ?? "";
            string tipo = CmbTipo?.SelectedItem?.ToString() ?? "";
            string loco1 = TxtLoco1?.Text?.Trim() ?? "";
            string loco2 = TxtLoco2?.Text?.Trim() ?? "";
            string software = TxtSoftware?.Text?.Trim() ?? "";
            string data = DateTime.Now.ToString("ddMMyy");
            string utente = TxtUtente?.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(ticket1))
            {
                TxtPreviewLog1.Text = noLog ? string.Empty : $"SR{ticket1} LOG {tipo} {loco1} {software} {data} {utente}".Trim();
                TxtPreviewDump1.Text = noDump ? string.Empty : $"SR{ticket1} DUMP {tipo} {loco1} {software} {data} {utente}".Trim();
            }
            else
            {
                TxtPreviewLog1.Text = string.Empty;
                if (TxtPreviewDump1 != null) TxtPreviewDump1.Text = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(ticket2) && TxtPreviewLog2 != null && TxtPreviewDump2 != null)
            {
                string l2 = string.IsNullOrWhiteSpace(loco2) ? loco1 : loco2;

                TxtPreviewLog2.Text = noLog ? string.Empty : $"SR{ticket2} LOG {tipo} {l2} {software} {data} {utente}".Trim();
                TxtPreviewDump2.Text = noDump ? string.Empty : $"SR{ticket2} DUMP {tipo} {l2} {software} {data} {utente}".Trim();
                if (SectionPreviewLog2 != null) SectionPreviewLog2.Visibility = Visibility.Visible;
                if (SectionPreviewDump2 != null) SectionPreviewDump2.Visibility = Visibility.Visible;
            }
            else if (TxtPreviewLog2 != null && TxtPreviewDump2 != null)
            {
                TxtPreviewLog2.Text = string.Empty;
                TxtPreviewDump2.Text = string.Empty;
                if (SectionPreviewLog2 != null) SectionPreviewLog2.Visibility = Visibility.Collapsed;
                if (SectionPreviewDump2 != null) SectionPreviewDump2.Visibility = Visibility.Collapsed;
            }

            // Riscontro visivo immediato: la colonna esclusa si attenua, oltre a restare svuotata.
            if (PanelPreviewLogColumn != null) PanelPreviewLogColumn.Opacity = noLog ? 0.4 : 1.0;
            if (PanelPreviewDumpColumn != null) PanelPreviewDumpColumn.Opacity = noDump ? 0.4 : 1.0;
        }

        private void BtnPulisci_Click(object sender, RoutedEventArgs e)
        {
            TxtUtente.Text = string.Empty;
            TxtTicket1.Text = string.Empty;
            TxtTicket2.Text = string.Empty;
            TxtLoco1.Text = string.Empty;
            TxtLoco2.Text = string.Empty;
            TxtScadenzaFrancia.Text = string.Empty;
            ChkModificaSoftware.IsChecked = true;
            if (CmbTipo != null) CmbTipo.SelectedIndex = -1;
        }

        private async void UpdateSoftwareField()
        {
            if (ChkModificaSoftware != null && ChkModificaSoftware.IsChecked == false)
            {
                // Salta l'aggiornamento automatico se l'utente ha tolto la spunta di autocompletamento
                return;
            }

            if (CmbTipo == null || CmbTipo.SelectedItem == null || TxtLoco1 == null || string.IsNullOrWhiteSpace(TxtLoco1.Text) || TxtSoftware == null)
            {
                if (TxtSoftware != null) TxtSoftware.Text = string.Empty;
                return;
            }

            string selectedTipo = CmbTipo.SelectedItem.ToString() ?? "";
            string loco1 = TxtLoco1.Text.Trim();

            try
            {
                string softwareValue = await System.Threading.Tasks.Task.Run(() =>
                    FlotteCache.FindSoftware(selectedTipo, loco1) ?? "Non trovato");

                TxtSoftware.Text = softwareValue;
            }
            catch (Exception ex)
            {
                TxtSoftware.Text = "Errore";
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private async void BtnCrea_Click(object sender, RoutedEventArgs e)
        {
            string ticket1 = TxtTicket1.Text.Trim();
            string ticket2 = TxtTicket2.Text.Trim();
            string tipo = CmbTipo.SelectedItem?.ToString() ?? "";
            string loco1 = TxtLoco1.Text.Trim();
            string loco2 = TxtLoco2.Text.Trim();
            string software = TxtSoftware.Text.Trim();
            string data = DateTime.Now.ToString("ddMMyy");
            string utente = TxtUtente.Text.Trim();
            string scadenzaFrancia = TxtScadenzaFrancia.Text.Trim();

            if (string.IsNullOrWhiteSpace(ticket1))
            {
                MessageBox.Show("Inserisci almeno il Ticket 1 per creare le cartelle.", "Avviso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Le due checkbox sono mutuamente esclusive (ChkNoLog_Checked/ChkNoDump_Checked): non
            // possono risultare entrambe vere, quindi non c'è il caso "non si crea nulla".
            bool creaLog = ChkNoLog?.IsChecked != true;
            bool creaDump = ChkNoDump?.IsChecked != true;

            try
            {
                string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;

                // Crea cartella madre e sottocartelle per Ticket 1 / Loco 1
                string treno1 = await GetTrenoFromDbAsync(tipo, loco1);
                string parentName1 = !string.IsNullOrWhiteSpace(treno1) ? $"{tipo} {treno1}".Trim() : $"{tipo} {loco1}".Trim();

                string l2 = string.IsNullOrWhiteSpace(loco2) ? loco1 : loco2;
                string treno2 = string.IsNullOrWhiteSpace(ticket2) ? "" : await GetTrenoFromDbAsync(tipo, l2);
                string parentName2 = !string.IsNullOrWhiteSpace(treno2) ? $"{tipo} {treno2}".Trim() : $"{tipo} {l2}".Trim();

                await System.Threading.Tasks.Task.Run(() =>
                {
                    string parentFolder1 = Path.Combine(baseLogDump, parentName1);
                    Directory.CreateDirectory(parentFolder1);

                    string folderLog1 = Path.Combine(parentFolder1, $"SR{ticket1} LOG {tipo} {loco1} {software} {data} {utente}".Trim());
                    string folderDump1 = Path.Combine(parentFolder1, $"SR{ticket1} DUMP {tipo} {loco1} {software} {data} {utente}".Trim());
                    if (creaLog) Directory.CreateDirectory(folderLog1);
                    if (creaDump) Directory.CreateDirectory(folderDump1);

                    if (!string.IsNullOrWhiteSpace(scadenzaFrancia))
                    {
                        string txtFile1 = Path.Combine(parentFolder1, $"{scadenzaFrancia}.txt");
                        if (!File.Exists(txtFile1)) File.WriteAllText(txtFile1, "");
                    }

                    // Se esiste Ticket 2, crea cartelle anche per quello usando Loco 2 (o Loco 1 se Loco 2 è vuoto)
                    if (!string.IsNullOrWhiteSpace(ticket2))
                    {
                        string parentFolder2 = Path.Combine(baseLogDump, parentName2);
                        Directory.CreateDirectory(parentFolder2);

                        string folderLog2 = Path.Combine(parentFolder2, $"SR{ticket2} LOG {tipo} {l2} {software} {data} {utente}".Trim());
                        string folderDump2 = Path.Combine(parentFolder2, $"SR{ticket2} DUMP {tipo} {l2} {software} {data} {utente}".Trim());
                        if (creaLog) Directory.CreateDirectory(folderLog2);
                        if (creaDump) Directory.CreateDirectory(folderDump2);

                        if (!string.IsNullOrWhiteSpace(scadenzaFrancia))
                        {
                            string txtFile2 = Path.Combine(parentFolder2, $"{scadenzaFrancia}.txt");
                            if (!File.Exists(txtFile2)) File.WriteAllText(txtFile2, "");
                        }
                    }
                });

                // MessageBox.Show("Cartelle create con successo in LOG & DUMP!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la creazione delle cartelle: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Le esclusioni valgono solo per la singola creazione: si azzerano sempre, andata a
                // buon fine o no, così la prossima pressione di "Crea" riparte dal comportamento
                // standard (entrambe le cartelle) invece di ripetere un'esclusione dimenticata.
                if (ChkNoLog != null) ChkNoLog.IsChecked = false;
                if (ChkNoDump != null) ChkNoDump.IsChecked = false;
            }
        }
    }
}
