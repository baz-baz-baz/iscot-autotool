using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Database
{
    public partial class DatabaseView : UserControl
    {
        private DatabaseManager? _dbManager;
        private string? _dbPath;
        private string _dbDirectory = "";
        private bool _isInitializing = true;

        /// <summary>
        /// Nome della tabella attualmente legata a <see cref="MainDataGrid"/>.ItemsSource. Non è lo
        /// stesso di <c>CmbTables.SelectedItem</c>: quando un evento di selezione della ComboBox
        /// scatta, <c>SelectedItem</c> riflette già la NUOVA scelta dell'utente, mentre questo campo
        /// resta sulla tabella VECCHIA finché <see cref="LoadDataForTable"/> non la aggiorna — è
        /// quello che serve a <see cref="SalvaModifichePendenti"/> per sapere su quale tabella
        /// scrivere l'UPDATE prima che la griglia venga sostituita.
        /// </summary>
        private string? _tabellaCorrenteInGriglia;

        public DatabaseView()
        {
            InitializeComponent();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                _dbDirectory = PersonalAutomationTool.Core.AppPaths.DatabaseFolder;
                if (!Directory.Exists(_dbDirectory))
                {
                    Directory.CreateDirectory(_dbDirectory);
                }

                // Ensure the emails.db database exists with a table
                EnsureEmailsDatabase();

                LoadDatabases();
                _isInitializing = false;
                
                if (CmbDatabases.Items.Count > 0)
                {
                    CmbDatabases.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore nell'inizializzazione del database: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EnsureEmailsDatabase()
        {
            try
            {
                string emailsDbPath = Path.Combine(_dbDirectory, "emails.db");
                using var tempManager = new DatabaseManager(emailsDbPath);
                tempManager.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS indirizzi_email (id INTEGER PRIMARY KEY AUTOINCREMENT, nome TEXT, email TEXT, categoria TEXT)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Errore creazione emails.db: " + ex.Message);
            }
        }

        private void LoadDatabases()
        {
            CmbDatabases.Items.Clear();
            if (Directory.Exists(_dbDirectory))
            {
                var files = Directory.GetFiles(_dbDirectory, "*.db");
                foreach (var file in files)
                {
                    CmbDatabases.Items.Add(Path.GetFileName(file));
                }
            }
        }

        private void CmbDatabases_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || CmbDatabases.SelectedItem == null) return;

            // Sul _dbManager VECCHIO, prima di sostituirlo: senza questo, modifiche di cella non
            // ancora salvate sul database che si sta abbandonando andrebbero perse in silenzio.
            SalvaModifichePendenti();

            string selectedDb = CmbDatabases.SelectedItem.ToString()!;
            _dbPath = Path.Combine(_dbDirectory, selectedDb);
            TxtPercorsoFile.Text = $"File aperto: {_dbPath}";

            _dbManager?.Dispose();
            _dbManager = new DatabaseManager(_dbPath);

            LoadTables();
        }

        private void LoadTables()
        {
            if (_dbManager == null) return;
            
            CmbTables.Items.Clear();
            var tables = _dbManager.GetTableNames();
            foreach (var t in tables)
            {
                CmbTables.Items.Add(t);
            }

            if (CmbTables.Items.Count > 0)
            {
                CmbTables.SelectedIndex = 0;
            }
            else
            {
                MainDataGrid.ItemsSource = null;
                _tabellaCorrenteInGriglia = null;
            }
        }

        private void CmbTables_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || CmbTables.SelectedItem == null) return;

            // Sulla tabella VECCHIA (CmbTables.SelectedItem è già quella nuova a questo punto):
            // stessa ragione di CmbDatabases_SelectionChanged, prima di sostituire la griglia.
            SalvaModifichePendenti();

            string selectedTable = CmbTables.SelectedItem.ToString()!;
            LoadDataForTable(selectedTable);
        }

        private void LoadDataForTable(string tableName)
        {
            if (_dbManager == null) return;
            string query = $"SELECT * FROM {tableName};";
            var data = _dbManager.ExecuteQuery(query);
            MainDataGrid.ItemsSource = data.DefaultView;
            _tabellaCorrenteInGriglia = tableName;
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTables.SelectedItem != null)
            {
                LoadDataForTable(CmbTables.SelectedItem.ToString()!);
            }
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null || CmbTables.SelectedItem == null) return;
            string tableName = CmbTables.SelectedItem.ToString()!;

            // Senza questo: aggiungere una riga ricarica subito la griglia da disco (sotto, dopo
            // l'INSERT) e le modifiche di cella non ancora salvate sulla riga aggiunta in
            // precedenza sparirebbero senza preavviso — il bug per cui una serie di "Nuova Riga"
            // consecutivi perdeva tutte le righe tranne l'ultima salvata esplicitamente.
            SalvaModifichePendenti();

            try
            {
                string primaryKeyCol = "";
                if (MainDataGrid.ItemsSource is System.Data.DataView dataView && dataView.Table != null)
                {
                    primaryKeyCol = dataView.Table.Columns.Contains("id") ? "id" :
                                    (dataView.Table.Columns.Contains("file_sig") ? "file_sig" : "");
                }

                if (string.IsNullOrEmpty(primaryKeyCol))
                {
                    MessageBox.Show("Modifica non supportata: Impossibile determinare la chiave primaria.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string query = "";
                if (tableName == "flotte")
                    query = "INSERT INTO flotte (tipo, treno, loco, software) VALUES ('Nuovo', 0, 0, 'Da definire')";
                else if (tableName == "renamer_config")
                    query = "INSERT INTO renamer_config (template) VALUES ('Nuovo Template')";
                else if (tableName == "renamer_queue")
                    query = "INSERT INTO renamer_queue (file_sig, current_path, proposed_name, state, last_template, added_at) VALUES ('" + Guid.NewGuid().ToString()[..8] + "', 'Percorso', 'Nuovo', 'pending', '', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";
                else if (tableName == "renamer_log")
                    query = "INSERT INTO renamer_log (ts, file_sig, old_path, new_path, template, result) VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', 'sig', 'old', 'new', 'temp', 'ok')";
                else if (tableName == "indirizzi_email")
                    query = "INSERT INTO indirizzi_email (nome, email, categoria) VALUES ('Nuovo Contatto', 'email@esempio.com', 'Generale')";
                else
                    query = $"INSERT INTO {tableName} DEFAULT VALUES";

                if (_dbManager.ExecuteNonQuery(query) > 0)
                {
                    LoadDataForTable(tableName); 
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante l'inserimento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (MainDataGrid.SelectedItem == null || CmbTables.SelectedItem == null)
            {
                MessageBox.Show("Seleziona una riga da eliminare.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dbManager == null) return;
            string tableName = CmbTables.SelectedItem.ToString()!;

            // Stessa ragione di BtnAddRow_Click: eliminare una riga ricarica la griglia da disco,
            // che altrimenti butterebbe via le modifiche non salvate delle righe rimaste.
            SalvaModifichePendenti();

            try
            {
                if (MainDataGrid.SelectedItem is System.Data.DataRowView rowView)
                {
                    string primaryKeyCol = rowView.Row.Table.Columns.Contains("id") ? "id" :
                                         (rowView.Row.Table.Columns.Contains("file_sig") ? "file_sig" : "");

                    if (string.IsNullOrEmpty(primaryKeyCol))
                    {
                        MessageBox.Show("Nessuna chiave primaria identificata per questa riga.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string primaryKeyValue = rowView[primaryKeyCol].ToString() ?? "";
                    string query = $"DELETE FROM {tableName} WHERE {primaryKeyCol} = @Id";

                    var param = new System.Collections.Generic.Dictionary<string, object?> { { "@Id", primaryKeyValue } };

                    if (_dbManager.ExecuteNonQuery(query, param) > 0)
                    {
                        LoadDataForTable(tableName); 
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore in eliminazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e) => SalvaModifichePendenti();

        /// <summary>
        /// Scrive su disco, con una <c>UPDATE</c> per riga, le modifiche di cella non ancora salvate
        /// della tabella attualmente legata a <see cref="MainDataGrid"/> — quella indicata da
        /// <see cref="_tabellaCorrenteInGriglia"/>, non necessariamente <c>CmbTables.SelectedItem</c>
        /// (vedi il commento sul campo). Nessun'operazione se non ci sono modifiche pendenti.
        ///
        /// <para>
        /// <b>Perché esiste come metodo a parte, richiamato anche fuori da "Salva Modifiche".</b>
        /// Prima di questa correzione, "Nuova Riga"/"Elimina Riga"/cambio tabella/cambio database
        /// chiamavano tutti <see cref="LoadDataForTable"/> subito dopo la propria operazione,
        /// sostituendo <see cref="MainDataGrid"/>.ItemsSource con una <c>DataTable</c> fresca da
        /// disco. Le modifiche digitate nelle celle vivono solo in quella <c>DataTable</c> in
        /// memoria finché non si preme esplicitamente "Salva Modifiche": la sostituzione le buttava
        /// via in silenzio, senza alcun avviso. Effetto osservato: aggiungere una ventina di treni
        /// con "Nuova Riga", compilarne i campi e passare al successivo prima di salvare cancellava
        /// ogni volta i dati appena digitati sulla riga precedente — l'unico segno lasciato è stato
        /// il contatore <c>sqlite_sequence</c> della tabella, avanzato ben oltre le righe realmente
        /// presenti. Richiamare questo metodo PRIMA di ogni azione che invoca
        /// <see cref="LoadDataForTable"/> rende quella sostituzione sicura: le modifiche pendenti
        /// sono già su disco quando la griglia viene ricaricata.
        /// </para>
        /// </summary>
        private void SalvaModifichePendenti()
        {
            if (_dbManager == null || _tabellaCorrenteInGriglia == null) return;

            try
            {
                if (MainDataGrid.ItemsSource is not System.Data.DataView dataView) return;

                var dataTable = dataView.Table;
                if (dataTable == null) return;

                var modifiedRows = dataTable.GetChanges(System.Data.DataRowState.Modified);
                if (modifiedRows != null)
                {
                    string primaryKeyCol = modifiedRows.Columns.Contains("id") ? "id" :
                                         (modifiedRows.Columns.Contains("file_sig") ? "file_sig" : "");

                    if (string.IsNullOrEmpty(primaryKeyCol))
                    {
                        MessageBox.Show("Modifica non supportata per tabelle senza chiave primaria nota.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    foreach (System.Data.DataRow row in modifiedRows.Rows)
                    {
                        var updates = new System.Collections.Generic.List<string>();
                        var parameters = new System.Collections.Generic.Dictionary<string, object?>();

                        string pkValue = row[primaryKeyCol, System.Data.DataRowVersion.Original].ToString() ?? "";
                        parameters.Add("@PK", pkValue);

                        foreach (System.Data.DataColumn col in modifiedRows.Columns)
                        {
                            if (col.ColumnName != primaryKeyCol)
                            {
                                string paramName = $"@{col.ColumnName}";
                                updates.Add($"{col.ColumnName} = {paramName}");
                                parameters.Add(paramName, row[col] ?? DBNull.Value);
                            }
                        }

                        string updateQuery = $"UPDATE {_tabellaCorrenteInGriglia} SET {string.Join(", ", updates)} WHERE {primaryKeyCol} = @PK";
                        _dbManager.ExecuteNonQuery(updateQuery, parameters);
                    }
                }

                dataTable.AcceptChanges();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
