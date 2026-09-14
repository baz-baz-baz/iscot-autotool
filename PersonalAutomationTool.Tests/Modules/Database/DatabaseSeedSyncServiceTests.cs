using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;
using PersonalAutomationTool.Modules.Database;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Database
{
    /// <summary>
    /// Tier 2 (file <c>.db</c> reali su disco): copre il fix per cui un aggiornamento tramite
    /// Auto-Updater lasciava intatto il vecchio <c>train_software.db</c>/<c>emails.db</c> già presente
    /// sul PC del tecnico, perché <see cref="PersonalAutomationTool.Core.AppPaths.EstraiSeedMancanti"/>
    /// scrive un seed solo se il file non esiste ancora. <see cref="DatabaseSeedSyncService"/> aggiunge
    /// il passo mancante: un database locale con <c>PRAGMA user_version</c> più basso del seed viene
    /// allineato — solo sulle tabelle anagrafica master, mai su quelle di storico locale.
    /// </summary>
    public sealed class DatabaseSeedSyncServiceTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("SeedSync_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private string PathFor(string name) => Path.Combine(_root.FullName, name);

        private static void Exec(string dbPath, params string[] statements)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};");
            conn.Open();
            foreach (string sql in statements)
            {
                using var cmd = new SqliteCommand(sql, conn);
                cmd.ExecuteNonQuery();
            }
            SqliteConnection.ClearPool(conn);
        }

        private static int LeggiUserVersion(string dbPath)
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
            conn.Open();
            using var cmd = new SqliteCommand("PRAGMA user_version;", conn);
            var result = (long)cmd.ExecuteScalar()!;
            SqliteConnection.ClearPool(conn);
            return (int)result;
        }

        private static List<(string Nome, string Email)> LeggiIndirizzi(string dbPath)
        {
            var risultato = new List<(string, string)>();
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;");
            conn.Open();
            using var cmd = new SqliteCommand("SELECT nome, email FROM indirizzi_email ORDER BY id;", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                risultato.Add((reader.GetString(0), reader.GetString(1)));
            SqliteConnection.ClearPool(conn);
            return risultato;
        }

        /// <summary>
        /// Lo scenario descritto nel bug: un vecchio <c>emails.db</c> locale, con destinatari mail
        /// obsoleti/mancanti rispetto al seed della nuova release, viene allineato dopo la
        /// sincronizzazione — sia i valori cambiati sia le nuove righe arrivano dal seed.
        /// </summary>
        [Fact]
        public void UnDatabaseLocaleConDestinatariObsoleti_DopoLaSincronizzazioneRifletteIlSeed()
        {
            string locale = PathFor("emails.db");
            string seed = PathFor("emails_seed.db");

            Exec(locale,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (1, 'Mario Rossi', 'mario.vecchio@example.com', 'Generale')",
                "PRAGMA user_version = 0;");

            Exec(seed,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (1, 'Mario Rossi', 'mario.rossi@hitachirail.com', 'Generale')",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (2, 'Nuovo Destinatario', 'nuovo@hitachirail.com', 'Ticket')",
                "PRAGMA user_version = 1;");

            var esito = DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["indirizzi_email"]);

            Assert.Equal(EsitoSincronizzazioneSeed.Eseguita, esito.Esito);
            Assert.Equal(0, esito.VersioneLocale);
            Assert.Equal(1, esito.VersioneSeed);
            Assert.Equal(2, esito.RigheSincronizzate["indirizzi_email"]);

            var indirizzi = LeggiIndirizzi(locale);
            Assert.Equal(2, indirizzi.Count);
            Assert.Contains(("Mario Rossi", "mario.rossi@hitachirail.com"), indirizzi);
            Assert.Contains(("Nuovo Destinatario", "nuovo@hitachirail.com"), indirizzi);
            Assert.DoesNotContain(indirizzi, i => i.Email == "mario.vecchio@example.com");

            Assert.Equal(1, LeggiUserVersion(locale));
        }

        [Fact]
        public void VersioneLocaleGiaAllineata_NonModificaNulla()
        {
            string locale = PathFor("emails.db");
            string seed = PathFor("emails_seed.db");

            Exec(locale,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (1, 'Invariato', 'invariato@example.com', 'Generale')",
                "PRAGMA user_version = 1;");

            Exec(seed,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (1, 'Diverso Nel Seed', 'diverso@example.com', 'Generale')",
                "PRAGMA user_version = 1;");

            var esito = DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["indirizzi_email"]);

            Assert.Equal(EsitoSincronizzazioneSeed.NonNecessaria, esito.Esito);
            var indirizzi = LeggiIndirizzi(locale);
            Assert.Equal("Invariato", Assert.Single(indirizzi).Nome);
        }

        [Fact]
        public void VersioneLocalePiuAltaDelSeed_NonRegredisce()
        {
            // Non dovrebbe accadere in produzione (il seed di una release è sempre >= al precedente),
            // ma la sincronizzazione non deve mai "tornare indietro" se capita.
            string locale = PathFor("emails.db");
            string seed = PathFor("emails_seed.db");

            Exec(locale,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "PRAGMA user_version = 5;");
            Exec(seed,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "PRAGMA user_version = 1;");

            var esito = DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["indirizzi_email"]);

            Assert.Equal(EsitoSincronizzazioneSeed.NonNecessaria, esito.Esito);
            Assert.Equal(5, LeggiUserVersion(locale));
        }

        /// <summary>
        /// Il cuore della garanzia "chirurgica": la sincronizzazione tocca solo le tabelle anagrafica
        /// master indicate, mai lo storico locale dell'operatore (renamer_log) né le sue configurazioni
        /// (renamer_config) — anche quando vivono nello stesso file .db.
        /// </summary>
        [Fact]
        public void TabelleDiStoricoLocale_RestanoInalterateDopoLaSincronizzazione()
        {
            string locale = PathFor("train_software.db");
            string seed = PathFor("train_software_seed.db");

            Exec(locale,
                "CREATE TABLE flotte (id INTEGER PRIMARY KEY, tipo TEXT, loco TEXT)",
                "INSERT INTO flotte (id, tipo, loco) VALUES (1, 'VECCHIO', '100')",
                "CREATE TABLE renamer_log (id INTEGER PRIMARY KEY, old_path TEXT, new_path TEXT)",
                "INSERT INTO renamer_log (id, old_path, new_path) VALUES (1, 'a.pdf', 'b.pdf')",
                "CREATE TABLE renamer_config (id INTEGER PRIMARY KEY, template TEXT)",
                "INSERT INTO renamer_config (id, template) VALUES (1, 'template del tecnico')",
                "PRAGMA user_version = 0;");

            Exec(seed,
                "CREATE TABLE flotte (id INTEGER PRIMARY KEY, tipo TEXT, loco TEXT)",
                "INSERT INTO flotte (id, tipo, loco) VALUES (1, 'NUOVO', '200')",
                "INSERT INTO flotte (id, tipo, loco) VALUES (2, 'NUOVO2', '201')",
                "PRAGMA user_version = 1;");

            var esito = DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["flotte"]);

            Assert.Equal(EsitoSincronizzazioneSeed.Eseguita, esito.Esito);

            using var conn = new SqliteConnection($"Data Source={locale};Mode=ReadOnly;");
            conn.Open();

            using (var cmd = new SqliteCommand("SELECT tipo FROM flotte ORDER BY id;", conn))
            using (var reader = cmd.ExecuteReader())
            {
                Assert.True(reader.Read());
                Assert.Equal("NUOVO", reader.GetString(0));
                Assert.True(reader.Read());
                Assert.Equal("NUOVO2", reader.GetString(0));
                Assert.False(reader.Read());
            }

            using (var cmd = new SqliteCommand("SELECT old_path, new_path FROM renamer_log;", conn))
            using (var reader = cmd.ExecuteReader())
            {
                Assert.True(reader.Read());
                Assert.Equal("a.pdf", reader.GetString(0));
                Assert.Equal("b.pdf", reader.GetString(1));
            }

            using (var cmd = new SqliteCommand("SELECT template FROM renamer_config;", conn))
            using (var reader = cmd.ExecuteReader())
            {
                Assert.True(reader.Read());
                Assert.Equal("template del tecnico", reader.GetString(0));
            }

            SqliteConnection.ClearPool(conn);
        }

        [Fact]
        public void EseguitaLaSincronizzazione_ProducUnFileDiBackupConLaVersionePrecedente()
        {
            string locale = PathFor("emails.db");
            string seed = PathFor("emails_seed.db");

            Exec(locale,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (1, 'X', 'x@example.com', 'Y')",
                "PRAGMA user_version = 0;");
            Exec(seed,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "PRAGMA user_version = 1;");

            DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["indirizzi_email"]);

            var backup = _root.GetFiles("emails_backup_v0_*.db");
            Assert.Single(backup);
        }

        [Fact]
        public void DopoLaSincronizzazione_LaConnessioneVieneRilasciataEIlFilePuoEssereSpostato()
        {
            // Stessa garanzia di DatabaseManagerLockTests, applicata al percorso di sincronizzazione:
            // un handle rimasto aperto lascerebbe il .db "in uso" per il resto dell'avvio.
            string locale = PathFor("emails.db");
            string seed = PathFor("emails_seed.db");

            Exec(locale,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "PRAGMA user_version = 0;");
            Exec(seed,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "INSERT INTO indirizzi_email (id, nome, email, categoria) VALUES (1, 'X', 'x@example.com', 'Y')",
                "PRAGMA user_version = 1;");

            DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["indirizzi_email"]);

            string spostato = PathFor("emails_spostato.db");
            File.Move(locale, spostato);
            Assert.True(File.Exists(spostato));
        }

        [Fact]
        public void NessunoDeiDueFile_LaSincronizzazioneNonFaNulla()
        {
            string locale = PathFor("non_esiste.db");
            string seed = PathFor("nemmeno_questo.db");

            var esito = DatabaseSeedSyncService.SincronizzaSeNecessario(locale, seed, ["indirizzi_email"]);

            Assert.Equal(EsitoSincronizzazioneSeed.NonNecessaria, esito.Esito);
        }

        /// <summary>
        /// Verifica l'intero percorso pubblico usato da <c>App.xaml.cs</c>: estrazione dal seed
        /// incorporato nell'assembly reale (non un file temporaneo preparato a mano), confronto
        /// versione e sincronizzazione — usando le stesse risorse incorporate della produzione
        /// (<see cref="PersonalAutomationTool.Core.AppPaths.SeedIncorporatiGestiti"/>).
        /// </summary>
        [Fact]
        public void SincronizzaDaRisorsaIncorporata_UsaIlSeedRealeDellAssembly()
        {
            string locale = PathFor("emails.db");
            Exec(locale,
                "CREATE TABLE indirizzi_email (id INTEGER PRIMARY KEY, nome TEXT, email TEXT, categoria TEXT)",
                "PRAGMA user_version = 0;");

            Assembly assembly = typeof(PersonalAutomationTool.Core.AppPaths).Assembly;

            var esito = DatabaseSeedSyncService.SincronizzaDaRisorsaIncorporata(
                locale, assembly, "Seed.emails.db", ["indirizzi_email"]);

            Assert.Equal(EsitoSincronizzazioneSeed.Eseguita, esito.Esito);
            Assert.True(esito.VersioneSeed >= 1);
            Assert.True(esito.RigheSincronizzate["indirizzi_email"] > 0);

            // Il temporaneo usato per l'ATTACH non deve restare sul disco dopo la sincronizzazione.
            var residui = Directory.GetFiles(Path.GetTempPath(), "emails.db.seed-*");
            Assert.Empty(residui);
        }

        [Fact]
        public void SincronizzaAllAvvio_CopreTuttiIFileRegistratiInTabelleMasterPerFile()
        {
            // AppPaths.SeedIncorporatiGestiti è la fonte d'origine dei file gestiti: se un domani si
            // aggiunge un terzo .db seed senza registrare le sue tabelle master, questo test lo
            // segnala invece di lasciarlo silenziosamente ignorato da SincronizzaAllAvvio.
            foreach (var (relativo, _) in PersonalAutomationTool.Core.AppPaths.SeedIncorporatiGestiti)
            {
                string nomeFile = Path.GetFileName(relativo);
                Assert.True(
                    nomeFile is "train_software.db" or "emails.db",
                    $"'{nomeFile}' è un seed incorporato non ancora coperto da DatabaseSeedSyncService.");
            }
        }
    }
}
