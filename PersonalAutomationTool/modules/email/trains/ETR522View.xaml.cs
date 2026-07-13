using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR522View : UserControl
    {
        public ETR522View()
        {
            InitializeComponent();
            TrainViewHelper.LoadCartelle(CmbCartelle, "ETR522");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();
    }
}
