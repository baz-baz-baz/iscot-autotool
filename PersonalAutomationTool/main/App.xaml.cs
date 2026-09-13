using System;
using System.Windows;

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
    private PersonalAutomationTool.Core.SingleInstanceGuard? _singleInstanceGuard;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Pattern istanza singola (§6.1-vicies-bis di PROJECT_MEMORY.md): deve girare PRIMA di
        // base.OnStartup, perché è dentro base.OnStartup che WPF crea e mostra la finestra di
        // StartupUri (MainWindow.xaml). Se questa è una seconda istanza, base.OnStartup non viene
        // proprio chiamato: niente finestra creata, niente sfarfallio da richiudere subito dopo.
        _singleInstanceGuard = new PersonalAutomationTool.Core.SingleInstanceGuard();
        if (!_singleInstanceGuard.IsPrimaryInstance)
        {
            PersonalAutomationTool.Core.SingleInstanceGuard.ActivateExistingInstance();
            Shutdown();
            return;
        }

        // Auto-update zero-click (PROJECT_MEMORY.md, sprint auto-update): niente MainWindow finché
        // il controllo aggiornamenti — con timeout breve, mai bloccante oltre pochi secondi — non è
        // concluso. ShutdownMode passa a OnExplicitShutdown perché il default (OnLastWindowClose)
        // chiuderebbe l'intera applicazione quando lo splash si chiude, dato che a quel punto è
        // l'unica finestra aperta: MainWindow non esiste ancora, viene creata solo dentro
        // base.OnStartup subito sotto. Ripristinato al valore di default appena MainWindow è aperta.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var splash = new UpdateSplashWindow();
        splash.Show();

        bool aggiornamentoAvviato = false;
        try
        {
            aggiornamentoAvviato = await PersonalAutomationTool.Core.AutoUpdateService
                .VerificaEAggiornaAsync(new Progress<string>(splash.SetStatus));
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
            splash.Close();
            Shutdown();
            return;
        }

        splash.Close();
        ShutdownMode = ShutdownMode.OnLastWindowClose;

        base.OnStartup(e);
        PersonalAutomationTool.Core.AppConfig.Initialize();
        PersonalAutomationTool.Core.MouseWheelScrollBehavior.InitializeGlobalMouseWheelHandler();

        // Fire-and-forget: idempotente (CREATE INDEX IF NOT EXISTS) e non deve ritardare
        // l'apertura della finestra principale.
        _ = System.Threading.Tasks.Task.Run(PersonalAutomationTool.Core.FlotteCache.EnsureIndices);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        _singleInstanceGuard?.Dispose();
    }
}
