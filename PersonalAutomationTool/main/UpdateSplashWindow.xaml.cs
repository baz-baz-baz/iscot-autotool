using System.Windows;

namespace PersonalAutomationTool
{
    /// <summary>
    /// Splash minimale mostrato durante il controllo aggiornamenti all'avvio. Nessun ViewModel: due
    /// righe di code-behind, stessa categoria di <c>RenamePreviewDialog</c> (§2.2 di PROJECT_MEMORY.md)
    /// — un guscio senza logica di decisione.
    /// </summary>
    public partial class UpdateSplashWindow : Window
    {
        public UpdateSplashWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Aggiorna il testo di stato. Sicuro da chiamare da un thread diverso da quello UI: passa
        /// sempre dal <see cref="System.Windows.Threading.Dispatcher"/> della finestra.
        /// </summary>
        public void SetStatus(string testo)
        {
            Dispatcher.Invoke(() => TxtStato.Text = testo);
        }
    }
}
