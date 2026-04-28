using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR521View : UserControl
    {
        public ETR521View()
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
                    .Where(n => n != null && n.StartsWith("ETR521", StringComparison.OrdinalIgnoreCase))
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
    }
}
