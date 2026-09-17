using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Verifiche;

namespace PersonalAutomationTool.Modules.Home
{
    /// <summary>Esito della verifica in sola lettura di un singolo percorso.</summary>
    public enum PathHealthStatus
    {
        /// <summary>Percorso (e, dove applicabile, file target) esistente, individuato e leggibile al 100%.</summary>
        Ok,

        /// <summary>
        /// Non bloccante: la cartella è raggiungibile ma qualcosa nel suo contenuto richiede
        /// attenzione senza essere un guasto — nessun file Excel attivo trovato, oppure il file
        /// trovato è un segnaposto cloud OneDrive non ancora scaricato in locale. In entrambi i casi
        /// il percorso di per sé è corretto: non è quindi lo stesso "ERRORE" di un percorso
        /// inesistente o inaccessibile.
        /// </summary>
        Avviso,

        /// <summary>
        /// Percorso non trovato/non raggiungibile, oppure un errore di I/O generico (percorso troppo
        /// lungo, guasto del volume, file corrotto o vuoto, …). Un solo stato "rosso" per tutti questi
        /// casi, come da specifica: non interessa distinguerli nel badge a schermo — il messaggio in
        /// <see cref="PathHealthCheckItem.Dettaglio"/> sì.
        /// </summary>
        Errore,

        /// <summary>
        /// Percorso trovato ma senza permessi di lettura sufficienti — bloccante quanto
        /// <see cref="Errore"/> (badge rosso), distinto solo per il messaggio diagnostico.
        /// </summary>
        AccessoNegato
    }

    /// <summary>
    /// Riga del dialog di verifica percorsi: dato immutabile, senza alcuna dipendenza da WPF, così
    /// resta verificabile da xUnit senza toccare <c>PresentationCore</c>. La colorazione del badge di
    /// stato vive interamente in <c>HealthCheckPathsDialog.xaml</c> (trigger su <see cref="StatoTesto"/>),
    /// non qui.
    /// </summary>
    public sealed record PathHealthCheckItem(string Funzione, string Percorso, PathHealthStatus Stato, string Dettaglio)
    {
        /// <summary>Etichetta del badge: le stesse tre diciture della specifica del committente.</summary>
        public string StatoTesto => Stato switch
        {
            PathHealthStatus.Ok => "OK",
            PathHealthStatus.Avviso => "AVVISO",
            PathHealthStatus.AccessoNegato => "ACCESSO NEGATO",
            _ => "ERRORE"
        };
    }

    /// <summary>
    /// Verifica lo stato dei percorsi Hitachi/SharePoint/OneDrive e locali usati dall'applicazione.
    ///
    /// <para>
    /// <b>Regola non negoziabile, richiesta esplicitamente dal committente: sola lettura.</b> Nessun
    /// metodo qui dentro chiama <c>Directory.CreateDirectory</c>, <c>File.Create</c> o equivalenti. Un
    /// percorso mancante viene <i>segnalato</i>, mai <i>corretto</i> — a differenza, per esempio, di
    /// <c>AppConfig.Initialize()</c>, che crea <c>LOG &amp; DUMP</c> se manca: quella è una cartella di
    /// lavoro locale dell'applicazione, questi sono percorsi di rete/SharePoint su cui l'app non ha
    /// alcuna autorità di creare struttura (lo stesso principio già scritto in §5.6 per
    /// <c>HomeViewModel.OnLogDumpRete</c>).
    /// </para>
    ///
    /// <para>
    /// <b>Legge le configurazioni reali, non una copia.</b> <see cref="EseguiControllo"/> interroga
    /// <c>HitachiPathsManager</c> e <c>VerifichePathsManager</c> — le stesse fonti già usate da EXCEL
    /// (Sposta/Riporta Report) e da VERIFICHE (Verifica Eseguita) — invece di duplicare l'elenco dei
    /// percorsi: se un percorso viene corretto in una di quelle schermate, l'health-check lo vede
    /// immediatamente, senza bisogno di sincronizzare due elenchi.
    /// </para>
    /// </summary>
    public static class PathHealthCheckService
    {
        /// <summary>
        /// Traduce un'eccezione in uno stato e in un messaggio diagnostico. Estratta come funzione
        /// pura — invece di restare inline nei blocchi <c>catch</c> — per essere verificabile da xUnit
        /// senza dover realmente negare un permesso NTFS su disco, cosa fragile e dipendente
        /// dall'ambiente in cui gira la suite.
        /// </summary>
        internal static (PathHealthStatus Stato, string Dettaglio) MappaEccezione(Exception ex) => ex switch
        {
            UnauthorizedAccessException => (PathHealthStatus.AccessoNegato, "Accesso negato: permessi di lettura insufficienti."),
            PathTooLongException => (PathHealthStatus.Errore, "Percorso troppo lungo."),
            IOException ioEx => (PathHealthStatus.Errore, $"Errore di I/O: {ioEx.Message}"),
            _ => (PathHealthStatus.Errore, $"Errore imprevisto: {ex.Message}")
        };

