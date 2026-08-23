using System;
using System.Collections.Generic;
using System.IO;
using PersonalAutomationTool.Modules.Database;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Database
{
    /// <summary>
    /// Verifica che <see cref="DatabaseManager.Dispose"/> rilasci l'handle sul file <c>.db</c> **in
    /// modo deterministico**, cioè che subito dopo il file possa essere spostato o eliminato da un
    /// altro processo.
    ///
    /// <para>
    /// Senza <c>SqliteConnection.ClearPool</c> questo non è garantito: il pool di
    /// <c>Microsoft.Data.Sqlite</c> trattiene la connessione — e quindi l'handle nativo sul file,
    /// più gli eventuali <c>-wal</c>/<c>-shm</c> — anche dopo <c>Close()</c> e <c>Dispose()</c>.
    /// Finché è così il database risulta "in uso" per chiunque altro, incluso un client di
    /// sincronizzazione OneDrive/SharePoint che tenti di caricarlo.
    /// </para>
    ///
    /// <para>
    /// I test spostano ed eliminano file reali: sono la stessa verifica che farebbe il sistema
    /// operativo, non una simulazione.
    /// </para>
    /// </summary>
    public sealed class DatabaseManagerLockTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("DbLock_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string NewDatabase(string name)
        {
            string path = Path.Combine(_root.FullName, name + ".db");
            using var db = new DatabaseManager(path);
            db.ExecuteNonQuery("CREATE TABLE flotte (tipo TEXT, loco TEXT)");
            db.ExecuteNonQuery("INSERT INTO flotte (tipo, loco) VALUES ('ETR700', '117')");
            return path;
        }

        [Fact]
        public void DopoDispose_IlFileDbPuoEssereSpostato()
        {
            string path = NewDatabase("spostabile");
            string destination = Path.Combine(_root.FullName, "spostato.db");

            using (var db = new DatabaseManager(path))
            {
                var righe = db.Query("SELECT tipo FROM flotte", static r => r.GetString(0));
                Assert.Single(righe);
            } // Dispose: chiude, svuota il pool, rilascia l'handle nativo.

            // Se l'handle fosse ancora aperto, questo lancerebbe IOException.
            File.Move(path, destination);

            Assert.True(File.Exists(destination));
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void DopoDispose_IlFileDbPuoEssereEliminato()
        {
            string path = NewDatabase("eliminabile");

            using (var db = new DatabaseManager(path))
            {
                db.ExecuteNonQuery("INSERT INTO flotte (tipo, loco) VALUES ('ETR1000', '119')");
            }

            File.Delete(path);

            Assert.False(File.Exists(path));
        }

        [Fact]
        public void DopoDispose_NessunFileCollateraleWalOShmResta()
        {
            // I file -wal e -shm restano sul disco finché esiste una connessione aperta al database.
            // La loro presenza dopo il rilascio segnalerebbe un handle non chiuso.
            string path = NewDatabase("collaterali");

            using (var db = new DatabaseManager(path))
            {
                db.ExecuteNonQuery("INSERT INTO flotte (tipo, loco) VALUES ('E404P', '627')");
            }

            Assert.False(File.Exists(path + "-wal"), "Il file -wal non è stato rilasciato.");
            Assert.False(File.Exists(path + "-shm"), "Il file -shm non è stato rilasciato.");
        }

        [Fact]
        public void ApertureRipetute_NonAccumulanoHandleAperti()
        {
            // Il percorso reale dell'applicazione: FlotteCache apre e chiude un DatabaseManager a
            // ogni invalidazione della cache. Se ogni ciclo lasciasse un handle nel pool, il file
            // resterebbe bloccato in modo permanente dopo il primo uso.
            string path = NewDatabase("ripetute");

            for (int i = 0; i < 10; i++)
            {
                using var db = new DatabaseManager(path);
                db.Query("SELECT tipo FROM flotte", static r => r.GetString(0));
            }

            string destination = Path.Combine(_root.FullName, "dopo_dieci_aperture.db");
            File.Move(path, destination);

            Assert.True(File.Exists(destination));
        }

        [Fact]
        public void ConnessioneAncoraAperta_IlFileRestaInUso()
        {
            // Controprova: finché il DatabaseManager NON è stato rilasciato, il file deve risultare
            // in uso. Se anche questo test passasse senza eccezione, i tre precedenti non
            // proverebbero nulla — è la verifica che il test è capace di fallire.
            string path = NewDatabase("ancora_aperto");
            string destination = Path.Combine(_root.FullName, "non_spostabile.db");

            using var db = new DatabaseManager(path);
            db.Query("SELECT tipo FROM flotte", static r => r.GetString(0));

            Assert.ThrowsAny<IOException>(() => File.Move(path, destination));
        }
    }
}
