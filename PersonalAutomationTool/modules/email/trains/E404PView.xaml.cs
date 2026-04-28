using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class E404PView : UserControl
    {
        public E404PView()
        {
            InitializeComponent();
            LoadCartelle();
        }

        private void LoadCartelle()
        {
            string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
            if (Directory.Exists(baseLogDump))
            {
                var directories = Directory.GetDirectories(baseLogDump);
                var filtered = directories
                    .Select(d => Path.GetFileName(d))
                    .Where(n => n != null && n.StartsWith("E404P", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                CmbCartelle.ItemsSource = filtered;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.MainContentControl.Content = new EmailView();
            }
        }

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e) { }
        private void BtnLogDump_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenzaVI_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenzaVT_Click(object sender, RoutedEventArgs e) { }
        private void BtnR2_Click(object sender, RoutedEventArgs e) { }
    }
}
