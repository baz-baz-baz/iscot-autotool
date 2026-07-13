using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR1000View : UserControl
    {
        public ETR1000View()
        {
            InitializeComponent();
            TrainViewHelper.LoadCartelle(CmbCartelle, ["ETR1000 "], excludePrefixes: ["ETR1000 I-F", "ETR1000 FH"]);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.OpenChiusuraTicketDialog(cartella, "ETR1000", isNd);
        }

        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.GenerateScadenzaEmailDirect(cartella, "ETR1000", "Scadenza 6 Mesi",
                "Revisione 6 mesi",
                "Eseguito controlli statici per scadenza semestrale come da procedura MR20A con esito positivo. Eseguito scarico dati. Con riferimento all'SSB, il treno è conforme all'esercizio commerciale.",
                isNd);
        }

        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.GenerateScadenzaEmailDirect(cartella, "ETR1000", "Scadenza 12 Mesi",
                "Revisione 12 mesi",
                "Eseguito controlli statici per scadenza annuale come da procedura MR20A con esito positivo. Eseguito scarico dati. Con riferimento all'SSB, il treno è conforme all'esercizio commerciale.",
                isNd);
        }

        private void Btn3R1_Click(object sender, RoutedEventArgs e) { }

        private void ChkPrefissoND_Checked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, true);
        private void ChkPrefissoND_Unchecked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, false);
    }
}
