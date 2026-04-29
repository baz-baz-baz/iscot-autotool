using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Modules.DestinatariMail
{
    public partial class DestinatariMailView : UserControl
    {
        public ObservableCollection<TrainConfig> TrainConfigs { get; set; }

        public DestinatariMailView()
        {
            InitializeComponent();
            TrainConfigs = DestinatariManager.LoadConfig();
            this.DataContext = this;
        }

        private void BtnSalva_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DestinatariManager.SaveConfig(TrainConfigs);
                MessageBox.Show("Modifiche ai destinatari salvate con successo!", "Salvataggio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Errore durante il salvataggio: {ex.Message}", "Errore", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnApriRubrica_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is EmailActionConfig actionConfig)
            {
                var dialog = new RubricaDialog
                {
                    Owner = Window.GetWindow(this)
                };

                if (dialog.ShowDialog() == true)
                {
                    string selectedEmails = dialog.GetSelectedEmails();
                    if (!string.IsNullOrEmpty(selectedEmails))
                    {
                        string tag = btn.Tag?.ToString() ?? "";
                        if (tag == "To")
                        {
                            string current = actionConfig.ToRecipients;
                            actionConfig.ToRecipients = string.IsNullOrEmpty(current) ? selectedEmails : current + "; " + selectedEmails;
                        }
                        else if (tag == "Cc")
                        {
                            string current = actionConfig.CcRecipients;
                            actionConfig.CcRecipients = string.IsNullOrEmpty(current) ? selectedEmails : current + "; " + selectedEmails;
                        }
                    }
                }
            }
        }
    }
}
