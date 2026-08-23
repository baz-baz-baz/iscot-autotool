using System.Windows;

namespace PersonalAutomationTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        PersonalAutomationTool.Core.AppConfig.Initialize();
        PersonalAutomationTool.Core.MouseWheelScrollBehavior.InitializeGlobalMouseWheelHandler();

        // Fire-and-forget: idempotente (CREATE INDEX IF NOT EXISTS) e non deve ritardare
        // l'apertura della finestra principale.
        _ = System.Threading.Tasks.Task.Run(PersonalAutomationTool.Core.FlotteCache.EnsureIndices);
    }
}
