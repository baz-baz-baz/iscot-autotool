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

        // Cache del solo TESTO del file, validata su data di modifica + dimensione.
        // Il JSON continua a essere deserializzato a ogni chiamata, quindi ogni chiamante
        // riceve come prima un grafo di oggetti indipendente (le modifiche non salvate nella
        // schermata "Destinatari Mail" restano invisibili alla generazione email, esattamente
        // come nel comportamento originale). Ciò che si risparmia è la lettura da disco,
        // che avveniva a ogni email generata e a ogni lettura di destinatari.
        private static readonly object _cacheLock = new();
        private static string? _cachedJson;
        private static DateTime _cachedWriteTimeUtc;
        private static long _cachedLength = -1;

        private static string GetConfigPath()
        {
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(folder, "destinatari.json");
        }

        private static string? ReadConfigJson(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return null;

                lock (_cacheLock)
                {
                    if (_cachedJson != null &&
                        _cachedLength == info.Length &&
                        _cachedWriteTimeUtc == info.LastWriteTimeUtc)
                    {
                        return _cachedJson;
                    }

                    string json = File.ReadAllText(path);
                    _cachedJson = json;
                    _cachedLength = info.Length;
                    _cachedWriteTimeUtc = info.LastWriteTimeUtc;
                    return json;
                }
            }
            catch
            {
                return null;
            }
        }

        private static void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedJson = null;
                _cachedLength = -1;
                _cachedWriteTimeUtc = default;
            }
        }

        public static ObservableCollection<TrainConfig> LoadConfig()
        {
            ObservableCollection<TrainConfig>? data = null;
            string path = GetConfigPath();
            string? json = ReadConfigJson(path);
            if (json != null)
            {
                try
                {
                    data = JsonSerializer.Deserialize<ObservableCollection<TrainConfig>>(json);
                }
                catch { }
            }

            if (data == null || data.Count == 0)
            {
                data = GenerateDefaultConfig();
                SaveConfig(data);
                return data;
            }

            EnsurePassaggioConsegneActions(data);
            return data;
        }

        public static void SaveConfig(ObservableCollection<TrainConfig> config)
        {
            string path = GetConfigPath();
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(path, json);
            InvalidateCache();
        }

        public static EmailActionConfig? GetRecipients(string trainType, string actionName)
        {
            var config = LoadConfig();
            if (string.IsNullOrWhiteSpace(trainType)) return null;

            string cleanInput = trainType.Replace(" ", "").ToUpperInvariant();

            foreach (var train in config)
            {
                string cleanTrain = train.TrainName.Replace(" ", "").ToUpperInvariant();
                bool match = cleanTrain == cleanInput;
                if (!match)
                {
                    if ((cleanInput.Contains("500") || cleanInput.Contains("404")) && (cleanTrain.Contains("500") || cleanTrain.Contains("404")))
                        match = true;
                }

                if (match)
                {
                    var act = train.Actions.FirstOrDefault(a => a.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase));
                    if (act != null) return act;
                }
            }

            return null;
        }

        private static void EnsurePassaggioConsegneActions(ObservableCollection<TrainConfig> config)
        {
            // La configurazione di default (5 treni × fino a 8 azioni, con stringhe lunghe di
            // destinatari) viene costruita solo se manca davvero un'azione da integrare.
            // Prima veniva rigenerata a ogni LoadConfig, cioè a ogni email.
            ObservableCollection<TrainConfig>? defaultConfig = null;

            foreach (var train in config)
            {
                bool hasPassaggio = train.Actions.Any(a => a.ActionName.Equals("Passaggio di consegne", StringComparison.OrdinalIgnoreCase));
                if (!hasPassaggio)
                {
                    defaultConfig ??= GenerateDefaultConfig();
                    var defaultTrain = defaultConfig.FirstOrDefault(t => t.TrainName.Equals(train.TrainName, StringComparison.OrdinalIgnoreCase));
                    var passaggioAction = defaultTrain?.Actions.FirstOrDefault(a => a.ActionName.Equals("Passaggio di consegne", StringComparison.OrdinalIgnoreCase));
                    if (passaggioAction != null)
                    {
                        train.Actions.Add(new EmailActionConfig
                        {
                            ActionName = passaggioAction.ActionName,
                            ToRecipients = passaggioAction.ToRecipients,
                            CcRecipients = passaggioAction.CcRecipients
                        });
                    }
                }
            }
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
                        new() { ActionName = "Passaggio di consegne", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
                        new() { ActionName = "Log Dump", ToRecipients = "etr500_analisidiagssb_sts@hitachirail.com", CcRecipients = "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; salvatore.demartino@hitachirail.com; Service_ISCOT_IMC_AV_Milano@it.iscot.com" },
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
                        new() { ActionName = "Passaggio di consegne", ToRecipients = "etr700_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
                        new() { ActionName = "Chiusura Ticket", ToRecipients = "etr700_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" }
                    ]
                },
                new()
                {
                    TrainName = "ETR1000",
                    Actions =
                    [
                        new() { ActionName = "Passaggio di consegne", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
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
                        new() { ActionName = "Passaggio di consegne", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
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
                        new() { ActionName = "Passaggio di consegne", ToRecipients = "etr1000_analisidiagssb_sts@hitachirail.com", CcRecipients = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" },
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
