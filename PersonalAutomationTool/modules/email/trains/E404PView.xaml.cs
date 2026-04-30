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
                var directoryInfo = new DirectoryInfo(baseLogDump);
                var directories = directoryInfo.GetDirectories()
                    .Where(d => d.Name.StartsWith("E404P", StringComparison.OrdinalIgnoreCase))
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

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "E404P")
            {
                Owner = Application.Current.MainWindow
            };
            
            if (dialog.ShowDialog() == true)
            {
                MessageBox.Show("Dati confermati! Implementare la generazione email.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void BtnLogDump_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenzaVI_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenzaVT_Click(object sender, RoutedEventArgs e) { }
        private void BtnR2_Click(object sender, RoutedEventArgs e) { }
    }
}
