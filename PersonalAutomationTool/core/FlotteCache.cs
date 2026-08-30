using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace PersonalAutomationTool.Core
{
    /// <summary>Una riga della tabella <c>flotte</c> di <c>train_software.db</c>, letta una sola volta e riusata.</summary>
    public sealed record FlotteRecord(string Tipo, string Treno, string Loco, string Software);

    /// <summary>
    /// Cache in memoria dell'intera tabella <c>flotte</c>, invalidata su data di modifica +
    /// dimensione del file (stesso pattern già usato per i JSON di configurazione). Prima di
    /// questa cache, ognuna delle sei query su questa tabella (CartelleView ×3, VerificheViewModel,
    /// PdfView, EmailService) apriva una connessione SQLite indipendente per ogni singola ricerca
    /// tipo/loco — con la tabella flotte tipicamente poche centinaia di righe, tenerla intera in
    /// memoria costa pochi KB e azzera quel costo.
    /// <para>
    /// <b>Non usata da `DatabaseView`</b>: quella schermata deve mostrare lo stato live del database
    /// (supporta inserimento/modifica/eliminazione righe), quindi continua a interrogare SQLite
    /// direttamente. Usare qui la cache mostrerebbe dati non aggiornati subito dopo una modifica.
    /// </para>
    /// <para>
    /// Ogni metodo di ricerca replica **esattamente** la query SQL che sostituisce, comprese le
    /// differenze fra chiamanti: solo <see cref="FindTrenoByLoco"/> (che sostituisce la query di
    /// <c>VerificheViewModel</c>) applica il fallback numerico su <c>loco</c> — è l'unica delle sei
    /// query originali ad averlo. Gli altri metodi restano a confronto testuale puro, come le query
    /// che sostituiscono.
    /// </para>
    /// </summary>
    public static class FlotteCache
    {
        private static readonly object _lock = new();
        private static List<FlotteRecord>? _cached;
        private static DateTime _cachedWriteTimeUtc;
        private static long _cachedLength = -1;

        private static string DbPath => AppPaths.DatabaseFile("train_software.db");

        /// <summary>
        /// Legge la colonna <paramref name="index"/> come stringa senza assumerne il tipo di storage
        /// SQLite (nella tabella <c>flotte</c>, <c>treno</c>/<c>loco</c> possono essere INTEGER o TEXT
        /// a seconda di come sono state inserite le righe — SQLite è a tipizzazione dinamica).
        /// <see cref="SqliteDataReader.GetString"/> lancerebbe su una colonna INTEGER; <c>GetValue().ToString()</c>
        /// replica esattamente il comportamento di <c>DataRow["col"]?.ToString()</c> usato dal codice
        /// che questa classe sostituisce.
        /// </summary>
        private static string Col(SqliteDataReader reader, int index) =>
            reader.IsDBNull(index) ? "" : reader.GetValue(index)?.ToString() ?? "";

        /// <summary>
        /// Crea (se assenti) gli indici su <c>(tipo, loco)</c> e su <c>loco</c>. Idempotente
        /// (<c>CREATE INDEX IF NOT EXISTS</c>): sicura da richiamare a ogni avvio. Con l'intera
        /// tabella già in cache in memoria, questi indici non servono più alle ricerche di questa
        /// classe — restano utili a `DatabaseView`, che deve continuare a interrogare SQLite
        /// direttamente per mostrare dati sempre aggiornati (vedi il commento sulla classe).
        /// Va chiamata fuori dal thread UI: anche se rapida su una tabella di poche centinaia di
        /// righe, resta comunque I/O su disco.
        /// </summary>
        public static void EnsureIndices()
        {
            try
            {
                if (!File.Exists(DbPath)) return;

                using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(DbPath);
                db.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_flotte_tipo_loco ON flotte(tipo, loco);");
                db.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_flotte_loco ON flotte(loco);");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore creazione indici flotte: {ex.Message}");
            }
        }

        /// <summary>Invalida la cache: da chiamare dopo una modifica alla tabella flotte da DatabaseView.</summary>
        public static void Invalidate()
        {
            lock (_lock)
            {
                _cached = null;
                _cachedLength = -1;
                _cachedWriteTimeUtc = default;
            }
        }

        private static List<FlotteRecord> GetAll()
        {
            var info = new FileInfo(DbPath);
            if (!info.Exists) return [];

            lock (_lock)
            {
                if (_cached != null && _cachedLength == info.Length && _cachedWriteTimeUtc == info.LastWriteTimeUtc)
                {
                    return _cached;
                }

                List<FlotteRecord> result;
                try
                {
                    using var db = new PersonalAutomationTool.Modules.Database.DatabaseManager(DbPath);
                    result = db.Query("SELECT tipo, treno, loco, software FROM flotte", static reader => new FlotteRecord(
                        Col(reader, 0), Col(reader, 1), Col(reader, 2), Col(reader, 3)));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Errore lettura cache flotte: {ex.Message}");
                    result = [];
                }

                _cached = result;
                _cachedLength = info.Length;
                _cachedWriteTimeUtc = info.LastWriteTimeUtc;
                return result;
            }
        }

        /// <summary>Sostituisce <c>SELECT DISTINCT tipo FROM flotte ORDER BY tipo</c> (CartelleView).</summary>
        public static List<string> GetDistinctTipiOrderByName() =>
            [.. GetAll().Select(r => r.Tipo).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal)];

        /// <summary>Sostituisce <c>SELECT DISTINCT tipo FROM flotte ORDER BY LENGTH(tipo) DESC</c> (PdfView).</summary>
        public static List<string> GetDistinctTipiOrderByLengthDesc() =>
            [.. GetAll().Select(r => r.Tipo).Distinct(StringComparer.Ordinal).OrderByDescending(t => t.Length)];

        /// <summary>Sostituisce <c>SELECT treno FROM flotte WHERE tipo=@tipo AND loco=@loco LIMIT 1</c> (CartelleView).</summary>
        public static string? FindTreno(string tipo, string loco) =>
            GetAll().FirstOrDefault(r => r.Tipo == tipo && r.Loco == loco)?.Treno;

        /// <summary>Sostituisce <c>SELECT software FROM flotte WHERE tipo=@tipo AND loco=@loco LIMIT 1</c> (CartelleView).</summary>
        public static string? FindSoftware(string tipo, string loco) =>
            GetAll().FirstOrDefault(r => r.Tipo == tipo && r.Loco == loco)?.Software;

        /// <summary>Sostituisce <c>SELECT treno, software FROM flotte WHERE tipo=@tipo AND loco=@loco</c> (EmailService).</summary>
        public static FlotteRecord? FindByTipoAndLoco(string tipo, string loco) =>
            GetAll().FirstOrDefault(r => r.Tipo == tipo && r.Loco == loco);

        /// <summary>
        /// Sostituisce <c>SELECT treno FROM flotte WHERE loco=@loco OR loco=@locoInt</c> (VerificheViewModel).
        /// L'originale aggiungeva il parametro numerico solo se <c>loco</c> era interamente numerico,
        /// per compensare eventuali righe con affinità INTEGER sulla colonna. Qui la stessa
        /// compensazione avviene confrontando anche la rappresentazione numerica della loco in cache.
        /// </summary>
        public static string? FindTrenoByLoco(string loco)
        {
            var all = GetAll();
            var exact = all.FirstOrDefault(r => r.Loco == loco);
            if (exact != null) return exact.Treno;

            if (int.TryParse(loco, out int locoInt))
            {
                var byInt = all.FirstOrDefault(r => int.TryParse(r.Loco, out int cachedLocoInt) && cachedLocoInt == locoInt);
                if (byInt != null) return byInt.Treno;
            }

            return null;
        }
    }
}
