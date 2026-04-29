using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class ETR522View : UserControl
    {
        public ETR522View()
        {
            InitializeComponent();
            LoadCartelle();
        }

        private void LoadCartelle()
        {
            string baseLogDump = PersonalAutomationTool.Core.AppConfig.LogAndDumpFolder;
            if (Directory.Exists(baseLogDump))
            {
                var directoryInfo = new DirectoryInfo(baseLogDump);
                var directories = directoryInfo.GetDirectories()
                    .Where(d => d.Name.StartsWith("ETR522", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var filteredNames = directories.Select(d => d.Name).ToList();
                CmbCartelle.ItemsSource = filteredNames;

                var lastCreated = directories.OrderByDescending(d => d.CreationTime).FirstOrDefault();
                if (lastCreated != null)
                {
                    CmbCartelle.SelectedItem = lastCreated.Name;
                }
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
