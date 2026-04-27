using System;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR1000IFView : UserControl
    {
        public ETR1000IFView()
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
