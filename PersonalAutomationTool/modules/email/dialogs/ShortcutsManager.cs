using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PersonalAutomationTool.Modules.Email.Dialogs
{
    public class TrainShortcutsModel
    {
        public string TrainName { get; set; } = string.Empty;
        public List<string> Shortcuts { get; set; } = new List<string>();
    }

    public static class ShortcutsManager
    {
        private static readonly string ConfigFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PersonalAutomationTool", "Config");
        private static readonly string ConfigFilePath = Path.Combine(ConfigFolder, "shortcuts.json");

        public static List<TrainShortcutsModel> LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                var defaultConfig = CreateDefaultConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                var config = JsonSerializer.Deserialize<List<TrainShortcutsModel>>(json);
                return config ?? new List<TrainShortcutsModel>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura shortcuts.json: {ex.Message}");
                return new List<TrainShortcutsModel>();
            }
        }

        public static void SaveConfig(List<TrainShortcutsModel> config)
        {
            try
            {
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio shortcuts.json: {ex.Message}");
            }
        }

        private static List<TrainShortcutsModel> CreateDefaultConfig()
        {
            var defaultShortcuts = new List<string>
            {
                "Nulla Riscontrato",
                "Nulla Riscontrato Dati",
                "Sost. Componente",
                "SIM-GIT",
                "SIM-GIT con Dati"
            };

            return new List<TrainShortcutsModel>
            {
                new TrainShortcutsModel { TrainName = "E404P", Shortcuts = new List<string>(defaultShortcuts) },
                new TrainShortcutsModel { TrainName = "ETR1000", Shortcuts = new List<string>(defaultShortcuts) },
                new TrainShortcutsModel { TrainName = "ETR1000FH", Shortcuts = new List<string>(defaultShortcuts) },
                new TrainShortcutsModel { TrainName = "ETR700", Shortcuts = new List<string>(defaultShortcuts) },
                new TrainShortcutsModel { TrainName = "ETR521", Shortcuts = new List<string>(defaultShortcuts) },
                new TrainShortcutsModel { TrainName = "ETR522", Shortcuts = new List<string>(defaultShortcuts) }
            };
        }

        public static List<string> GetShortcutsForTrain(string trainName)
        {
            var config = LoadConfig();
            var trainConfig = config.FirstOrDefault(t => t.TrainName.Equals(trainName, StringComparison.OrdinalIgnoreCase));
            
            if (trainConfig != null && trainConfig.Shortcuts != null && trainConfig.Shortcuts.Count > 0)
            {
                return trainConfig.Shortcuts;
            }

            // Fallback se non ci sono shortcut configurati per quel treno
            return new List<string>
            {
                "Nulla Riscontrato",
                "Nulla Riscontrato Dati",
                "Sost. Componente",
                "SIM-GIT",
                "SIM-GIT con Dati"
            };
        }
    }
}
