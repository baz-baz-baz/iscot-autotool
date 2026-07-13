using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR700View : UserControl
    {
        public ETR700View()
        {
            InitializeComponent();
            TrainViewHelper.LoadCartelle(CmbCartelle, "ETR700");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            bool isNd = ChkPrefissoND.IsChecked == true;
            TrainViewHelper.OpenChiusuraTicketDialog(cartella, "ETR700", isNd);
        }

        private void ChkPrefissoND_Checked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, true);
        private void ChkPrefissoND_Unchecked(object sender, RoutedEventArgs e) => TrainViewHelper.SetNdCheckboxState(TxtPrefissoND, false);
    }
}
