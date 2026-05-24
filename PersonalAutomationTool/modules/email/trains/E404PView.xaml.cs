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
                // MessageBox.Show("Dati confermati! Implementare la generazione email.", "Successo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        private void BtnLogDump_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "E404P", false, "Log Dump")
            {
                Owner = Application.Current.MainWindow
            };
            
            if (dialog.ShowDialog() == true)
            {
            }
        }
        private void BtnScadenza6Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Inizializza il dialog per sfruttare il suo parsing delle locomotive, senza mostrarlo
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "E404P", false, "Scadenza 6 Mesi");

            foreach (var group in dialog.LocoGroups)
            {
                foreach (var input in group.Inputs)
                {
                    input.Avviso = "";
                    input.DataOra = "";
                    input.Avaria = "STB operazioni alle motrici indicate a scadenza 6 mesi come da PM ETR500 AV documento di riferimento PM 203/E/F-L/P edizione vigente";
                    input.Intervento = "Eseguita scadenza 6 mesi con esito positivo come da manuale MR20A";
                }
            }

            // Salva in cache in modo che la successiva mail "Log Dump" trovi i dati precompilati
            dialog.SaveCache();
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "E404P", dialog.LocoGroups, false, "Scadenza 6 Mesi");
        }

        private void BtnScadenza12Mesi_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Inizializza il dialog per sfruttare il suo parsing delle locomotive, senza mostrarlo
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "E404P", false, "Scadenza 12 Mesi");

            foreach (var group in dialog.LocoGroups)
            {
                foreach (var input in group.Inputs)
                {
                    input.Avviso = "";
                    input.DataOra = "";
                    input.Avaria = "STB operazioni alle motrici indicate a scadenza 12 mesi come da PM ETR500 AV documento di riferimento PM 203/E/F-L/P edizione vigente.";
                    input.Intervento = "Eseguita scadenza 12 mesi con esito positivo come da manuale MR20A";
                }
            }

            // Salva in cache in modo che la successiva mail "Log Dump" trovi i dati precompilati
            dialog.SaveCache();
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "E404P", dialog.LocoGroups, false, "Scadenza 12 Mesi");
        }

        private void BtnScadenzaVI_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Inizializza il dialog per sfruttare il parsing, senza mostrarlo
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "E404P", false, "Scadenza V.I");

            foreach (var group in dialog.LocoGroups)
            {
                foreach (var input in group.Inputs)
                {
                    input.Avviso = "";
                    input.DataOra = "";
                    input.Avaria = "Eseguire controlli come da checklist AV-VI";
                    input.Intervento = "Effettuati controlli come da checklist AV-VI senza riscontrare anomalie al SSB/ETCS4. Eseguito scarico dati per analisi dell’Ingegneria Hitachi Rail STS.";
                }
            }

            // Salva in cache in modo che la successiva mail "Log Dump" trovi i dati precompilati
            dialog.SaveCache();
            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "E404P", dialog.LocoGroups, false, "Scadenza V.I");
        }
        
        private void BtnScadenzaVT_Click(object sender, RoutedEventArgs e)
        {
            string cartella = CmbCartelle.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(cartella))
            {
                MessageBox.Show("Selezionare una cartella.", "Attenzione", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Inizializza il dialog per sfruttare il parsing, senza mostrarlo
            var dialog = new PersonalAutomationTool.Modules.Email.Dialogs.ChiusuraTicketDialog(cartella, "E404P", false, "Scadenza V.T");

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

            // NOTA: Non salviamo la cache (dialog.SaveCache()) come richiesto, così non sovrascrive
            // i log e dump preesistenti per questa cartella.

            PersonalAutomationTool.Modules.Email.EmailService.GenerateChiusuraTicketEmail(cartella, "E404P", dialog.LocoGroups, false, "Scadenza V.T");
        }

        private void BtnR2_Click(object sender, RoutedEventArgs e) { }
    }
}
