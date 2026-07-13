using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.Email.Trains
{
    public partial class E404PView : UserControl
    {
        public E404PView()
        {
            InitializeComponent();
            TrainViewHelper.LoadCartelle(CmbCartelle, "E404P");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => TrainViewHelper.NavigateBack();

        private void BtnChiusuraTicket_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            TrainViewHelper.OpenChiusuraTicketDialog(cartella, "E404P", isNd: false);
        }

        private void BtnLogDump_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            TrainViewHelper.OpenChiusuraTicketDialog(cartella, "E404P", isNd: false, actionType: "Log Dump");
        }

        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            TrainViewHelper.GenerateScadenzaEmailWithDialog(cartella, "E404P", "Scadenza 6 Mesi",
                "STB operazioni alle motrici indicate a scadenza 6 mesi come da PM ETR500 AV documento di riferimento PM 203/E/F-L/P edizione vigente",
                "Eseguita scadenza 6 mesi con esito positivo come da manuale MR20A");
        }

        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            TrainViewHelper.GenerateScadenzaEmailWithDialog(cartella, "E404P", "Scadenza 12 Mesi",
                "STB operazioni alle motrici indicate a scadenza 12 mesi come da PM ETR500 AV documento di riferimento PM 203/E/F-L/P edizione vigente.",
                "Eseguita scadenza 12 mesi con esito positivo come da manuale MR20A");
        }

        private void BtnScadenzaVI_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            TrainViewHelper.GenerateScadenzaEmailWithDialog(cartella, "E404P", "Scadenza V.I",
                "Eseguire controlli come da checklist AV-VI",
                "Effettuati controlli come da checklist AV-VI senza riscontrare anomalie al SSB/ETCS4. Eseguito scarico dati per analisi dell'Ingegneria Hitachi Rail STS.");
        }

        private void BtnScadenzaVT_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // VT non salva la cache (come da codice originale)
            var dialog = new Dialogs.ChiusuraTicketDialog(cartella, "E404P", false, "Scadenza V.T");
            foreach (var group in dialog.LocoGroups)
            {
                foreach (var input in group.Inputs)
                {
                    input.Avviso = "";
                    input.DataOra = "";
                    input.Avaria = "Eseguire scadenza VT";
                    input.Intervento = "Effettuati controlli di VT come da checklist con esito positivo.";
                }
            }
            EmailService.GenerateChiusuraTicketEmail(cartella, "E404P", dialog.LocoGroups, false, "Scadenza V.T");
        }

        private void BtnR2_Click(object sender, RoutedEventArgs e) { }
    }
}
