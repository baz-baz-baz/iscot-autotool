using System;
using System.Threading.Tasks;
using System.Windows;

namespace PersonalAutomationTool.Modules.Home
{
    /// <summary>
    /// Dialog modale di riscontro per <see cref="PathHealthCheckService"/>. Avvia la scansione da solo
    /// all'apertura (<c>Loaded</c>), così il tecnico non deve premere un secondo pulsante dopo aver già
    /// premuto "Verifica Percorsi Hitachi" in HOME; "Ricontrolla" ripete la stessa scansione senza
    /// richiudere la finestra.
    ///
    /// <para>
    /// Codice-behind diretto, senza ViewModel: stessa categoria di <c>RenamePreviewDialog</c> — un
    /// guscio sottile che mostra un elenco già calcolato da un servizio, senza logica di decisione
    /// propria (§2.2 di PROJECT_MEMORY.md).
    /// </para>
    /// </summary>
    public partial class HealthCheckPathsDialog : Window
    {
        public HealthCheckPathsDialog()
        {
            InitializeComponent();
            Loaded += async (_, _) => await EseguiScansioneAsync();
        }

        private async Task EseguiScansioneAsync()
        {
            Overlay.IsBusy = true;
            try
            {
                // Ogni percorso può essere una cartella SharePoint/OneDrive lenta o disconnessa:
                // mai sul thread UI (§3, vincolo 1 di PROJECT_MEMORY.md).
                var risultati = await Task.Run(PathHealthCheckService.EseguiControllo);
                ItemsGrid.ItemsSource = risultati;
            }
            catch (Exception ex)
            {
                // Difensivo: PathHealthCheckService intercetta già ogni eccezione per singolo
                // percorso, quindi non dovrebbe mai arrivare qui. Se succede comunque (es. le
                // configurazioni JSON stesse non leggibili), la scansione non deve lasciare la
                // finestra bloccata sull'overlay senza spiegazione.
                MessageBox.Show($"Errore durante la verifica dei percorsi:\n{ex.Message}",
                    "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Overlay.IsBusy = false;
            }
        }

        private async void BtnRicontrolla_Click(object sender, RoutedEventArgs e) => await EseguiScansioneAsync();

        private void BtnChiudi_Click(object sender, RoutedEventArgs e) => Close();
    }
}
