using System;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR522View : UserControl
    {
        public ETR522View()
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
