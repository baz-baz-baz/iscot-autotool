using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace PersonalAutomationTool.Modules.Database
{
    public class DatabaseManager : IDisposable
    {
        // Era statico: serializzava gli accessi di TUTTE le istanze di DatabaseManager nel processo,
        // anche verso file .db diversi (es. una query su emails.db aspettava una query su
        // train_software.db in corso su un'altra istanza, senza motivo). Ogni istanza incapsula
        // una propria SqliteConnection: il lock deve solo proteggere quella, non essere globale.
        private readonly object _dbLock = new();
        private readonly string connectionString;
        private SqliteConnection? _connection;

        public DatabaseManager(string dbPath)
        {
            connectionString = $"Data Source={dbPath};";
            OpenConnection();
        }

        private void OpenConnection()
        {
            lock (_dbLock)
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
        }

        public void Dispose()
        {
            lock (_dbLock)
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
            }
            GC.SuppressFinalize(this);
        }

        public DataTable ExecuteQuery(string query, Dictionary<string, object?>? parameters = null)
        {
            var dataTable = new DataTable();

            lock (_dbLock)
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
                    using var reader = command.ExecuteReader();
                    dataTable.Load(reader);
                }
                catch (Exception ex)
                {
                    // Gestione semplice dell'errore restituendo il messaggio nella tabella
                    dataTable.Columns.Add("Errore");
                    dataTable.Rows.Add(ex.Message);
                }
            }

            return dataTable;
        }

        public List<string> GetTableNames()
        {
            const string query = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
            return Query(query, static reader => reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "")
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();
        }

        /// <summary>
        /// Lettura tipizzata: esegue <paramref name="query"/> e proietta ogni riga tramite
        /// <paramref name="map"/>, senza mai materializzare un <see cref="DataTable"/> intermedio.
        /// Stessa politica di errore di <see cref="ExecuteQuery"/> (nessuna eccezione verso il
        /// chiamante: un errore SQL produce una lista vuota, loggata su <see cref="System.Diagnostics.Debug"/>)
        /// — a differenza di quella però qui l'errore non può essere distinto da "nessuna riga",
        /// perché non c'è più una colonna "Errore" su cui il chiamante possa controllare: usarla dove
        /// quella distinzione non serve al chiamante (i punti migrati in questa sessione la
        /// scartavano comunque subito dopo averla controllata).
        /// </summary>
        public List<T> Query<T>(string query, Func<SqliteDataReader, T> map, Dictionary<string, object?>? parameters = null)
        {
            var results = new List<T>();

            lock (_dbLock)
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
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add(map(reader));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DB Query Error: {ex.Message}");
                }
            }

            return results;
        }

        public int ExecuteNonQuery(string query, Dictionary<string, object?>? parameters = null)
        {
            lock (_dbLock)
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
}
