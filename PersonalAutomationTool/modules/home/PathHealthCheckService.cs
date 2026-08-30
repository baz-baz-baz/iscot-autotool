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
        /// <summary>Percorso esistente e leggibile.</summary>
        Ok,

        /// <summary>
        /// Percorso non trovato/non raggiungibile, oppure un errore di I/O generico (percorso troppo
        /// lungo, guasto del volume, …). Un solo stato "rosso" per entrambi, come da specifica: non
        /// interessa distinguerli nel badge a schermo — il messaggio in <see cref="PathHealthCheckItem.Dettaglio"/> sì.
        /// </summary>
        Errore,

        /// <summary>Percorso trovato ma senza permessi di lettura sufficienti.</summary>
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
                string percorso = HitachiPathsManager.GetHitachiDir(userProfile, cfg.Train) ?? string.Empty;
                risultati.Add(CheckDirectory($"Report Interventi {cfg.Train}", percorso));

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

                risultati.Add(CheckDirectory(cfg.FilePrefix, risolto.CartellaPrincipale));
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
