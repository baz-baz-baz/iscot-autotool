using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR1000FHView : UserControl
    {
        public ETR1000FHView()
        {
            InitializeComponent();
            // "ETR1001FH" è il tipo reale scritto nei nomi di cartella (colonna `tipo` di flotte.db,
            // vedi PROJECT_MEMORY.md §5.3-bis): "ETR1000 FH" resta come prefisso di compatibilità per
            // eventuali cartelle più vecchie create prima che questa combo diventasse DB-driven.
            TrainViewHelper.LoadCartelle(CmbCartelle, "ETR1001FH", "ETR1000 FH");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.OpenChiusuraTicketDialog(cartella, "ETR1001FH", isNd);
        }

        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e) { }

        private void ChkPrefissoND_Checked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, true);
        private void ChkPrefissoND_Unchecked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, false);
    }
}
