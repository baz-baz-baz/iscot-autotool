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

    protected override void OnStartup(StartupEventArgs e)
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
