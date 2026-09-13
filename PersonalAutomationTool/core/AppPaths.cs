using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Risolve dove vivono i file <b>scrivibili</b> dell'applicazione: configurazioni JSON e database
    /// SQLite locali.
    ///
    /// <para>
    /// <b>Perché esiste, e perché è nato insieme alla distribuzione stand-alone.</b> Fino allo Sprint 16
    /// otto punti indipendenti del codice componevano il percorso a mano, ciascuno con il proprio
    /// <c>Path.Combine(AppDomain.CurrentDomain.BaseDirectory, …)</c>. Finché l'applicazione girava da
    /// <c>dotnet run</c> o da una cartella di output funzionava, perché lì <c>BaseDirectory</c> è la
    /// cartella dell'eseguibile. Pubblicando in <b>single-file</b> (§ "Profilo di distribuzione" nel
    /// <c>.csproj</c>) non è più vero: <c>BaseDirectory</c> diventa la <b>cartella temporanea di
    /// estrazione</b> del bundle, il cui nome dipende dall'hash del file. A ogni aggiornamento
    /// dell'eseguibile l'hash cambia, quindi cambia la cartella, e i tecnici avrebbero ritrovato
    /// l'applicazione azzerata — compreso <c>destinatari.json</c>, con gli indirizzi reali compilati a mano.
    /// </para>
    ///
    /// <para>
    /// <b>Sprint 29: da <c>%APPDATA%\PersonalAutomationTool</c> a <c>%LOCALAPPDATA%\iscot-autotool</c>.</b>
    /// Due motivi. Il primo è tecnico: <c>%APPDATA%</c> è il profilo <i>roaming</i>, che nei domini
    /// aziendali viene spesso reindirizzato su una condivisione di rete o sincronizzato al logon — il
    /// posto sbagliato per un database SQLite, il cui locking su file non è affidabile in rete. Il
    /// secondo è la richiesta esplicita del committente: dati e log degli errori
    /// (<see cref="CrashReporter"/>) nella stessa cartella, sotto il nome del progetto. La 2.0.0 aveva
    /// scartato lo spostamento perché avrebbe orfanato lo stato già esistente (§6.1-tricies di
    /// PROJECT_MEMORY.md): qui non succede, perché <see cref="Initialize"/> migra <b>per prima</b> la
    /// cartella della 2.0.0 (<see cref="LegacyDataFolder"/>), che non viene né modificata né cancellata.
    /// </para>
    /// </summary>
    public static class AppPaths
    {
        /// <summary>Nome della cartella applicativa sotto <c>%LOCALAPPDATA%</c>.</summary>
        private const string NomeCartellaApplicazione = "iscot-autotool";

        /// <summary>Nome della cartella dati usata fino alla 2.0.0, sotto <c>%APPDATA%</c> (roaming).</summary>
        private const string NomeCartellaLegacy = "PersonalAutomationTool";

        /// <summary>
        /// Sottocartella dei database, mantenuta <b>identica</b> a quella di installazione
        /// (<c>modules\database</c>) invece di essere semplificata: i percorsi vengono confrontati e
        /// copiati l'uno nell'altro, e tenerli speculari rende la migrazione una copia diretta.
        /// </summary>
        private const string SottocartellaDatabase = @"modules\database";

        /// <summary>
        /// File di stato trasferiti nella cartella dati al primo avvio. Percorsi relativi, così valgono
        /// per tutte le cartelle di origine.
        /// </summary>
        private static readonly string[] FileDiStato =
        [
            "destinatari.json",
            "shortcuts.json",
            "hitachi_paths.json",
            "verifiche_paths.json",
            @"modules\database\train_software.db",
            @"modules\database\emails.db"
        ];

        /// <summary>
        /// Database seed incorporati nell'assembly: percorso relativo nella cartella dati → nome logico
        /// della risorsa (<c>LogicalName</c> nel <c>.csproj</c>, da tenere allineato).
        /// </summary>
        private static readonly (string Relativo, string Risorsa)[] SeedIncorporati =
        [
            (@"modules\database\train_software.db", "Seed.train_software.db"),
            (@"modules\database\emails.db", "Seed.emails.db")
        ];

        /// <summary>
        /// Cartella dei dati scrivibili: <c>%LOCALAPPDATA%\iscot-autotool</c>.
        /// Valorizzata da <see cref="Initialize"/>.
        /// </summary>
        public static string DataFolder { get; private set; } = string.Empty;

        /// <summary>Cartella dei database SQLite locali, dentro <see cref="DataFolder"/>.</summary>
        public static string DatabaseFolder => Path.Combine(DataFolder, SottocartellaDatabase);

        /// <summary>
        /// Cartella da cui l'applicazione è stata avviata. In single-file è la cartella temporanea di
        /// estrazione del bundle: va bene per <b>leggere</b> i file distribuiti con l'applicazione,
        /// mai per scrivere.
        /// </summary>
        public static string InstallFolder => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Dove <see cref="Initialize"/> colloca la cartella dati. Calcolata senza bisogno di
        /// <see cref="Initialize"/>: la usa anche <see cref="CrashReporter"/>, che deve poter scrivere il
        /// log proprio quando l'avvio fallisce prima di arrivarci.
        /// </summary>
        public static string CartellaDatiPredefinita =>
            Path.Combine(CartellaSpeciale(Environment.SpecialFolder.LocalApplicationData), NomeCartellaApplicazione);

        /// <summary>
        /// Cartella dati della 2.0.0 (<c>%APPDATA%\PersonalAutomationTool</c>): sola origine di
        /// migrazione, mai scritta.
        /// </summary>
        public static string LegacyDataFolder =>
            Path.Combine(CartellaSpeciale(Environment.SpecialFolder.ApplicationData), NomeCartellaLegacy);

        /// <summary>
        /// Percorso completo di un file di configurazione dentro <see cref="DataFolder"/>.
        /// </summary>
        public static string DataFile(string nomeFile) => Path.Combine(DataFolder, nomeFile);

        /// <summary>
        /// Percorso completo di un database dentro <see cref="DatabaseFolder"/>.
        /// </summary>
        public static string DatabaseFile(string nomeFile) => Path.Combine(DatabaseFolder, nomeFile);

        /// <summary>
        /// Prepara la cartella dati e vi trasferisce i file di stato mancanti. Da chiamare una sola volta
        /// all'avvio, <b>prima</b> di qualunque accesso a configurazioni o database.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Se <c>%LOCALAPPDATA%</c> non è determinabile: proseguire significherebbe scrivere su un
        /// percorso relativo alla cartella corrente, cioè accanto all'eseguibile.
        /// </exception>
        public static void Initialize()
        {
            string cartellaDati = CartellaDatiPredefinita;
            if (!Path.IsPathFullyQualified(cartellaDati))
            {
                throw new InvalidOperationException(
                    $"Impossibile determinare la cartella %LOCALAPPDATA% dell'utente: la cartella dati risulterebbe '{cartellaDati}'.");
            }

            DataFolder = cartellaDati;
            Prepara(cartellaDati, LegacyDataFolder, InstallFolder, typeof(AppPaths).Assembly);
        }

        /// <summary>
        /// Crea <paramref name="cartellaDati"/> e la completa da tre origini, in ordine di precedenza.
        /// Ogni passo copia <b>solo ciò che manca ancora</b>, quindi la prima origine che possiede un file
        /// vince sulle successive e nulla di già presente viene mai sovrascritto:
        /// <list type="number">
        /// <item><paramref name="cartellaLegacy"/> — lo stato reale della 2.0.0 (database modificati dai
        /// tecnici, <c>destinatari.json</c> curato a mano);</item>
        /// <item><paramref name="cartellaInstallazione"/> — personalizzazioni di una versione "a cartella"
        /// pre-Sprint 16, oppure il contenuto estratto del bundle single-file;</item>
        /// <item>i seed incorporati in <paramref name="assemblySeed"/> — l'ultima rete: non dipendono né da
        /// dove né da come è stato estratto l'eseguibile.</item>
        /// </list>
        /// </summary>
        internal static void Prepara(string cartellaDati, string cartellaLegacy, string cartellaInstallazione, Assembly assemblySeed)
        {
            Directory.CreateDirectory(cartellaDati);
            Directory.CreateDirectory(Path.Combine(cartellaDati, SottocartellaDatabase));

            foreach (string origine in new[] { cartellaLegacy, cartellaInstallazione })
            {
                // Un'origine relativa (cartella speciale di Windows non risolvibile) verrebbe cercata nella
                // cartella corrente, dove non c'è nulla da migrare.
                if (Path.IsPathFullyQualified(origine))
                {
                    TrasferisciFileMancanti(origine, cartellaDati, FileDiStato);
                }
            }

            EstraiSeedMancanti(assemblySeed, cartellaDati, SeedIncorporati);
        }

        /// <summary>
        /// Copia da <paramref name="origine"/> a <paramref name="destinazione"/> i soli file che nella
        /// destinazione <b>non esistono ancora</b>, e restituisce quanti ne ha copiati.
        ///
        /// <para>
        /// <b>"Solo quelli mancanti" è la regola che rende l'operazione sicura da ripetere</b>: gira a
        /// ogni avvio e non deve mai sovrascrivere il lavoro dell'utente. Se nella cartella dati c'è già
        /// un <c>destinatari.json</c> curato a mano, un aggiornamento dell'applicazione che ne porta uno
        /// di default non lo tocca. Il rovescio consapevole della medaglia: un <c>train_software.db</c>
        /// aggiornato in una nuova release <b>non</b> rimpiazza quello già in uso, perché quel file
        /// contiene anche dati dell'utente (<c>renamer_log</c>, modifiche da DatabaseView) e non è
        /// distinguibile dal seed distribuito. Un eventuale aggiornamento dell'anagrafica flotte va
        /// quindi fatto dalla schermata DATABASE, non sostituendo il file.
        /// </para>
        ///
        /// <para>
        /// Un singolo file che non si riesce a copiare non interrompe gli altri né l'avvio: si riparte
        /// dal default, che è sempre rigenerabile, invece di impedire l'accesso all'applicazione.
        /// </para>
        /// </summary>
        internal static int TrasferisciFileMancanti(string origine, string destinazione, IEnumerable<string> fileRelativi)
        {
            int copiati = 0;

            foreach (string relativo in fileRelativi)
            {
                try
                {
                    string sorgente = Path.Combine(origine, relativo);
                    string arrivo = Path.Combine(destinazione, relativo);

                    if (!File.Exists(sorgente) || File.Exists(arrivo)) continue;

                    string? cartellaArrivo = Path.GetDirectoryName(arrivo);
                    if (!string.IsNullOrEmpty(cartellaArrivo)) Directory.CreateDirectory(cartellaArrivo);

                    File.Copy(sorgente, arrivo);
                    copiati++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Trasferimento di '{relativo}' non riuscito: {ex.Message}");
                }
            }

            return copiati;
        }

        /// <summary>
        /// Scrive in <paramref name="destinazione"/> i database seed incorporati nell'assembly che non vi
        /// esistono ancora, e restituisce quanti ne ha scritti. Stessa regola di
        /// <see cref="TrasferisciFileMancanti"/>: un file esistente non viene mai toccato.
        ///
        /// <para>
        /// La scrittura passa da un file temporaneo rinominato solo a copia completata: un avvio
        /// interrotto a metà non può lasciare un <c>.db</c> troncato, che al giro successivo
        /// risulterebbe "già presente" e non verrebbe più ripristinato.
        /// </para>
        /// </summary>
        internal static int EstraiSeedMancanti(Assembly assembly, string destinazione, IEnumerable<(string Relativo, string Risorsa)> seed)
        {
            int estratti = 0;

            foreach (var (relativo, risorsa) in seed)
            {
                string arrivo = Path.Combine(destinazione, relativo);
                string temporaneo = arrivo + ".seed-tmp";

                try
                {
                    if (File.Exists(arrivo)) continue;

                    using Stream? contenuto = assembly.GetManifestResourceStream(risorsa);
                    if (contenuto == null) continue;

                    string? cartellaArrivo = Path.GetDirectoryName(arrivo);
                    if (!string.IsNullOrEmpty(cartellaArrivo)) Directory.CreateDirectory(cartellaArrivo);

                    using (var file = new FileStream(temporaneo, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        contenuto.CopyTo(file);
                    }

                    File.Move(temporaneo, arrivo);
                    estratti++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Estrazione del seed '{risorsa}' non riuscita: {ex.Message}");
                    try { if (File.Exists(temporaneo)) File.Delete(temporaneo); } catch { /* best-effort */ }
                }
            }

            return estratti;
        }

        private static string CartellaSpeciale(Environment.SpecialFolder cartella) =>
            // DoNotVerify: senza, GetFolderPath restituisce una stringa VUOTA quando la cartella non esiste
            // ancora fisicamente, e Path.Combine("", …) diventa un percorso relativo.
            Environment.GetFolderPath(cartella, Environment.SpecialFolderOption.DoNotVerify);

        /// <summary>I file di stato gestiti, esposti per i test.</summary>
        internal static IReadOnlyList<string> FileDiStatoGestiti => FileDiStato;

        /// <summary>I seed incorporati gestiti, esposti per i test.</summary>
        internal static IReadOnlyList<(string Relativo, string Risorsa)> SeedIncorporatiGestiti => SeedIncorporati;
    }
}
