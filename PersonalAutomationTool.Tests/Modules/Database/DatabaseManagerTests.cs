using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Modules.Database;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Database
{
    /// <summary>
    /// Tier 2: un vero file SQLite temporaneo per test (<see cref="Directory.CreateTempSubdirectory"/>,
    /// ripulito a fine test), non un database in memoria — lo stesso genere di percorso reale che
    /// <c>FlotteCache</c> e <c>RubricaDialog</c> attraversano in produzione tramite
    /// <see cref="DatabaseManager.Query{T}"/> (intervento 2.7, Sprint 3).
    /// </summary>
    public sealed class DatabaseManagerTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("DatabaseManagerTests_");
        private readonly string _dbPath;

        public DatabaseManagerTests()
        {
            _dbPath = Path.Combine(_root.FullName, "test.db");
        }

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private sealed record Riga(string Tipo, string Loco);

        [Fact]
        public void Query_ProiettaOgniRigaTramiteMap()
        {
            using (var db = new DatabaseManager(_dbPath))
            {
                db.ExecuteNonQuery("CREATE TABLE flotte (tipo TEXT, loco TEXT)");
                db.ExecuteNonQuery("INSERT INTO flotte (tipo, loco) VALUES ('ETR700', '117')");
                db.ExecuteNonQuery("INSERT INTO flotte (tipo, loco) VALUES ('ETR1000', '204')");
            }

            using var reader = new DatabaseManager(_dbPath);
            List<Riga> righe = reader.Query("SELECT tipo, loco FROM flotte ORDER BY tipo", static r =>
                new Riga(r.IsDBNull(0) ? "" : r.GetValue(0)!.ToString()!, r.IsDBNull(1) ? "" : r.GetValue(1)!.ToString()!));

            Assert.Equal(2, righe.Count);
            Assert.Equal(new Riga("ETR1000", "204"), righe[0]);
            Assert.Equal(new Riga("ETR700", "117"), righe[1]);
        }

        [Fact]
        public void Query_ColonnaIntegerLetta_NonLanciaEProduceStringa()
        {
            // Scoperta della sessione: `loco`/`treno` in `flotte` possono avere storage class INTEGER
            // anche su una colonna dichiarata TEXT (SQLite ha tipizzazione dinamica) — succede quando
            // una riga viene inserita da `DatabaseView` con valori numerici letterali (0, 0). Il
            // mapping deve leggere con `GetValue().ToString()`, non `GetString()`, o questo caso lancia.
            using (var db = new DatabaseManager(_dbPath))
            {
                db.ExecuteNonQuery("CREATE TABLE flotte (tipo TEXT, treno INTEGER)");
                db.ExecuteNonQuery("INSERT INTO flotte (tipo, treno) VALUES ('Nuovo', 0)");
            }

            using var reader = new DatabaseManager(_dbPath);
            var righe = reader.Query("SELECT tipo, treno FROM flotte", static r =>
                (Tipo: r.IsDBNull(0) ? "" : r.GetValue(0)!.ToString()!, Treno: r.IsDBNull(1) ? "" : r.GetValue(1)!.ToString()!));

            var riga = Assert.Single(righe);
            Assert.Equal("Nuovo", riga.Tipo);
            Assert.Equal("0", riga.Treno);
        }

        [Fact]
        public void Query_TabellaInesistente_RestituisceListaVuota_NonLancia()
        {
            using var db = new DatabaseManager(_dbPath);

            List<string> righe = db.Query("SELECT * FROM tabella_inesistente", static r => r.GetValue(0)!.ToString()!);

            Assert.Empty(righe);
        }

        [Fact]
        public void Query_NessunaRiga_RestituisceListaVuota()
        {
            using var db = new DatabaseManager(_dbPath);
            db.ExecuteNonQuery("CREATE TABLE flotte (tipo TEXT)");

            List<string> righe = db.Query("SELECT tipo FROM flotte", static r => r.GetValue(0)!.ToString()!);

            Assert.Empty(righe);
        }

        [Fact]
        public void GetTableNames_UsaQuery_ElencaTabelleUtente()
        {
            using var db = new DatabaseManager(_dbPath);
            db.ExecuteNonQuery("CREATE TABLE flotte (tipo TEXT)");
            db.ExecuteNonQuery("CREATE TABLE indirizzi_email (nome TEXT)");

            var tables = db.GetTableNames();

            Assert.Equal(["flotte", "indirizzi_email"], tables.OrderBy(t => t, StringComparer.Ordinal));
        }
    }
}
