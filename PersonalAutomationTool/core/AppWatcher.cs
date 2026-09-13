using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Aggiornamento in tempo reale dei moduli che mostrano il contenuto di <c>LOG &amp; DUMP</c> (HOME,
    /// CARTELLE, PDF, EXCEL — §2.5 di PROJECT_MEMORY.md): un solo <see cref="FileSystemWatcher"/>
    /// ricorsivo, eventi raggruppati da un <see cref="Debouncer"/>, notifica consegnata sul thread UI.
    ///
    /// <para>
    /// <b>Sprint 29 (§6.1-tricies-semel): perché nella 2.0.0 l'aggiornamento automatico non funzionava.</b>
    /// <c>Initialize</c> veniva chiamato dal costruttore di MainWindow, che nel pacchetto distribuito
    /// veniva costruita <b>prima</b> di <c>AppConfig.Initialize()</c> (vedi <c>App.OnStartup</c>). La
    /// cartella risultava quindi una stringa vuota, <c>Directory.CreateDirectory("")</c> lanciava, e il
    /// <c>catch</c> scriveva solo su <c>Debug</c>: nessun watcher, per tutta la sessione, senza alcun
    /// segno visibile. Da qui le tre regole di questa versione: una cartella vuota è un errore di
    /// programmazione e fa fallire subito l'avvio; ogni altro problema del watcher finisce nel log degli
    /// errori (<see cref="CrashReporter.RegistraAnomalia"/>); un watcher in errore si ricrea da solo.
    /// </para>
    /// </summary>
    public static class AppWatcher
    {
        /// <summary>
        /// Finestra di raggruppamento degli eventi (erano 300 ms). Dà a Excel, OneDrive e SharePoint il
        /// tempo di completare la scrittura — tipicamente file temporaneo, rinomina, rimozione — prima
        /// che i moduli rileggano la cartella.
        /// </summary>
        private static readonly TimeSpan RitardoDebounce = TimeSpan.FromMilliseconds(500);

        /// <summary>Attesa prima di ricreare un watcher andato in errore.</summary>
        private static readonly TimeSpan RitardoRiavvio = TimeSpan.FromSeconds(2);

        /// <summary>
        /// 64 KB, il massimo consentito da Windows (il default è 8 KB): una raffica di eventi — la copia
        /// di una cartella di log con migliaia di file — oltre il buffer viene persa (§6.5).
        /// </summary>
        private const int DimensioneBufferEventi = 64 * 1024;

        private static readonly object _lock = new();
        private static FileSystemWatcher? _watcher;
        private static Debouncer? _debouncer;
        private static string? _cartella;

        public static event Action? OnLogDumpFolderChanged;

        /// <summary>
        /// Consegna della notifica al thread UI. Sostituibile solo dai test, che non hanno un dispatcher
        /// WPF: in produzione è sempre <see cref="Dispatcher.InvokeAsync(Action)"/>.
        /// </summary>
        internal static Action<Action> EseguiSuThreadUi { get; set; } = EseguiSuDispatcherWpf;

        /// <summary>Avvia la sorveglianza di <see cref="AppConfig.LogAndDumpFolder"/>.</summary>
        /// <exception cref="InvalidOperationException">Se <c>AppConfig.Initialize()</c> non è ancora stato chiamato.</exception>
        public static void Initialize() => Initialize(AppConfig.LogAndDumpFolder);

        internal static void Initialize(string cartella)
        {
            if (string.IsNullOrWhiteSpace(cartella))
            {
                // Esattamente il difetto della 2.0.0: fallire qui, rumorosamente, invece di spegnere
                // l'aggiornamento automatico in silenzio.
                throw new InvalidOperationException(
                    "AppWatcher: la cartella LOG & DUMP non è ancora stata risolta. " +
                    "AppConfig.Initialize() deve essere chiamato prima (PROJECT_MEMORY.md §5.8).");
            }

            lock (_lock)
            {
                FermaWatcher();
                _cartella = cartella;
                _debouncer ??= new Debouncer(RitardoDebounce, NotificaSottoscrittori,
                    ex => CrashReporter.RegistraAnomalia("AppWatcher: notifica di aggiornamento non riuscita", ex));
                AvviaWatcher();
            }
        }

        /// <summary>Ferma la sorveglianza e rilascia watcher e timer. Idempotente.</summary>
        public static void Stop()
        {
            lock (_lock)
            {
                FermaWatcher();
                _debouncer?.Dispose();
                _debouncer = null;
                _cartella = null;
            }
        }

        /// <summary>Da chiamare sotto <see cref="_lock"/>.</summary>
        private static void AvviaWatcher()
        {
            if (_cartella == null) return;

            try
            {
                if (!Directory.Exists(_cartella))
                {
                    Directory.CreateDirectory(_cartella);
                }

                var watcher = new FileSystemWatcher(_cartella)
                {
                    // Solo nomi di file e cartelle: sono gli unici filtri che producono Created, Deleted e
                    // Renamed, i soli eventi sottoscritti. LastWrite e CreationTime (presenti fino alla
                    // 2.0.0) generavano record "Changed" mai letti, che occupavano comunque il buffer
                    // interno e ne anticipavano l'esaurimento durante la copia dei log.
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    IncludeSubdirectories = true,
                    InternalBufferSize = DimensioneBufferEventi
                };

                watcher.Created += OnEvento;
                watcher.Deleted += OnEvento;
                watcher.Renamed += OnEvento;
                watcher.Error += OnErrore;
                watcher.EnableRaisingEvents = true;

                _watcher = watcher;
            }
            catch (Exception ex)
            {
                CrashReporter.RegistraAnomalia(
                    $"AppWatcher: impossibile sorvegliare '{_cartella}', aggiornamento automatico non attivo", ex);
            }
        }

        /// <summary>Da chiamare sotto <see cref="_lock"/>.</summary>
        private static void FermaWatcher()
        {
            if (_watcher == null) return;

            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppWatcher: rilascio del watcher non riuscito: {ex.Message}");
            }
            finally
            {
                _watcher = null;
            }
        }

        private static void OnEvento(object sender, FileSystemEventArgs e) => _debouncer?.Segnala();

        private static void OnErrore(object sender, ErrorEventArgs e)
        {
            // Due cause tipiche: buffer interno esaurito da una raffica (InternalBufferOverflowException)
            // o cartella non più raggiungibile (OneDrive in pausa, Desktop reindirizzato). In entrambi i
            // casi degli eventi sono andati persi e il watcher può aver smesso di notificare: si
            // ricaricano comunque i moduli e si ricrea il watcher.
            CrashReporter.RegistraAnomalia("AppWatcher: errore del FileSystemWatcher, riavvio in corso", e.GetException());
            _debouncer?.Segnala();
            _ = RiavviaAsync(sender as FileSystemWatcher);
        }

        private static async Task RiavviaAsync(FileSystemWatcher? watcherInErrore)
        {
            await Task.Delay(RitardoRiavvio).ConfigureAwait(false);

            lock (_lock)
            {
                // Nel frattempo Stop() o una nuova Initialize() hanno già sostituito il watcher.
                if (_cartella == null || !ReferenceEquals(_watcher, watcherInErrore)) return;

                FermaWatcher();
                AvviaWatcher();
            }
        }

        private static void NotificaSottoscrittori() => EseguiSuThreadUi(InvocaSottoscrittori);

        private static void InvocaSottoscrittori()
        {
            Action? gestori = OnLogDumpFolderChanged;
            if (gestori == null) return;

            // Uno per uno, ciascuno isolato: un'eccezione nel primo sottoscrittore impediva
            // l'aggiornamento di tutti quelli successivi e, arrivando al dispatcher, chiudeva l'app.
            foreach (Action gestore in gestori.GetInvocationList())
            {
                try
                {
                    gestore();
                }
                catch (Exception ex)
                {
                    CrashReporter.RegistraAnomalia(
                        $"AppWatcher: aggiornamento automatico non riuscito in {gestore.Method.DeclaringType?.Name}.{gestore.Method.Name}", ex);
                }
            }
        }

        private static void EseguiSuDispatcherWpf(Action azione)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted) return;

            dispatcher.InvokeAsync(azione);
        }
    }
}
