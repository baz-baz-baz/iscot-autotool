using System;
using System.Threading.Tasks;
using System.Windows;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Tenuto vivo per tutta la durata dell'applicazione: il mutex di sistema che rappresenta si
    /// libera se l'oggetto viene raccolto dal garbage collector, quindi deve restare referenziato
    /// come campo, non come variabile locale di <see cref="OnStartup"/>.
    /// </summary>
    private SingleInstanceGuard? _singleInstanceGuard;

    public App()
    {
        // Prima istruzione eseguita dall'applicazione: il Main generato da WPF costruisce App prima di
        // InitializeComponent() (i dizionari MaterialDesign di App.xaml) e di Run(). Agganciare qui la
        // rete di sicurezza copre anche un fallimento in quelle fasi, che altrimenti chiuderebbe il
        // processo senza lasciare traccia (§6.1-tricies-semel di PROJECT_MEMORY.md).
        CrashReporter.Attiva(this);
    }

    /// <summary>
    /// Sequenza di avvio: istanza singola → cartelle dati → SQLite nativo → auto-update → MainWindow.
    ///
    /// <para>
    /// <b>Perché App.xaml non ha più <c>StartupUri</c> (Sprint 29, il difetto della 2.0.0).</b> WPF apre
    /// la finestra di <c>StartupUri</c> <b>quando <c>OnStartup</c> ritorna</b>, non dentro
    /// <c>base.OnStartup</c> come assumeva la versione precedente di questo file. Un
    /// <c>async void OnStartup</c> "ritorna" al primo <c>await</c> che non si completa subito: con
    /// l'eseguibile pubblicato (nome file reale → chiamata a GitHub dell'auto-update) MainWindow veniva
    /// quindi costruita <b>durante</b> il controllo aggiornamenti, prima di <c>AppConfig.Initialize()</c>.
    /// Tre conseguenze, tutte verificate sul pacchetto 2.0.0:
    /// <list type="bullet">
    /// <item><c>AppWatcher</c> partiva con la cartella LOG &amp; DUMP vuota, falliva in silenzio, e
    /// l'aggiornamento automatico restava spento per l'intera sessione;</item>
    /// <item>HOME e ogni vista aperta in quei secondi lavoravano su percorsi relativi alla cartella
    /// corrente — la schermata DATABASE apriva <c>modules\database\emails.db</c> creato accanto
    /// all'eseguibile, e lo teneva per tutta la sessione;</item>
    /// <item>la chiusura dello splash azzerava <c>Application.MainWindow</c>, mai più riassegnata: ogni
    /// <c>Application.Current.MainWindow is MainWindow</c> falliva, e i pulsanti treno di EMAIL non
    /// facevano nulla.</item>
    /// </list>
    /// Con l'eseguibile rinominato — lo smoke test dello Sprint 28 — il controllo aggiornamenti ritorna
    /// in modo sincrono e l'ordine risultava giusto per caso: per questo nessuna verifica lo aveva visto.
    /// Ora MainWindow viene creata esplicitamente, in fondo, quando tutto ciò da cui dipende è pronto:
    /// l'ordine non dipende più dai tempi della rete.
    /// </para>
    /// </summary>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Finché MainWindow non esiste: con il default (OnLastWindowClose), chiudere lo splash — per un
        // momento l'unica finestra aperta — chiuderebbe l'intera applicazione.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            await AvviaAsync();
        }
        catch (Exception ex)
        {
            // Qualunque errore dell'avvio (cartella dati non creabile, MainWindow che non si costruisce…)
            // arriverebbe altrimenti al dispatcher da un async void, senza una finestra a cui appoggiarsi.
            CrashReporter.Segnala(ex, "Avvio dell'applicazione (App.OnStartup)", fatale: true);
            _singleInstanceGuard?.Dispose();
            Shutdown(1);
        }
    }

    private async Task AvviaAsync()
    {
        // Pattern istanza singola (§6.1-vicies-bis di PROJECT_MEMORY.md): prima di qualunque finestra,
        // così una seconda istanza termina senza sfarfallii.
        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.IsPrimaryInstance)
        {
            SingleInstanceGuard.ActivateExistingInstance();
            Shutdown();
            return;
        }

        var splash = new UpdateSplashWindow();
        splash.Show();

        try
        {
            // 1. Cartelle dati e LOG & DUMP: prima di QUALUNQUE altra cosa (§5.8) e, soprattutto, prima
            //    di qualsiasi await — dopo il primo await l'ordine non sarebbe più garantito.
            AppConfig.Initialize();

            // 2. Motore SQLite nativo, con un fallimento visibile invece che inghiottito più tardi.
            InizializzaSqliteNativo();

            // 2-bis. Allineamento forzato delle tabelle anagrafiche master (flotte, destinatari mail)
            // al seed incorporato in questa release. Senza questo passo un aggiornamento tramite
            // Auto-Updater lasciava intatto il vecchio train_software.db/emails.db già presente sul PC
            // del tecnico — EstraiSeedMancanti (sopra, dentro AppConfig.Initialize) scrive un seed solo
            // se il file non esiste ancora. Deve girare dopo l'init nativo di SQLite e prima di
            // qualunque modulo (FlotteCache, RubricaDialog, DatabaseView) che legga questi database.
            try
            {
                PersonalAutomationTool.Modules.Database.DatabaseSeedSyncService.SincronizzaAllAvvio();
            }
            catch (Exception ex)
            {
                CrashReporter.Segnala(ex, "Sincronizzazione seed database (DatabaseSeedSyncService)", fatale: false);
            }

            // 3. Auto-update zero-click (§6.1-vicies-septies): niente MainWindow finché il controllo — con
            //    timeout breve, mai bloccante oltre pochi secondi — non è concluso.
            bool aggiornamentoAvviato = false;
            try
            {
                aggiornamentoAvviato = await AutoUpdateService.VerificaEAggiornaAsync(new Progress<string>(splash.SetStatus));
            }
            catch
            {
                // Difesa ulteriore anche se AutoUpdateService non dovrebbe mai propagare: un controllo
                // di aggiornamento silenzioso non deve mai impedire l'avvio normale dell'app.
            }

            if (aggiornamentoAvviato)
            {
                // Il nuovo eseguibile è già scaricato e l'helper di sostituzione è già stato avviato:
                // libera subito il mutex, così quando l'helper riavvia il binario aggiornato (dopo aver
                // atteso la fine di QUESTO processo) trova il nome libero e diventa lui l'istanza
                // primaria, invece di attivare una finestra che sta per sparire.
                _singleInstanceGuard.Dispose();
                Shutdown();
                return;
            }
        }
        finally
        {
            splash.Close();
        }

        MouseWheelScrollBehavior.InitializeGlobalMouseWheelHandler();

        // Prima di MainWindow: HOME ed EXCEL si sottoscrivono già nei loro costruttori.
        AppWatcher.Initialize();

        var finestraPrincipale = new MainWindow();
        MainWindow = finestraPrincipale;
        ShutdownMode = ShutdownMode.OnLastWindowClose;
        finestraPrincipale.Show();

        // Fire-and-forget: idempotente (CREATE INDEX IF NOT EXISTS) e non deve ritardare
        // l'apertura della finestra principale.
        _ = Task.Run(FlotteCache.EnsureIndices);
    }

    /// <summary>
    /// Carica esplicitamente all'avvio la libreria nativa <c>e_sqlite3</c> (SQLitePCLRaw), estratta dal
    /// bundle single-file. <c>Microsoft.Data.Sqlite</c> lo farebbe da sé alla prima connessione, ma lì un
    /// fallimento (DLL non estratta, bloccata da un antivirus) diventa una
    /// <c>TypeInitializationException</c> dentro un modulo qualsiasi, che <c>DatabaseManager</c> e
    /// <c>FlotteCache</c> inghiottono. Qui il problema compare subito, con il suo nome, nel log.
    /// Non fatale: le funzioni che non usano i database restano utilizzabili.
    /// </summary>
    private static void InizializzaSqliteNativo()
    {
        try
        {
            SQLitePCL.Batteries_V2.Init();

            // Init registra il provider; questa chiamata forza il caricamento reale della DLL nativa.
            _ = SQLitePCL.raw.sqlite3_libversion_number();
        }
        catch (Exception ex)
        {
            CrashReporter.Segnala(ex, "Inizializzazione del motore SQLite nativo (e_sqlite3)", fatale: false);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppWatcher.Stop();
        base.OnExit(e);
        _singleInstanceGuard?.Dispose();
    }
}
