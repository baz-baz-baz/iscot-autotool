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
        /// Sposta un file **senza che esista un istante in cui non si trova da nessuna parte**: copia,
        /// verifica che la copia sia arrivata integra, e solo allora elimina l'origine.
        ///
        /// <para>
        /// <b>Perché non <see cref="File.Move(string, string, bool)"/> (bug ETR500, §6.1-tricies-septies).</b>
        /// Con <c>overwrite: true</c>, <c>File.Move</c> passa da <c>MoveFileEx</c> con
        /// <c>MOVEFILE_REPLACE_EXISTING</c>: l'origine viene rimossa come parte dell'operazione. Fra una
        /// cartella locale e una sincronizzata SharePoint quell'operazione può fallire a metà — il demone
        /// di sincronizzazione aggancia la destinazione, il placeholder viene rifiutato, la rete cade — e
        /// l'esito osservato sul campo è il peggiore possibile: il file non è più nell'origine e non è
        /// ancora nella destinazione. Per il tecnico, "il report è sparito".
        /// </para>
        ///
        /// <para>
        /// L'ordine qui sotto rende quell'esito impossibile: nel caso peggiore il file resta in
        /// <b>entrambi</b> i posti — uno stato ridondante, non una perdita.
        /// </para>
        /// </summary>
        /// <exception cref="IOException">
        /// La copia non è verificabile (assente o di dimensione diversa dall'originale), oppure
        /// l'eliminazione dell'origine non riesce. In entrambi i casi il file esiste ancora: il messaggio
        /// dice dove, perché è l'informazione che serve al tecnico per non ripetere il lavoro.
        /// </exception>
        public static void MoveSafely(
            string sourcePath,
            string destinationPath,
            int maxAttempts = DefaultMaxAttempts,
            int initialDelayMs = DefaultInitialDelayMs,
            Action<int, int>? onRetry = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(sourcePath);
            ArgumentException.ThrowIfNullOrEmpty(destinationPath);

            long expectedLength = new FileInfo(sourcePath).Length;

            Execute(() => File.Copy(sourcePath, destinationPath, overwrite: true), maxAttempts, initialDelayMs, onRetry);
            VerifyCopy(sourcePath, destinationPath, expectedLength);

            try
            {
                Execute(() => File.Delete(sourcePath), maxAttempts, initialDelayMs, onRetry);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"Il file è stato copiato correttamente in «{destinationPath}», ma non è stato possibile " +
                    $"rimuovere la copia di partenza «{sourcePath}»: eliminarla a mano per evitare di ritrovarsela " +
                    "come report ancora da riportare. Nessun dato è andato perso.", ex);
            }
        }

        /// <summary>Variante asincrona di <see cref="MoveSafely"/>, per i comandi della UI.</summary>
        public static async Task MoveSafelyAsync(
            string sourcePath,
            string destinationPath,
            int maxAttempts = DefaultMaxAttempts,
            int initialDelayMs = DefaultInitialDelayMs,
            Action<int, int>? onRetry = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(sourcePath);
            ArgumentException.ThrowIfNullOrEmpty(destinationPath);

            long expectedLength = new FileInfo(sourcePath).Length;

            await ExecuteAsync(() => File.Copy(sourcePath, destinationPath, overwrite: true), maxAttempts, initialDelayMs, onRetry)
                .ConfigureAwait(false);
            VerifyCopy(sourcePath, destinationPath, expectedLength);

            try
            {
                await ExecuteAsync(() => File.Delete(sourcePath), maxAttempts, initialDelayMs, onRetry).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"Il file è stato copiato correttamente in «{destinationPath}», ma non è stato possibile " +
                    $"rimuovere la copia di partenza «{sourcePath}»: eliminarla a mano per evitare di ritrovarsela " +
                    "come report ancora da riportare. Nessun dato è andato perso.", ex);
            }
        }

        /// <summary>
        /// La copia esiste ed è grande quanto l'originale. Il confronto è sulla dimensione e non su un
        /// hash: su un report da 60 MB l'hash costerebbe una rilettura completa di entrambi i file a ogni
        /// spostamento, mentre una copia troncata — l'unico esito realistico di una copia interrotta —
        /// si riconosce già dalla dimensione.
        /// </summary>
        private static void VerifyCopy(string sourcePath, string destinationPath, long expectedLength)
        {
            var destination = new FileInfo(destinationPath);

            if (!destination.Exists)
            {
                throw new IOException(
                    $"La copia di «{sourcePath}» verso «{destinationPath}» non è stata verificata: il file di " +
                    "destinazione non esiste. L'originale non è stato toccato.");
            }

            if (destination.Length != expectedLength)
            {
                throw new IOException(
                    $"La copia di «{sourcePath}» verso «{destinationPath}» risulta incompleta " +
                    $"({destination.Length} byte invece di {expectedLength}). L'originale non è stato toccato.");
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
