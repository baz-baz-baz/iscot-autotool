using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Riprova un'operazione su file quando fallisce per una **violazione di condivisione**, cioè
    /// perché un altro processo tiene il file agganciato in quell'istante.
    ///
    /// <para>
    /// <b>Perché serve su questa applicazione.</b> I report vivono fra il Desktop e cartelle
    /// sincronizzate OneDrive/SharePoint, e su un file appena scritto possono insistere
    /// contemporaneamente: il client di sincronizzazione che lo sta caricando, l'indicizzatore di
    /// Windows, un antivirus, o un processo EXCEL.EXE non ancora terminato. Sono agganci di durata
    /// breve — centinaia di millisecondi — quindi un singolo tentativo fallisce mentre un secondo
    /// tentativo poco dopo riesce. È esattamente il profilo di un errore <i>intermittente</i>.
    /// </para>
    ///
    /// <para>
    /// <b>Riprova solo ciò che ha senso riprovare.</b> Vengono ritentate unicamente le violazioni di
    /// condivisione e di blocco (codici Win32 32 e 33). Un file mancante, un percorso inesistente o
    /// un accesso negato per permessi non migliorano aspettando: rilanciano subito, senza far
    /// perdere secondi all'utente davanti a un errore che resterebbe comunque tale.
    /// </para>
    /// </summary>
    public static class FileOperationRetry
    {
        /// <summary>Codice Win32 32 (<c>ERROR_SHARING_VIOLATION</c>) mappato in HRESULT.</summary>
        internal const int HResultSharingViolation = unchecked((int)0x80070020);

        /// <summary>Codice Win32 33 (<c>ERROR_LOCK_VIOLATION</c>) mappato in HRESULT.</summary>
        internal const int HResultLockViolation = unchecked((int)0x80070021);

        /// <summary>Numero massimo di tentativi complessivi (il primo più le riprove).</summary>
        public const int DefaultMaxAttempts = 5;

        /// <summary>Attesa prima della prima riprova; raddoppia a ogni tentativo successivo.</summary>
        public const int DefaultInitialDelayMs = 200;

        /// <summary>Vero se l'eccezione indica un file temporaneamente agganciato da un altro processo.</summary>
        public static bool IsSharingViolation(Exception exception) =>
            exception is IOException && exception.HResult is HResultSharingViolation or HResultLockViolation;

        /// <summary>
        /// Esegue <paramref name="operation"/> riprovando con backoff esponenziale finché incontra
        /// violazioni di condivisione. L'ultimo tentativo lascia propagare l'eccezione originale, con
        /// il suo messaggio di sistema intatto, così il chiamante può mostrarla nella diagnostica.
        /// </summary>
        /// <param name="operation">L'operazione da eseguire (spostamento, copia, apertura...).</param>
        /// <param name="maxAttempts">Tentativi complessivi. Il valore predefinito copre circa 3 secondi di attesa cumulativa.</param>
        /// <param name="initialDelayMs">Attesa prima della prima riprova, in millisecondi.</param>
        /// <param name="onRetry">Notifica opzionale prima di ogni attesa: riceve il numero di tentativo già fallito e i millisecondi di attesa. Usata per aggiornare il messaggio a schermo.</param>
        public static async Task ExecuteAsync(
            Action operation,
            int maxAttempts = DefaultMaxAttempts,
            int initialDelayMs = DefaultInitialDelayMs,
            Action<int, int>? onRetry = null)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            int delay = initialDelayMs;

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    operation();
                    return;
                }
                catch (Exception ex) when (IsSharingViolation(ex) && attempt < maxAttempts)
                {
                    onRetry?.Invoke(attempt, delay);
                    await Task.Delay(delay).ConfigureAwait(false);
                    delay *= 2;
                }
            }
        }

        /// <summary>
        /// Variante sincrona, per i punti che non possono attendere in modo asincrono (per esempio
        /// dentro un <c>Task.Run</c> già in esecuzione su thread pool).
        /// </summary>
        public static void Execute(
            Action operation,
            int maxAttempts = DefaultMaxAttempts,
            int initialDelayMs = DefaultInitialDelayMs,
            Action<int, int>? onRetry = null)
        {
            ArgumentNullException.ThrowIfNull(operation);
            if (maxAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maxAttempts));

            int delay = initialDelayMs;

            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    operation();
                    return;
                }
                catch (Exception ex) when (IsSharingViolation(ex) && attempt < maxAttempts)
                {
                    onRetry?.Invoke(attempt, delay);
                    Thread.Sleep(delay);
                    delay *= 2;
                }
            }
        }

        /// <summary>
        /// Messaggio diagnostico per l'utente quando tutte le riprove sono fallite: dice quale file è
        /// bloccato e cosa può fare, invece di riportare solo il messaggio di sistema.
        /// </summary>
        public static string BuildDiagnosticMessage(string filePath, Exception exception)
        {
            string fileName = Path.GetFileName(filePath);
            return $"Impossibile spostare il file «{fileName}»: risulta ancora in uso da un altro programma.\n\n" +
                   "Cause più frequenti:\n" +
                   "• il file è aperto in Excel (chiudere Excel e riprovare);\n" +
                   "• OneDrive/SharePoint lo sta sincronizzando in questo momento (attendere che l'icona di sincronizzazione diventi verde);\n" +
                   "• un antivirus o l'indicizzatore di Windows lo sta analizzando.\n\n" +
                   $"Percorso: {filePath}\n" +
                   $"Dettaglio tecnico: {exception.Message}";
        }
    }
}
