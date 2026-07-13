using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR1000IFView : UserControl
    {
        public ETR1000IFView()
        {
            InitializeComponent();
            TrainViewHelper.LoadCartelle(CmbCartelle, "ETR1000IF", "ETR1000 I-F");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.OpenChiusuraTicketDialog(cartella, "ETR1000IF", isNd);
        }

        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e) { }

        private void BtnScadenzeFrancesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determina il nome della scadenza dal primo file .txt trovato
            string scadenzaName = "I0"; // Fallback
            string baseLogDump = Core.AppConfig.LogAndDumpFolder;
            string fullPath = Path.Combine(baseLogDump, cartella);
            if (Directory.Exists(fullPath))
            {
                var txtFiles = Directory.GetFiles(fullPath, "*.txt");
                if (txtFiles.Length > 0)
                {
                    scadenzaName = Path.GetFileNameWithoutExtension(txtFiles[0]);
                }
            }

            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.GenerateScadenzaEmailDirect(cartella, "ETR1000IF", "Scadenze Francesi",
                $"Revisione {scadenzaName}",
                $"Eseguito controlli statici per scadenza {scadenzaName} come da PdM con esito positivo.",
                isNd, useBistandardClean: true);
        }

        private void ChkPrefissoND_Checked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, true);
        private void ChkPrefissoND_Unchecked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, false);
    }
}
