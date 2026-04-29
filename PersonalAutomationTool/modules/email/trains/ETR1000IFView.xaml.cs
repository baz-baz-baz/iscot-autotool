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

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = "";
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella);
            dialog.Owner = Application.Current.MainWindow;
            
            if (dialog.ShowDialog() == true)
            {
                MessageBox.Show("Dati confermati! Implementare la generazione email.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e) { }
        private void BtnScadenzeFrancesi_Click(object sender, RoutedEventArgs e) { }

        private void ChkPrefissoND_Checked(object sender, RoutedEventArgs e)
        {
            if (TxtPrefissoND != null)
            {
                TxtPrefissoND.Text = "ON";
                TxtPrefissoND.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3B82F6"));
            }
        }

        private void ChkPrefissoND_Unchecked(object sender, RoutedEventArgs e)
        {
            if (TxtPrefissoND != null)
            {
                TxtPrefissoND.Text = "OFF";
                TxtPrefissoND.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7F8C8D"));
            }
        }
    }
}
