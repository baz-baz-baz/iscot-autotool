using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.Sqlite;

namespace PersonalAutomationTool.Modules.Database
{
    public class DatabaseManager : IDisposable
    {
        private readonly string connectionString;
        private SqliteConnection? _connection;

        public DatabaseManager(string dbPath)
        {
            connectionString = $"Data Source={dbPath};";
            OpenConnection();
        }

        private void OpenConnection()
        {
            try
            {
                _connection ??= new SqliteConnection(connectionString);

                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Connection Error: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                if (_connection.State == ConnectionState.Open)
                {
                    _connection.Close();
                }
                _connection.Dispose();
                _connection = null;
            }
            GC.SuppressFinalize(this);
        }

        public DataTable ExecuteQuery(string query)
        {
            var dataTable = new DataTable();

            try
            {
                OpenConnection();
                if (_connection == null) throw new InvalidOperationException("Connessione non inizializzata.");

                using var command = new SqliteCommand(query, _connection);
                using var reader = command.ExecuteReader();
                dataTable.Load(reader);
            }
            catch (Exception ex)
            {
                // Gestione semplice dell'errore restituendo il messaggio nella tabella
                dataTable.Columns.Add("Errore");
                dataTable.Rows.Add(ex.Message);
            }

            return dataTable;
        }

        public List<string> GetTableNames()
        {
            var tables = new List<string>();
            var query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";

            var resultTable = ExecuteQuery(query);
            foreach (DataRow row in resultTable.Rows)
            {
                var tableName = row["name"]?.ToString();
                if (!string.IsNullOrEmpty(tableName))
                {
                    tables.Add(tableName);
                }
            }
            return tables;
        }

        public int ExecuteNonQuery(string query, Dictionary<string, object?>? parameters = null)
        {
            try
            {
                OpenConnection();
                if (_connection == null) throw new InvalidOperationException("Connessione non inizializzata.");

                using var command = new SqliteCommand(query, _connection);
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                    }
                }
                return command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Errore SQL: {ex.Message}");
            }
        }
    }
}
