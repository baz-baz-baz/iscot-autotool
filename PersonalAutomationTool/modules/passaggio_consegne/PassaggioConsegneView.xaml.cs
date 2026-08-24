using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Vista del modulo PASSAGGIO CONSEGNE: un <c>TabControl</c> con una scheda per flotta
    /// (ETR 500, ETR 700, ETR 1000), tutte generate dallo stesso <c>DataTemplate</c>.
    ///
    /// <para>
    /// Code-behind volutamente vuoto: il DataContext è creato in XAML e ogni comportamento vive nel
    /// <see cref="PassaggioConsegneViewModel"/>. In particolare l'esportazione del PDF <b>non</b> passa
    /// più di qui: la versione precedente aveva un gestore <c>Click</c> che catturava questa stessa
    /// vista con <c>RenderTargetBitmap</c>, ed è la ragione per cui il modulo non era verificabile da
    /// test automatici. Ora il PDF nasce da un <see cref="RapportinoSnapshot"/> e la vista non ha
    /// alcun ruolo nella sua generazione.
    /// </para>
    /// </summary>
    public partial class PassaggioConsegneView : UserControl
    {
        public PassaggioConsegneView()
        {
            InitializeComponent();
        }
    }
}
