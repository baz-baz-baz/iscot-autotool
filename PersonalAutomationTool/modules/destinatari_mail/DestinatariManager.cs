using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace PersonalAutomationTool.Modules.DestinatariMail
{
    public class EmailActionConfig : INotifyPropertyChanged
    {
        private string _actionName = string.Empty;
        public string ActionName
        {
            get => _actionName;
            set { _actionName = value; OnPropertyChanged(nameof(ActionName)); }
        }

        private string _toRecipients = string.Empty;
        public string ToRecipients
        {
            get => _toRecipients;
            set { _toRecipients = value; OnPropertyChanged(nameof(ToRecipients)); }
        }

        private string _ccRecipients = string.Empty;
        public string CcRecipients
        {
            get => _ccRecipients;
            set { _ccRecipients = value; OnPropertyChanged(nameof(CcRecipients)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class TrainConfig : INotifyPropertyChanged
    {
        private string _trainName = string.Empty;
        public string TrainName
        {
            get => _trainName;
            set { _trainName = value; OnPropertyChanged(nameof(TrainName)); }
        }

        private ObservableCollection<EmailActionConfig> _actions = new ObservableCollection<EmailActionConfig>();
        public ObservableCollection<EmailActionConfig> Actions
        {
            get => _actions;
            set { _actions = value; OnPropertyChanged(nameof(Actions)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public static class DestinatariManager
    {
        private static string GetConfigPath()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(folder, "destinatari.json");
        }

        public static ObservableCollection<TrainConfig> LoadConfig()
        {
            string path = GetConfigPath();
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<ObservableCollection<TrainConfig>>(json);
                    if (data != null && data.Count > 0)
                    {
                        return data;
                    }
                }
                catch (Exception)
                {
                    // Fallback to default if error
                }
            }

            return GenerateDefaultConfig();
        }

        public static void SaveConfig(ObservableCollection<TrainConfig> config)
        {
            string path = GetConfigPath();
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(path, json);
        }

        private static ObservableCollection<TrainConfig> GenerateDefaultConfig()
        {
            var config = new ObservableCollection<TrainConfig>
            {
                new TrainConfig
                {
                    TrainName = "E404P",
                    Actions = new ObservableCollection<EmailActionConfig>
                    {
                        new EmailActionConfig { ActionName = "Chiusura Ticket", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new EmailActionConfig { ActionName = "Log Dump", ToRecipients = "etr500_analisidiagssb_sts@hitachirail.com", CcRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 6 Mesi", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new EmailActionConfig { ActionName = "Scadenza 12 Mesi", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new EmailActionConfig { ActionName = "Scadenza V.I", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new EmailActionConfig { ActionName = "Scadenza V.T", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new EmailActionConfig { ActionName = "R2", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" }
                    }
                },
                new TrainConfig
                {
                    TrainName = "ETR700",
                    Actions = new ObservableCollection<EmailActionConfig>
                    {
                        new EmailActionConfig { ActionName = "Chiusura Ticket", ToRecipients = "etr700_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" }
                    }
                },
                new TrainConfig
                {
                    TrainName = "ETR1000",
                    Actions = new ObservableCollection<EmailActionConfig>
                    {
                        new EmailActionConfig { ActionName = "Chiusura Ticket", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 6 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 12 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "3R1", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" }
                    }
                },
                new TrainConfig
                {
                    TrainName = "ETR1000IF",
                    Actions = new ObservableCollection<EmailActionConfig>
                    {
                        new EmailActionConfig { ActionName = "Chiusura Ticket", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 6 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 12 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenze Francesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" }
                    }
                },
                new TrainConfig
                {
                    TrainName = "ETR1000FH",
                    Actions = new ObservableCollection<EmailActionConfig>
                    {
                        new EmailActionConfig { ActionName = "Chiusura Ticket", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 6 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" },
                        new EmailActionConfig { ActionName = "Scadenza 12 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com" }
                    }
                }
            };
            return config;
        }
    }
}
