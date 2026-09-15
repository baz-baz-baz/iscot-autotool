using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Rete di sicurezza globale contro le chiusure silenziose: intercetta ogni eccezione non gestita
    /// del processo, ne scrive il dettaglio completo in <see cref="PercorsoLog"/> e la mostra al tecnico.
    ///
    /// <para>
    /// <b>Perché esiste (Sprint 29, §6.1-tricies-semel di PROJECT_MEMORY.md).</b> Il pacchetto 2.0.0
    /// distribuito si chiudeva senza alcun messaggio: su una workstation d'officina non ci sono un
    /// debugger né una console, e il registro eventi di Windows non è qualcosa che si possa chiedere di
    /// consultare a un tecnico. Un errore che non lascia traccia non si può correggere.
    /// </para>
    ///
    /// <para>
    /// <b>I tre canali, e perché servono tutti.</b>
    /// <list type="bullet">
    /// <item><c>Application.DispatcherUnhandledException</c>: thread UI — gestori di clic, comandi, la
    /// continuazione di un <c>async void</c> dopo un <c>await</c>. Viene marcata come gestita: il
    /// tecnico legge l'errore e l'applicazione resta aperta, invece di perdere il lavoro a video.</item>
    /// <item><c>AppDomain.UnhandledException</c>: thread in background (callback di
    /// <c>System.Threading.Timer</c>, thread del <c>FileSystemWatcher</c>) e fallimenti prima che il
    /// dispatcher parta. Il processo termina comunque — il runtime non consente di impedirlo — ma non
    /// più in silenzio.</item>
    /// <item><c>TaskScheduler.UnobservedTaskException</c>: <c>Task</c> lanciati e mai attesi. In .NET
    /// moderno non chiudono il processo, ed è proprio per questo che senza log resterebbero invisibili.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// Il log vive in <c>%LOCALAPPDATA%\iscot-autotool\crash.log</c>, accanto ai dati applicativi
    /// (<see cref="AppPaths.CartellaDatiPredefinita"/>): sempre scrivibile dall'utente, mai dentro la
    /// cartella temporanea di estrazione del bundle single-file né accanto all'eseguibile.
    /// </para>
    /// </summary>
    public static class CrashReporter
    {
        private const string NomeFileLog = "crash.log";

        /// <summary>Oltre questa dimensione il log viene ruotato in <c>crash.old.log</c>.</summary>
        private const long DimensioneMassimaLog = 1024 * 1024;

        private const string TitoloAvviso = "Personal Automation Tool — Errore imprevisto";

        private static readonly object _scritturaLock = new();

        /// <summary>1 mentre un avviso non fatale è a video (guardia anti-valanga di finestre).</summary>
        private static int _avvisoInCorso;

        /// <summary>Percorso del log degli errori: <c>%LOCALAPPDATA%\iscot-autotool\crash.log</c>.</summary>
        public static string PercorsoLog =>
            PercorsoLogSostitutivo ?? Path.Combine(AppPaths.CartellaDatiPredefinita, NomeFileLog);

        /// <summary>
        /// Solo per i test: dirotta il log su un file usa-e-getta, così una suite che provoca volutamente
        /// un'anomalia non scrive nel <c>crash.log</c> reale della macchina su cui gira.
        /// </summary>
        internal static string? PercorsoLogSostitutivo { get; set; }

        /// <summary>
        /// Aggancia i tre intercettori. Da chiamare una sola volta, il prima possibile: nel costruttore
        /// di <c>App</c>, prima che WPF carichi i dizionari di risorse e avvii il dispatcher.
        /// </summary>
        public static void Attiva(Application app)
        {
            app.DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        /// <summary>
        /// Registra l'errore nel log e lo mostra al tecnico. <paramref name="fatale"/> cambia solo il
        /// testo dell'avviso ("verrà chiusa" / "resta aperta"): chiudere l'applicazione resta compito
        /// del chiamante.
        /// </summary>
        public static void Segnala(Exception eccezione, string origine, bool fatale)
        {
            string? percorso = Registra(fatale ? "ERRORE FATALE" : "ERRORE NON GESTITO", origine, eccezione);
            MostraAvviso(eccezione, percorso, fatale);
        }

        /// <summary>
        /// Registra nel log, <b>senza</b> avviso a video, un problema già gestito che però non deve
        /// passare inosservato (es. l'aggiornamento automatico che non riesce a partire). È la
        /// differenza con un <c>Debug.WriteLine</c>, che nel pacchetto distribuito non lascia traccia.
        /// </summary>
        public static void RegistraAnomalia(string contesto, Exception? eccezione = null) =>
            Registra("ANOMALIA GESTITA", contesto, eccezione);

        /// <summary>
        /// Registra una traccia diagnostica di un'operazione **riuscita**, senza avviso a video: serve
        /// quando ciò che conta non è l'errore ma poter ricostruire a posteriori *cosa* è stato scritto
        /// e *dove*. Introdotta per il Report Interventi (§6.1-tricies-septies): il difetto segnalato dal
        /// committente — "dice di aver scritto alla riga N, ma nel foglio non c'è niente" — era
        /// indiagnosticabile dal campo, perché l'unica traccia era una MessageBox che riportava un
        /// numero di riga senza dire su quale foglio né con quali valori.
        /// </summary>
        public static void RegistraDiagnostica(string contesto) =>
            Registra("DIAGNOSTICA", contesto, null);

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Gestita: l'operazione in corso è persa, l'applicazione no. Unica eccezione: un errore
            // arrivato quando non esiste ancora (o non esiste più) alcuna finestra. Proseguire lascerebbe
            // un processo vivo ma invisibile, chiudibile solo da Gestione attività.
            e.Handled = true;
            bool nessunaFinestra = Application.Current is not { } app || app.Windows.Count == 0;

            Segnala(e.Exception, "Thread UI (DispatcherUnhandledException)", fatale: nessunaFinestra);

            if (nessunaFinestra) Application.Current?.Shutdown(1);
        }

        private static void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception eccezione = e.ExceptionObject as Exception
                ?? new InvalidOperationException($"Eccezione non-.NET: {e.ExceptionObject}");

            Segnala(eccezione,
                $"Thread in background (AppDomain.UnhandledException, IsTerminating={e.IsTerminating})",
                fatale: e.IsTerminating);
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
            string? percorso = Registra("ERRORE NON GESTITO", "Task non atteso (TaskScheduler.UnobservedTaskException)", e.Exception);

            // Questo evento scatta sul thread del finalizzatore, durante una garbage collection:
            // bloccarlo con una finestra modale fermerebbe la finalizzazione dell'intero processo.
            // L'avviso viene consegnato al thread UI, se esiste ancora.
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is { HasShutdownStarted: false })
            {
                dispatcher.InvokeAsync(() => MostraAvviso(e.Exception, percorso, fatale: false));
            }
        }

        private static void MostraAvviso(Exception eccezione, string? percorsoLog, bool fatale)
        {
            // Un solo avviso non fatale alla volta: un errore che si ripete (un timer, un evento a raffica)
            // non deve seppellire il tecnico sotto una pila di finestre — le ripetizioni finiscono comunque
            // tutte nel log. Gli avvisi fatali passano sempre: sono l'ultima cosa che il processo farà.
            if (!fatale && Interlocked.CompareExchange(ref _avvisoInCorso, 1, 0) != 0) return;

            try
            {
                string testo = ComponiMessaggio(eccezione, percorsoLog, fatale);

                if (Application.Current?.Dispatcher.CheckAccess() == true)
                {
                    MessageBox.Show(testo, TitoloAvviso, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    // Fuori dal thread UI non c'è una finestra proprietaria: senza DefaultDesktopOnly
                    // l'avviso potrebbe aprirsi dietro la finestra principale massimizzata, invisibile.
                    MessageBox.Show(testo, TitoloAvviso, MessageBoxButton.OK, MessageBoxImage.Error,
                        MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            catch (Exception ex)
            {
                Registra("ANOMALIA GESTITA", "Impossibile mostrare l'avviso di errore", ex);
            }
            finally
            {
                if (!fatale) Interlocked.Exchange(ref _avvisoInCorso, 0);
            }
        }

        /// <summary>
        /// Testo dell'avviso a video: tipo e messaggio dell'eccezione, la causa più interna quando
        /// diversa (WPF avvolge spesso l'errore reale in <c>XamlParseException</c> o
        /// <c>TargetInvocationException</c>, il cui messaggio da solo non dice nulla) e dove trovare lo
        /// stack trace completo.
        /// </summary>
        internal static string ComponiMessaggio(Exception eccezione, string? percorsoLog, bool fatale)
        {
            var sb = new StringBuilder();
            sb.AppendLine(fatale
                ? "Si è verificato un errore che impedisce all'applicazione di proseguire."
                : "Si è verificato un errore imprevisto: l'operazione in corso è stata interrotta.");
            sb.AppendLine();
            sb.AppendLine($"{eccezione.GetType().Name}: {eccezione.Message}");

            Exception causa = eccezione.GetBaseException();
            if (!ReferenceEquals(causa, eccezione))
            {
                sb.AppendLine($"Causa: {causa.GetType().Name}: {causa.Message}");
            }

            sb.AppendLine();
            if (percorsoLog != null)
            {
                sb.AppendLine("Il dettaglio completo (stack trace) è stato salvato in:");
                sb.AppendLine(percorsoLog);
            }
            else
            {
                sb.AppendLine("Non è stato possibile salvare il dettaglio dell'errore su disco.");
            }

            sb.AppendLine();
            sb.Append(fatale
                ? "L'applicazione verrà chiusa."
                : "L'applicazione resta aperta, ma è consigliato riavviarla.");
            sb.Append(" Premere Ctrl+C in questa finestra per copiarne il testo.");
            return sb.ToString();
        }

        private static string? Registra(string intestazione, string origine, Exception? eccezione)
        {
            string voce;
            try
            {
                voce = FormattaVoce(intestazione, origine, eccezione, DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                voce = $"{intestazione} | {origine}{Environment.NewLine}{eccezione}{Environment.NewLine}" +
                       $"(intestazione non disponibile: {ex.Message}){Environment.NewLine}";
            }

            Debug.WriteLine(voce);

            string percorso = PercorsoLog;
            if (ScriviVoce(percorso, voce, DimensioneMassimaLog)) return percorso;

            // Ultima risorsa: la cartella temporanea resta scrivibile anche quando il profilo locale non
            // lo è (profilo temporaneo, criteri di gruppo restrittivi).
            string riserva = Path.Combine(Path.GetTempPath(), "iscot-autotool-" + NomeFileLog);
            return ScriviVoce(riserva, voce, DimensioneMassimaLog) ? riserva : null;
        }

        /// <summary>
        /// Una voce del log: intestazione con tutto ciò che serve a capire <i>dove</i> è successo senza
        /// dover chiedere nulla al tecnico (versione, eseguibile, sistema, percorsi in uso), poi
        /// <see cref="Exception.ToString"/>, che include tipo, messaggio, stack trace e l'intera catena
        /// delle eccezioni interne.
        /// </summary>
        internal static string FormattaVoce(string intestazione, string origine, Exception? eccezione, DateTimeOffset momento)
        {
            var sb = new StringBuilder();
            sb.AppendLine(new string('=', 100));
            sb.AppendLine($"{momento.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)}  |  {intestazione}");
            sb.AppendLine($"Origine      : {origine}");
            sb.AppendLine($"Versione     : {VersioneApplicazione()}");
            sb.AppendLine($"Processo     : {Environment.ProcessPath} (PID {Environment.ProcessId}, " +
                          $"{(Environment.Is64BitProcess ? 64 : 32)} bit, thread {Environment.CurrentManagedThreadId})");
            sb.AppendLine($"Sistema      : {RuntimeInformation.OSDescription} | {RuntimeInformation.FrameworkDescription}");
            sb.AppendLine($"Cartella dati: {ValoreOAssente(AppPaths.DataFolder)}");
            sb.AppendLine($"LOG & DUMP   : {ValoreOAssente(AppConfig.LogAndDumpFolder)}");
            sb.AppendLine(new string('-', 100));
            sb.AppendLine(eccezione?.ToString() ?? "(nessuna eccezione associata)");
            sb.AppendLine();
            return sb.ToString();
        }

        /// <summary>
        /// Accoda <paramref name="voce"/> al file, creando la cartella se manca e ruotando il file in
        /// <c>.old.log</c> oltre <paramref name="dimensioneMassimaByte"/>. Non lancia mai: un log che
        /// fallisce non deve diventare a sua volta la causa di un crash.
        /// </summary>
        internal static bool ScriviVoce(string percorsoFile, string voce, long dimensioneMassimaByte)
        {
            try
            {
                lock (_scritturaLock)
                {
                    string? cartella = Path.GetDirectoryName(percorsoFile);
                    if (!string.IsNullOrEmpty(cartella)) Directory.CreateDirectory(cartella);

                    var info = new FileInfo(percorsoFile);
                    if (info.Exists && info.Length > dimensioneMassimaByte)
                    {
                        File.Move(percorsoFile, Path.ChangeExtension(percorsoFile, ".old.log"), overwrite: true);
                    }

                    using var stream = new FileStream(percorsoFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    writer.Write(voce);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Scrittura del log errori in '{percorsoFile}' non riuscita: {ex.Message}");
                return false;
            }
        }

        private static string ValoreOAssente(string valore) =>
            string.IsNullOrEmpty(valore) ? "(non ancora inizializzata)" : valore;

        private static string VersioneApplicazione()
        {
            Assembly assembly = typeof(CrashReporter).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "sconosciuta";
        }
    }
}
