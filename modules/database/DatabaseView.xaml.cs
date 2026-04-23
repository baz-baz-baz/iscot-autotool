using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Database
{
    public partial class DatabaseView : UserControl
    {
        private DatabaseManager? _dbManager;
        private string? _dbPath;

        public DatabaseView()
        {
            InitializeComponent();
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                // Il database è stato inserito dall'utente nella cartella corrente del modulo (modules/database/train_software.db)
                // Costruiamo il percorso assoluto partendo dalla directory base dell'eseguibile / progetto
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // L'eseguibile di VisualStudio è in bin/Debug/net..., noi vogliamo risalire al progetto e cercare in modules/database
                // (Approccio locale, per ora cerchiamo di puntare al vero path in cui si trova)
                DirectoryInfo? dir = new(baseDir);
                while (dir != null && dir.Name != "PersonalAutomationTool")
                {
                    dir = dir.Parent;
                }

                if (dir == null)
                {
                    MessageBox.Show("Impossibile trovare la cartella di base del progetto.", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _dbPath = Path.Combine(dir.FullName, "modules", "database", "train_software.db");

                if (!File.Exists(_dbPath))
                {
                    MessageBox.Show($"File database non trovato in:\n{_dbPath}", "Errore Database", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                _dbManager = new DatabaseManager(_dbPath);
                LoadTables();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore nell'inizializzazione del database: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTables()
        {
            if (_dbManager == null) return;

            LoadDataForTable("flotte");
        }

        private void LoadDataForTable(string tableName)
        {
            if (_dbManager == null) return;
            string query = $"SELECT * FROM {tableName};";
            var data = _dbManager.ExecuteQuery(query);
            MainDataGrid.ItemsSource = data.DefaultView;
        }

        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            LoadTables();
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null) return;
            string tableName = "flotte";

            try
            {
                // A seconda della tabella aggiunge una riga vuota
                // Cerchiamo di dedurre la chiave primaria.
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

                // Inseriamo un record "vuoto" nel db.
                // SQLite per le chiavi primarie incrementali gestisce un INSERT INTO tableName DEFAULT VALUES (se tutte libere)
                // Nel caso del nostro schema 'id' è autoincrement in flotte e config, ma log ha timestamp ecc.
                // Per semplificare, usiamo una query che definisce valori di default generici.

                string query = "";
                if (tableName == "flotte")
                    query = "INSERT INTO flotte (tipo, treno, loco, software) VALUES ('Nuovo', 0, 0, 'Da definire')";
                else if (tableName == "renamer_config")
                    query = "INSERT INTO renamer_config (template) VALUES ('Nuovo Template')";
                else if (tableName == "renamer_queue")
                    query = "INSERT INTO renamer_queue (file_sig, current_path, proposed_name, state, last_template, added_at) VALUES ('" + Guid.NewGuid().ToString()[..8] + "', 'Percorso', 'Nuovo', 'pending', '', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";
                else if (tableName == "renamer_log")
                    query = "INSERT INTO renamer_log (ts, file_sig, old_path, new_path, template, result) VALUES ('" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', 'sig', 'old', 'new', 'temp', 'ok')";
                else
                    query = $"INSERT INTO {tableName} DEFAULT VALUES";

                if (_dbManager.ExecuteNonQuery(query) > 0)
                {
                    LoadDataForTable(tableName); // Ricarica mostrandola nella griglia
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante l'inserimento: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (MainDataGrid.SelectedItem == null)
            {
                MessageBox.Show("Seleziona una riga da eliminare.", "Errore", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_dbManager == null) return;
            string tableName = "flotte";

            try
            {
                // Estrarre ID dalla riga selezionata per eliminarla
                if (MainDataGrid.SelectedItem is System.Data.DataRowView rowView)
                {
                    // L'assunto base per le tabelle estratte è avere un identificatore univoco. 
                    // 'id' per flotte, config, log e 'file_sig' per queue. 
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
                        LoadDataForTable(tableName); // ricarica la tabella dopo eliminazione
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore in eliminazione: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MainDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // La modifica delle celle verrà colta al salvataggio
            // Potremmo intercettare qui il cambiamento per update immediato row per row.
        }

        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_dbManager == null) return;
            string tableName = "flotte";

            try
            {
                // Estraiamo il DataTable associato al DataGrid
                if (MainDataGrid.ItemsSource is not System.Data.DataView dataView) return;

                var dataTable = dataView.Table;
                if (dataTable == null) return;

                // Controlliamo righe modificate
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

                        string updateQuery = $"UPDATE {tableName} SET {string.Join(", ", updates)} WHERE {primaryKeyCol} = @PK";
                        _dbManager.ExecuteNonQuery(updateQuery, parameters);
                    }
                }

                dataTable.AcceptChanges();
                MessageBox.Show("Salvataggio completato con successo.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