        /// <summary>
        /// Verifica una cartella: esistenza più una sonda di leggibilità passiva.
        /// <c>Directory.EnumerateFileSystemEntries(...).FirstOrDefault()</c> enumera al più una voce
        /// (mai l'intero contenuto) e non apre né blocca alcun file — sufficiente a far emergere un
        /// <see cref="UnauthorizedAccessException"/> se le ACL bloccano la lettura pur con la cartella
        /// esistente, come richiesto dal punto 3 della specifica.
        /// </summary>
        internal static PathHealthCheckItem CheckDirectory(string funzione, string percorso)
        {
            if (string.IsNullOrWhiteSpace(percorso))
                return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Errore, "Percorso non configurato.");

            try
            {
                if (!Directory.Exists(percorso))
                {
                    return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Errore,
                        "Percorso non trovato - verificare sincronizzazione OneDrive/SharePoint.");
                }

                _ = Directory.EnumerateFileSystemEntries(percorso).FirstOrDefault();

                return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Ok, "Cartella raggiungibile.");
            }
            catch (Exception ex)
            {
                var (stato, dettaglio) = MappaEccezione(ex);
                return new PathHealthCheckItem(funzione, percorso, stato, dettaglio);
            }
        }

        /// <summary>
        /// Verifica un file: esistenza più un'apertura passiva in sola lettura con condivisione
        /// totale (<c>FileShare.ReadWrite | FileShare.Delete</c>), che non acquisisce alcun lock e
        /// quindi non disturba un altro processo che stesse scrivendo lo stesso file in quel momento.
        /// </summary>
        internal static PathHealthCheckItem CheckFile(string funzione, string percorso)
        {
            if (string.IsNullOrWhiteSpace(percorso))
                return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Errore, "Percorso non configurato.");

            try
            {
                if (!File.Exists(percorso))
                {
                    return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Errore,
                        "File non trovato - verificare sincronizzazione OneDrive/SharePoint.");
                }

                using var stream = File.Open(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Ok, "File raggiungibile.");
            }
            catch (Exception ex)
            {
                var (stato, dettaglio) = MappaEccezione(ex);
                return new PathHealthCheckItem(funzione, percorso, stato, dettaglio);
            }
        }

        /// <summary>
        /// Verifica una cartella che dovrebbe ospitare un foglio Excel <b>attivo</b> — VERIFICHE e
        /// Report Interventi, non le cartelle di archivio (OLD), dove restare vuote è normale (una
        /// cartella "vecchi report" può legittimamente non avere ancora nulla dentro nei primi giorni
        /// di un anno, come già annotato per <see cref="EseguiControllo"/>).
        ///
        /// <para>
        /// <b>Il falso positivo che questo corregge.</b> <see cref="CheckDirectory"/> segna "OK" non
        /// appena la cartella esiste: se manca il file, se contiene solo un lucchetto Excel
        /// (<c>~$*.xlsx</c>) o se il file più recente è in realtà un segnaposto cloud OneDrive mai
        /// scaricato, il badge restava verde mentre VERIFICHE (o Report Interventi) falliva davvero.
        /// Qui la cartella non basta: serve trovare, fra i file che rispondono a
        /// <paramref name="searchPattern"/>, quello più recente — stessa selezione già usata da
        /// <c>VerificheViewModel.LoadDataForFleet</c> per VERIFICHE e da
        /// <c>ExcelViewModel</c> (pattern <c>"Report Interventi*.xls*"</c>, non ricorsivo) per Report
        /// Interventi — e sottoporlo a <see cref="CheckExcelFile"/>.
        /// </para>
        /// </summary>
        /// <param name="recursive">
        /// <see langword="true"/> per VERIFICHE (il file può stare in una sottocartella, come
        /// <c>LoadDataForFleet</c>), <see langword="false"/> per Report Interventi (cercato solo al
        /// primo livello, come <c>ExcelViewModel</c>).
        /// </param>
        internal static PathHealthCheckItem CheckExcelFolder(string funzione, string percorso, string searchPattern, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(percorso))
                return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Errore, "Percorso non configurato.");

            try
            {
                if (!Directory.Exists(percorso))
                {
                    return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Errore,
                        "Percorso inesistente - verificare sincronizzazione OneDrive/SharePoint.");
                }

                string? fileTarget = TrovaFilePiuRecente(percorso, searchPattern, recursive);

                if (fileTarget == null)
                {
                    return new PathHealthCheckItem(funzione, percorso, PathHealthStatus.Avviso,
                        "Cartella raggiungibile, ma nessun file Excel attivo trovato.");
                }

                return CheckExcelFile(funzione, fileTarget);
            }
            catch (Exception ex)
            {
                var (stato, dettaglio) = MappaEccezione(ex);
                return new PathHealthCheckItem(funzione, percorso, stato, dettaglio);
            }
        }

        /// <summary>
        /// Fra i file di <paramref name="cartella"/> che rispondono a <paramref name="searchPattern"/>,
        /// il più recente per data di ultima modifica — scartando sempre i lucchetti Excel
        /// (<c>~$*.xlsx</c>), che esistono solo mentre qualcuno ha il foglio aperto e non sono mai il
        /// file da verificare. <see langword="null"/> se nessun file risponde al pattern.
        /// </summary>
        internal static string? TrovaFilePiuRecente(string cartella, string searchPattern, bool recursive)
        {
            var opzioni = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            string? fileTarget = null;
            DateTime ultimaScrittura = DateTime.MinValue;

            foreach (var file in Directory.EnumerateFiles(cartella, searchPattern, opzioni))
            {
                if (Path.GetFileName(file).StartsWith("~$", StringComparison.Ordinal)) continue;

                var scrittura = File.GetLastWriteTime(file);
                if (fileTarget == null || scrittura > ultimaScrittura)
                {
                    fileTarget = file;
                    ultimaScrittura = scrittura;
                }
            }

            return fileTarget;
        }

        /// <summary>
        /// Verifica di basso livello su un file Excel già individuato, in tre passi: attributi cloud
        /// (OneDrive Files On-Demand, senza mai forzarne il download — il controllo degli attributi
        /// avviene <b>prima</b> di aprire lo stream, apposta), apertura reale in sola lettura, firma
        /// ZIP/OpenXML dei primi 4 byte (<c>0x50 0x4B 0x03 0x04</c>, la stessa di qualunque .xlsx: è
        /// un pacchetto ZIP). Un file bloccato da un altro processo senza condivisione, o con permessi
        /// insufficienti, arriva al chiamante come eccezione — non intercettata qui apposta, stesso
        /// schema di <see cref="CheckFile"/>, che lascia la classificazione a <see cref="MappaEccezione"/>.
        /// </summary>
        /// <summary>
        /// <c>FILE_ATTRIBUTE_RECALL_ON_OPEN</c> (0x00040000) e <c>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS</c>
        /// (0x00400000): i due flag Win32 con cui OneDrive marca un segnaposto "Files On-Demand" non
        /// ancora scaricato, a seconda della versione del client. <see cref="FileAttributes"/> non li
        /// espone come membri nominati (solo <see cref="FileAttributes.Offline"/> lo è) — da qui la
        /// necessità di questi due valori grezzi, uniti in OR bit a bit come qualunque altro flag
        /// dell'enum.
        /// </summary>
        private const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;
        private const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

        internal static PathHealthCheckItem CheckExcelFile(string funzione, string filePath)
        {
            var attributi = File.GetAttributes(filePath);
            if ((attributi & (FileAttributes.Offline | RecallOnOpen | RecallOnDataAccess)) != 0)
            {
                return new PathHealthCheckItem(funzione, filePath, PathHealthStatus.Avviso,
                    $"'{Path.GetFileName(filePath)}' è presente su OneDrive ma non scaricato in locale " +
                    "(richiede \"Conserva sempre su questo dispositivo\").");
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            byte[] header = new byte[4];
            int bytesRead = stream.Read(header, 0, 4);

            bool firmaZipValida = bytesRead == 4 &&
                header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;

            if (!firmaZipValida)
            {
                return new PathHealthCheckItem(funzione, filePath, PathHealthStatus.Errore,
                    $"'{Path.GetFileName(filePath)}' non è un file .xlsx valido (intestazione ZIP/OpenXML assente o file vuoto).");
            }

            var info = new FileInfo(filePath);
            string dettaglio = $"File attivo: '{info.Name}' — {FormattaDimensione(info.Length)}, " +
                $"modificato il {info.LastWriteTime:dd/MM/yyyy HH:mm}.";

            return new PathHealthCheckItem(funzione, filePath, PathHealthStatus.Ok, dettaglio);
        }

        private static string FormattaDimensione(long byteCount) =>
            byteCount < 1024 * 1024
                ? $"{byteCount / 1024.0:0.#} KB"
                : $"{byteCount / (1024.0 * 1024.0):0.##} MB";

        /// <summary>
        /// Esegue la scansione completa: cartelle Hitachi di EXCEL (una per treno configurato),
        /// cartelle VERIFICHE principale e OLD (una coppia per flotta), la radice locale di
        /// <c>LOG &amp; DUMP</c> e la sua controparte di rete. Pensata per girare su thread pool: ogni
        /// controllo può toccare un percorso SharePoint/OneDrive lento o disconnesso (§3, vincolo 1 —
        /// zero I/O sul dispatcher).
        /// </summary>
        public static IReadOnlyList<PathHealthCheckItem> EseguiControllo()
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var risultati = new List<PathHealthCheckItem>();

            int annoCorrente = DateTime.Now.Year;
            foreach (var cfg in HitachiPathsManager.LoadConfig())
            {
                // Cartella "attiva": deve contenere il file master, non solo esistere (stesso pattern
                // non ricorsivo "Report Interventi*.xls*" già usato da ExecuteSpostaReport/RiportaReport
                // in ExcelViewModel, riletto qui invece di indovinato).
                string percorso = HitachiPathsManager.GetHitachiDir(userProfile, cfg.Train) ?? string.Empty;
                risultati.Add(CheckExcelFolder($"Report Interventi {cfg.Train}", percorso, "Report Interventi*.xls*", recursive: false));

                // Cartella "vecchi report" dove "Sposta Report" archivia il file sostituito (ETR700 e
                // E404P la nominano con l'anno corrente: può legittimamente non esistere ancora nei
                // primi giorni di un anno nuovo, prima del primo "Sposta Report" — non è di per sé un
                // sintomo di problemi di sincronizzazione, a differenza delle altre righe).
                string? percorsoOld = HitachiPathsManager.GetReportOldFolder(userProfile, cfg.Train, annoCorrente);
                if (percorsoOld != null)
                {
                    risultati.Add(CheckDirectory($"Report Interventi {cfg.Train} (OLD)", percorsoOld));
                }
            }

            foreach (var cfg in VerifichePathsManager.LoadConfig())
            {
                var risolto = VerifichePathsManager.Risolvi(userProfile, cfg.Fleet);
                if (risolto == null) continue;

                // Idem: la cartella principale deve contenere un file "Verifiche" attivo, non solo
                // esistere — stesso pattern ricorsivo già usato da VerificheViewModel.LoadDataForFleet.
                risultati.Add(CheckExcelFolder(cfg.FilePrefix, risolto.CartellaPrincipale, "*Verifiche*.xlsx", recursive: true));
                if (risolto.CartellaOld != null)
                {
                    risultati.Add(CheckDirectory($"{cfg.FilePrefix} (OLD)", risolto.CartellaOld));
                }
            }

            risultati.Add(CheckDirectory("LOG & DUMP Radice", AppConfig.LogAndDumpFolder));
            risultati.Add(CheckDirectory("LOG & DUMP in rete", HomeViewModel.GetLogDumpReteBasePath()));

            return risultati;
        }
    }
}
