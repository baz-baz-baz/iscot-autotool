using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.DestinatariMail
{
    public class EmailActionConfig : ViewModelBase
    {
        private string _actionName = string.Empty;
        public string ActionName
        {
            get => _actionName;
            set => SetProperty(ref _actionName, value);
        }

        private string _toRecipients = string.Empty;
        public string ToRecipients
        {
            get => _toRecipients;
            set => SetProperty(ref _toRecipients, value);
        }

        private string _ccRecipients = string.Empty;
        public string CcRecipients
        {
            get => _ccRecipients;
            set => SetProperty(ref _ccRecipients, value);
        }
    }

    public class TrainConfig : ViewModelBase
    {
        private string _trainName = string.Empty;
        public string TrainName
        {
            get => _trainName;
            set => SetProperty(ref _trainName, value);
        }

        private ObservableCollection<EmailActionConfig> _actions = [];
        public ObservableCollection<EmailActionConfig> Actions
        {
            get => _actions;
            set => SetProperty(ref _actions, value);
        }
    }

    public static class DestinatariManager
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

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
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(path, json);
        }

        private static ObservableCollection<TrainConfig> GenerateDefaultConfig()
        {
            var config = new ObservableCollection<TrainConfig>
            {
                new()
                {
                    TrainName = "E404P",
                    Actions =
                    [
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "Log Dump", ToRecipients = "etr500_analisidiagssb_sts@hitachirail.com", CcRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; salvatore.demartino@hitachirail.com" },
                        new() { ActionName = "Scadenza 6 Mesi", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "Scadenza 12 Mesi", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "Scadenza V.I", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "Scadenza V.T", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "R2", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" }
                    ]
                },
                new()
                {
                    TrainName = "ETR700",
                    Actions =
                    [
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "etr700_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" }
                    ]
                },
                new()
                {
                    TrainName = "ETR1000",
                    Actions =
                    [
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenza 6 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenza 12 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "3R1", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" }
                    ]
                },
                new()
                {
                    TrainName = "ETR1000IF",
                    Actions =
                    [
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenza 6 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenza 12 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenze Francesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" }
                    ]
                },
                new()
                {
                    TrainName = "ETR1000FH",
                    Actions =
                    [
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenza 6 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Scadenza 12 mesi", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" }
                    ]
                }
            };
            return config;
        }
    }
}
