using System;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR700View : UserControl
    {
        public ETR700View()
        {
            InitializeComponent();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.MainContentControl.Content = new EmailView();
            }
        }
    }
}
