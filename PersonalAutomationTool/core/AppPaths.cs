using System;
using System.Collections.Generic;
using System.IO;

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
    /// estrazione</b> del bundle, il cui nome dipende dall'hash del file. Verificato a runtime sul
    /// pacchetto pubblicato: i due <c>.db</c> finivano in
    /// <c>%LOCALAPPDATA%\Temp\.net\PersonalAutomationTool\&lt;hash&gt;\modules\database\</c>, e lì
    /// venivano anche scritti.
    /// </para>
    ///
    /// <para>
    /// <b>Cosa comportava.</b> L'applicazione funzionava, ma perdeva lo stato in silenzio: Windows può
    /// ripulire <c>%TEMP%</c>, e soprattutto <b>a ogni aggiornamento dell'eseguibile l'hash cambia</b>,
    /// quindi cambia la cartella e i tecnici avrebbero ritrovato l'applicazione azzerata — compreso
    /// <c>destinatari.json</c>, che contiene gli indirizzi reali personalizzati a mano e la cui
    /// preziosità è già segnalata in §6.1-quaterdecies.
    /// </para>
    ///
    /// <para>
    /// <b>Soluzione.</b> Lo stato scrivibile vive in <c>%APPDATA%\PersonalAutomationTool</c>, che non
    /// dipende né da dove è installato l'eseguibile né da come è stato pubblicato. Al primo avvio
    /// <see cref="Initialize"/> vi trasferisce i file già esistenti accanto all'eseguibile: la stessa
    /// operazione copre <b>due</b> casi che sembrano diversi ma non lo sono — l'aggiornamento di una
    /// macchina che usava la versione a cartella (migrazione delle personalizzazioni) e il primo avvio
    /// del pacchetto single-file (seed dei <c>.db</c> distribuiti dentro il bundle).
    /// </para>
    /// </summary>
    public static class AppPaths
    {
        /// <summary>Nome della cartella applicativa sotto <c>%APPDATA%</c>.</summary>
        private const string NomeCartellaApplicazione = "PersonalAutomationTool";

        /// <summary>
        /// Sottocartella dei database, mantenuta <b>identica</b> a quella di installazione
        /// (<c>modules\database</c>) invece di essere semplificata: i due percorsi vengono confrontati
        /// e copiati l'uno nell'altro, e tenerli speculari rende la migrazione una copia diretta.
        /// </summary>
        private const string SottocartellaDatabase = @"modules\database";

        /// <summary>
        /// File di stato trasferiti dalla cartella di installazione a quella dati al primo avvio.
        /// Percorsi relativi, così valgono per entrambe le cartelle.
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
        /// Cartella dei dati scrivibili: <c>%APPDATA%\PersonalAutomationTool</c>.
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
        /// Percorso completo di un file di configurazione dentro <see cref="DataFolder"/>.
        /// </summary>
        public static string DataFile(string nomeFile) => Path.Combine(DataFolder, nomeFile);

        /// <summary>
        /// Percorso completo di un database dentro <see cref="DatabaseFolder"/>.
        /// </summary>
        public static string DatabaseFile(string nomeFile) => Path.Combine(DatabaseFolder, nomeFile);

        /// <summary>
        /// Prepara la cartella dati e vi trasferisce i file di stato già esistenti nella cartella di
        /// installazione. Da chiamare una sola volta all'avvio, <b>prima</b> di qualunque accesso a
        /// configurazioni o database.
        /// </summary>
        public static void Initialize()
        {
            DataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                NomeCartellaApplicazione);

            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(DatabaseFolder);

            TrasferisciFileMancanti(InstallFolder, DataFolder, FileDiStato);
        }

        /// <summary>
        /// Copia da <paramref name="origine"/> a <paramref name="destinazione"/> i soli file che nella
        /// destinazione <b>non esistono ancora</b>, e restituisce quanti ne ha copiati.
        ///
        /// <para>
        /// <b>"Solo quelli mancanti" è la regola che rende l'operazione sicura da ripetere</b>: gira a
        /// ogni avvio e non deve mai sovrascrivere il lavoro dell'utente. Se in <c>%APPDATA%</c> c'è già
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

        /// <summary>I file di stato gestiti, esposti per i test.</summary>
        internal static IReadOnlyList<string> FileDiStatoGestiti => FileDiStato;
    }
}
