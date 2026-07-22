using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    public partial class PassaggioConsegneView : UserControl
    {
        public PassaggioConsegneView()
        {
            InitializeComponent();
        }

        private void BtnPassaggioConsegne_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is PassaggioConsegneViewModel vm && vm.SelectedRapportino != null)
            {
                vm.SalvaDati(showNotification: false);
                string pdfPath = PassaggioConsegnePdfExporter.ExportToPdf(RapportinoSheetBorder, vm.SelectedRapportino.TipoTreno);
                PassaggioConsegneEmailService.OpenDraftEmail(vm.SelectedRapportino, pdfPath);
            }
        }
    }
}
