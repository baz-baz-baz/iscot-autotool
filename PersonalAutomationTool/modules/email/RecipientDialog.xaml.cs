using System;
using System.Linq;
using System.Windows;

namespace PersonalAutomationTool.Modules.Email
{
    public partial class RecipientDialog : Window
    {
        private readonly string _trainType;

        public RecipientDialog(string trainType)
        {
            InitializeComponent();
            _trainType = trainType;
            TxtTitle.Text = $"Destinatari per: {_trainType}";
            LoadData();
        }

        private void LoadData()
        {
            var config = EmailSettingsManager.LoadRecipients(_trainType);
            TxtToRecipients.Text = string.Join("; ", config.To);
            TxtCcRecipients.Text = string.Join("; ", config.Cc);
            TxtBccRecipients.Text = string.Join("; ", config.Bcc);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Come Outlook, dividiamo per punto e virgola ';' o nuova riga.
            char[] separators = { ';', '\n', '\r' };

            var toLines = TxtToRecipients.Text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(l => l.Trim())
                                              .Where(l => !string.IsNullOrEmpty(l))
                                              .ToList();

            var ccLines = TxtCcRecipients.Text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(l => l.Trim())
                                              .Where(l => !string.IsNullOrEmpty(l))
                                              .ToList();

            var bccLines = TxtBccRecipients.Text.Split(separators, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(l => l.Trim())
                                              .Where(l => !string.IsNullOrEmpty(l))
                                              .ToList();

            var config = new EmailRecipientsConfig
            {
                To = toLines,
                Cc = ccLines,
                Bcc = bccLines
            };

            EmailSettingsManager.SaveRecipients(_trainType, config);
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
