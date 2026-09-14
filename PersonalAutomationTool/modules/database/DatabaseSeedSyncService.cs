using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Database
{
    public enum EsitoSincronizzazioneSeed
    {
        NonNecessaria,
        Eseguita,
        Fallita
    }

    /// <summary>Esito della sincronizzazione forzata di un singolo file <c>.db</c>.</summary>
    public sealed record RisultatoSincronizzazioneSeed(
        string DbFile,
        EsitoSincronizzazioneSeed Esito,
        int VersioneLocale,
        int VersioneSeed,
        IReadOnlyDictionary<string, int> RigheSincronizzate,
        string? Errore = null);

    /// <summary>
    /// Allineamento forzato delle tabelle anagrafiche "master" (<c>flotte</c>, <c>indirizzi_email</c>)
    /// al seed incorporato nella release corrente, all'avvio dell'applicazione.
    ///
    /// <para>
    /// <b>Il difetto che questa classe corregge.</b> <see cref="AppPaths.EstraiSeedMancanti"/> scrive
    /// un database seed solo se il file **non esiste ancora** nella cartella dati — la regola giusta
    /// per non perdere lo storico locale (<c>renamer_log</c>, <c>renamer_queue</c>), ma sbagliata per
    /// le tabelle anagrafiche: un tecnico che aggiorna l'app tramite l'Auto-Updater continua a
    /// lavorare sul <c>train_software.db</c> già presente sul suo PC, senza mai ricevere le righe di
    /// <c>flotte</c> né i <c>indirizzi_email</c> aggiornati sulla macchina di sviluppo — esattamente
    /// il comportamento verificato su questa stessa macchina prima del fix: la cartella dati aveva
    /// <c>flotte</c> ferma a 228 righe mentre il seed incorporato nel repository ne conta già 278.
    /// </para>
    ///
    /// <para>
    /// <b>Il meccanismo di versione.</b> Ogni file <c>.db</c> porta la propria versione in
    /// <c>PRAGMA user_version</c> (nativo di SQLite, nessuna tabella aggiuntiva da mantenere). Un
    /// database mai versionato risulta a 0: è il caso di ogni installazione precedente a questo fix,
    /// quindi la prima sincronizzazione scatta automaticamente al primo avvio dopo l'aggiornamento. Il
    /// seed incorporato (<see cref="AppPaths.SeedIncorporatiGestiti"/>) è la sola fonte confrontata:
    /// non un file "loose" nella cartella di installazione, per la stessa ragione per cui
    /// <c>AppPaths</c> incorpora i seed come risorsa invece di affidarsi al contenuto estratto del
    /// bundle single-file (§ "Profilo di distribuzione" nel <c>.csproj</c>).
    /// </para>
    ///
    /// <para>
    /// <b>Cosa viene toccato, cosa no.</b> Solo le tabelle elencate in
    /// <see cref="TabelleMasterPerFile"/> vengono svuotate e reinserite dal seed, dentro una singola
    /// transazione per file (atomiche: o tutte le righe della tabella arrivano dal seed, o nessuna
    /// modifica resta). Le altre tabelle dello stesso file — <c>renamer_log</c>, <c>renamer_queue</c>,
    /// <c>renamer_config</c> — non vengono mai referenziate da questa classe, quindi restano intatte
    /// indipendentemente da quante volte la sincronizzazione viene eseguita.
    /// </para>
    /// </summary>
    public static class DatabaseSeedSyncService
    {
        /// <summary>
        /// Tabelle "anagrafica master" da forzare per ciascun file .db gestito da
        /// <see cref="AppPaths.SeedIncorporatiGestiti"/>. Chiave = nome file (non percorso), per
        /// restare valida sia per il percorso relativo di sviluppo sia per quello di produzione.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> TabelleMasterPerFile =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["train_software.db"] = ["flotte"],
                ["emails.db"] = ["indirizzi_email"],
            };

        private static readonly IReadOnlyDictionary<string, int> RigheVuote = new Dictionary<string, int>();

        /// <summary>
        /// Punto d'ingresso da <c>App.xaml.cs</c>: sincronizza tutti i database seed gestiti. Da
        /// chiamare una sola volta all'avvio, dopo <see cref="AppPaths.Initialize"/> (i file locali
        /// devono già esistere) e prima che qualunque modulo (Email, Cartelle, Verifiche, Database,
        /// DestinatariMail) interroghi SQLite.
        /// </summary>
        public static IReadOnlyList<RisultatoSincronizzazioneSeed> SincronizzaAllAvvio()
        {
            var risultati = new List<RisultatoSincronizzazioneSeed>();
            Assembly assembly = typeof(AppPaths).Assembly;

            foreach (var (relativo, risorsa) in AppPaths.SeedIncorporatiGestiti)
            {
                string nomeFile = Path.GetFileName(relativo);
                if (!TabelleMasterPerFile.TryGetValue(nomeFile, out string[]? tabelle)) continue;

                string dbLocale = AppPaths.DataFile(relativo);
                risultati.Add(SincronizzaDaRisorsaIncorporata(dbLocale, assembly, risorsa, tabelle));
            }

            return risultati;
        }

        /// <summary>
        /// Estrae la risorsa seed <paramref name="risorsaSeed"/> in un file temporaneo — necessario
        /// perché <c>ATTACH DATABASE</c> richiede un percorso su disco, non uno stream in memoria — e
        /// lo confronta con <paramref name="dbPathLocale"/>. Il temporaneo viene sempre ripulito, sia
        /// in caso di successo sia di errore.
        /// </summary>
        internal static RisultatoSincronizzazioneSeed SincronizzaDaRisorsaIncorporata(
            string dbPathLocale, Assembly assembly, string risorsaSeed, string[] tabelleMaster)
        {
            string nomeFile = Path.GetFileName(dbPathLocale);

            if (!File.Exists(dbPathLocale))
                return new(nomeFile, EsitoSincronizzazioneSeed.NonNecessaria, 0, 0, RigheVuote);

            string seedTemporaneo = Path.Combine(Path.GetTempPath(), $"{nomeFile}.seed-{Guid.NewGuid():N}");
            try
            {
                using (Stream? contenuto = assembly.GetManifestResourceStream(risorsaSeed))
                {
                    if (contenuto == null)
                        return new(nomeFile, EsitoSincronizzazioneSeed.NonNecessaria, 0, 0, RigheVuote);

                    using var file = new FileStream(seedTemporaneo, FileMode.Create, FileAccess.Write, FileShare.None);
                    contenuto.CopyTo(file);
                }

                return SincronizzaSeNecessario(dbPathLocale, seedTemporaneo, tabelleMaster);
            }
            finally
            {
                try { if (File.Exists(seedTemporaneo)) File.Delete(seedTemporaneo); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"Pulizia del seed temporaneo '{seedTemporaneo}' non riuscita: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Confronta <c>PRAGMA user_version</c> fra <paramref name="dbPathLocale"/> e
        /// <paramref name="dbPathSeed"/>: se il locale non è indietro non fa nulla. Altrimenti esegue
        /// un backup preventivo e sostituisce, dentro un'unica transazione, il contenuto di ogni
        /// tabella in <paramref name="tabelleMaster"/> con quello del seed, poi allinea la versione.
        /// Un errore in qualunque fase (ATTACH, transazione, lock del file) viene intercettato e
        /// restituito come esito: non deve mai impedire l'avvio dell'applicazione.
        /// </summary>
        internal static RisultatoSincronizzazioneSeed SincronizzaSeNecessario(
            string dbPathLocale, string dbPathSeed, string[] tabelleMaster)
        {
            string nomeFile = Path.GetFileName(dbPathLocale);

            if (!File.Exists(dbPathLocale) || !File.Exists(dbPathSeed))
                return new(nomeFile, EsitoSincronizzazioneSeed.NonNecessaria, 0, 0, RigheVuote);

            int versioneLocale = LeggiUserVersion(dbPathLocale);
            int versioneSeed = LeggiUserVersion(dbPathSeed);

            if (versioneLocale >= versioneSeed)
                return new(nomeFile, EsitoSincronizzazioneSeed.NonNecessaria, versioneLocale, versioneSeed, RigheVuote);

            EseguiBackupPreventivo(dbPathLocale, versioneLocale);

            try
            {
                var righe = new Dictionary<string, int>();

                using (var db = new DatabaseManager(dbPathLocale))
                {
                    db.ExecuteNonQuery("ATTACH DATABASE @seed AS seed_master;",
                        new Dictionary<string, object?> { ["@seed"] = dbPathSeed });

                    try
                    {
                        db.ExecuteNonQuery("BEGIN IMMEDIATE;");
                        try
                        {
                            foreach (string tabella in tabelleMaster)
                            {
                                // I nomi delle tabelle vengono solo da TabelleMasterPerFile (costante
                                // interna, mai da input esterno): l'interpolazione qui non espone
                                // un'iniezione SQL, i parametri legano solo i valori (il percorso del
                                // seed sopra), non identificatori come nomi di tabella.
                                db.ExecuteNonQuery($"DELETE FROM {tabella};");
                                db.ExecuteNonQuery($"INSERT INTO {tabella} SELECT * FROM seed_master.{tabella};");
                                righe[tabella] = ContaRighe(db, tabella);
                            }

                            db.ExecuteNonQuery($"PRAGMA user_version = {versioneSeed};");
                            db.ExecuteNonQuery("COMMIT;");
                        }
                        catch
                        {
                            db.ExecuteNonQuery("ROLLBACK;");
                            throw;
                        }
                    }
                    finally
                    {
                        // Deve girare sia dopo COMMIT sia dopo ROLLBACK: un ATTACH lasciato aperto
                        // farebbe fallire il prossimo avvio ("database seed_master already in use").
                        db.ExecuteNonQuery("DETACH DATABASE seed_master;");
                    }
                } // Dispose: chiude e libera il pool subito, come DatabaseManagerLockTests verifica.

                return new(nomeFile, EsitoSincronizzazioneSeed.Eseguita, versioneLocale, versioneSeed, righe);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sincronizzazione seed fallita per '{nomeFile}': {ex.Message}");
                return new(nomeFile, EsitoSincronizzazioneSeed.Fallita, versioneLocale, versioneSeed, RigheVuote, ex.Message);
            }
        }

        private static int LeggiUserVersion(string dbPath)
        {
            using var db = new DatabaseManager(dbPath);
            List<int> risultato = db.Query("PRAGMA user_version;", static r => r.GetInt32(0));
            return risultato.Count > 0 ? risultato[0] : 0;
        }

        private static int ContaRighe(DatabaseManager db, string tabella)
        {
            List<int> risultato = db.Query($"SELECT COUNT(*) FROM {tabella};", static r => r.GetInt32(0));
            return risultato.Count > 0 ? risultato[0] : 0;
        }

        /// <summary>
        /// Copia di sicurezza best-effort prima della sovrascrittura chirurgica. Non bloccante: se il
        /// backup fallisce (disco pieno, cartella non scrivibile) la sincronizzazione prosegue lo
        /// stesso, stessa filosofia di <see cref="AppPaths.TrasferisciFileMancanti"/> — un singolo
        /// passo non riuscito non deve impedire l'avvio dell'applicazione.
        /// </summary>
        private static void EseguiBackupPreventivo(string dbPath, int versionePrecedente)
        {
            try
            {
                string nomeBackup = $"{Path.GetFileNameWithoutExtension(dbPath)}_backup_v{versionePrecedente}_{DateTime.Now:yyyyMMddHHmmss}.db";
                string cartella = Path.GetDirectoryName(dbPath)!;
                File.Copy(dbPath, Path.Combine(cartella, nomeBackup), overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"Backup preventivo non riuscito per '{dbPath}': {ex.Message}");
            }
        }
    }
}
