using System;
using System.IO;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using PersonalAutomationTool.Modules.Database;

namespace PersonalAutomationTool.Modules.Cartelle
{
    public partial class CartelleView : UserControl
    {
        private DatabaseManager? _dbManager;
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

            PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged += RefreshData;
            this.Unloaded += (s, e) => PersonalAutomationTool.Core.AppWatcher.OnLogDumpFolderChanged -= RefreshData;
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
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                DirectoryInfo? dir = new(baseDir);
                while (dir != null && dir.Name != "PersonalAutomationTool")
                {
                    dir = dir.Parent;
                }

                if (dir == null) return;

                string dbPath = Path.Combine(dir.FullName, "modules", "database", "train_software.db");

                if (File.Exists(dbPath))
                {
                    _dbManager = new DatabaseManager(dbPath);
                    await LoadTipiTrenoAsync();
                }
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Errore DB: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        }

        private async System.Threading.Tasks.Task LoadTipiTrenoAsync()
        {
            if (_dbManager == null) return;

            string query = "SELECT DISTINCT tipo FROM flotte ORDER BY tipo;";

            // Execute the physical SQL query synchronously on a background thread so the UI thread doesn't hang
            var dataTable = await System.Threading.Tasks.Task.Run(() => _dbManager.ExecuteQuery(query));

            // Load results back onto the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                CmbTipo.Items.Clear();
                foreach (DataRow row in dataTable.Rows)
                {
                    if (row["tipo"] != DBNull.Value)
                    {
                        CmbTipo.Items.Add(row["tipo"].ToString());
                    }
                }
            });
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

        private async System.Threading.Tasks.Task<string> GetTrenoFromDbAsync(string tipo, string loco)
        {
            if (_dbManager == null || string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(loco)) return "";
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string query = $"SELECT treno FROM flotte WHERE tipo = '{tipo}' AND loco = '{loco}' LIMIT 1;";
                    var dataTable = _dbManager.ExecuteQuery(query);
                    if (dataTable.Rows.Count > 0 && dataTable.Rows[0]["treno"] != DBNull.Value)
                    {
                        return dataTable.Rows[0]["treno"].ToString()?.Trim() ?? "";
                    }
                }
                catch { }
                return "";
            });
        }

        private async void UpdatePreviews()
        {
            if (TxtPreviewLog1 == null || TxtTicket1 == null) return; // Se l'interfaccia non è ancora inizializzata

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
                string treno1 = await GetTrenoFromDbAsync(tipo, loco1);
                string parentName1 = !string.IsNullOrWhiteSpace(treno1) ? $"{tipo} {treno1}".Trim() : $"{tipo} {loco1}".Trim();

                TxtPreviewLog1.Text = $@"{parentName1}\SR{ticket1} LOG {tipo} {loco1} {software} {data} {utente}".Trim();
                TxtPreviewDump1.Text = $@"{parentName1}\SR{ticket1} DUMP {tipo} {loco1} {software} {data} {utente}".Trim();
            }
            else
            {
                TxtPreviewLog1.Text = string.Empty;
                if (TxtPreviewDump1 != null) TxtPreviewDump1.Text = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(ticket2) && TxtPreviewLog2 != null && TxtPreviewDump2 != null)
            {
                string l2 = string.IsNullOrWhiteSpace(loco2) ? loco1 : loco2;
                string treno2 = await GetTrenoFromDbAsync(tipo, l2);
                string parentName2 = !string.IsNullOrWhiteSpace(treno2) ? $"{tipo} {treno2}".Trim() : $"{tipo} {l2}".Trim();

                TxtPreviewLog2.Text = $@"{parentName2}\SR{ticket2} LOG {tipo} {l2} {software} {data} {utente}".Trim();
                TxtPreviewDump2.Text = $@"{parentName2}\SR{ticket2} DUMP {tipo} {l2} {software} {data} {utente}".Trim();
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

            if (_dbManager == null || CmbTipo == null || CmbTipo.SelectedItem == null || TxtLoco1 == null || string.IsNullOrWhiteSpace(TxtLoco1.Text) || TxtSoftware == null)
            {
                if (TxtSoftware != null) TxtSoftware.Text = string.Empty;
                return;
            }

            string selectedTipo = CmbTipo.SelectedItem.ToString() ?? "";
            string loco1 = TxtLoco1.Text.Trim();

            try
            {
                string softwareValue = await System.Threading.Tasks.Task.Run(() =>
                {
                    // Cerchiamo il software incrociando "tipo" e "loco"
                    string query = $"SELECT software FROM flotte WHERE tipo = '{selectedTipo}' AND loco = '{loco1}' LIMIT 1;";
                    var dataTable = _dbManager.ExecuteQuery(query);

                    if (dataTable.Rows.Count > 0 && dataTable.Rows[0]["software"] != DBNull.Value)
                    {
                        return dataTable.Rows[0]["software"].ToString() ?? "";
                    }
                    return "Non trovato";
                });

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

            try
            {
                string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;

                // Crea cartella madre e sottocartelle per Ticket 1 / Loco 1
                string treno1 = await GetTrenoFromDbAsync(tipo, loco1);
                string parentName1 = !string.IsNullOrWhiteSpace(treno1) ? $"{tipo} {treno1}".Trim() : $"{tipo} {loco1}".Trim();

                string parentFolder1 = Path.Combine(baseLogDump, parentName1);
                Directory.CreateDirectory(parentFolder1);

                string folderLog1 = Path.Combine(parentFolder1, $"SR{ticket1} LOG {tipo} {loco1} {software} {data} {utente}".Trim());
                string folderDump1 = Path.Combine(parentFolder1, $"SR{ticket1} DUMP {tipo} {loco1} {software} {data} {utente}".Trim());
                Directory.CreateDirectory(folderLog1);
                Directory.CreateDirectory(folderDump1);

                if (!string.IsNullOrWhiteSpace(scadenzaFrancia))
                {
                    string txtFile1 = Path.Combine(parentFolder1, $"{scadenzaFrancia}.txt");
                    if (!File.Exists(txtFile1)) File.WriteAllText(txtFile1, "");
                }

                // Se esiste Ticket 2, crea cartelle anche per quello usando Loco 2 (o Loco 1 se Loco 2 è vuoto)
                if (!string.IsNullOrWhiteSpace(ticket2))
                {
                    string l2 = string.IsNullOrWhiteSpace(loco2) ? loco1 : loco2;
                    string treno2 = await GetTrenoFromDbAsync(tipo, l2);
                    string parentName2 = !string.IsNullOrWhiteSpace(treno2) ? $"{tipo} {treno2}".Trim() : $"{tipo} {l2}".Trim();

                    string parentFolder2 = Path.Combine(baseLogDump, parentName2);
                    Directory.CreateDirectory(parentFolder2);

                    string folderLog2 = Path.Combine(parentFolder2, $"SR{ticket2} LOG {tipo} {l2} {software} {data} {utente}".Trim());
                    string folderDump2 = Path.Combine(parentFolder2, $"SR{ticket2} DUMP {tipo} {l2} {software} {data} {utente}".Trim());
                    Directory.CreateDirectory(folderLog2);
                    Directory.CreateDirectory(folderDump2);

                    if (!string.IsNullOrWhiteSpace(scadenzaFrancia))
                    {
                        string txtFile2 = Path.Combine(parentFolder2, $"{scadenzaFrancia}.txt");
                        if (!File.Exists(txtFile2)) File.WriteAllText(txtFile2, "");
                    }
                }

                MessageBox.Show("Cartelle create con successo in LOG & DUMP!", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la creazione delle cartelle: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
