using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR521View : UserControl
    {
        public ETR521View()
        {
            InitializeComponent();
            TrainViewHelper.LoadCartelle(CmbCartelle, "ETR521");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();
    }
}
