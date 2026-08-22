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
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        // Cache del testo del file validata su data di modifica + dimensione: come per
        // destinatari.json si evita la lettura da disco ripetuta, ma la deserializzazione
        // resta per chiamata, così ogni chiamante continua a ricevere liste indipendenti.
        private static readonly object _cacheLock = new();
        private static string? _cachedJson;
        private static DateTime _cachedWriteTimeUtc;
        private static long _cachedLength = -1;

        private static string ConfigFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.json");

        private static string? ReadConfigJson(string path)
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

        public static List<TrainShortcutsModel> LoadConfig()
        {
            string path = ConfigFilePath;
            if (!File.Exists(path))
            {
                var defaultConfig = CreateDefaultConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string? json = ReadConfigJson(path);
                if (json == null) return new List<TrainShortcutsModel>();

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
                string json = JsonSerializer.Serialize(config, _jsonOptions);
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio shortcuts.json: {ex.Message}");
            }
            finally
            {
                lock (_cacheLock)
                {
                    _cachedJson = null;
                    _cachedLength = -1;
                    _cachedWriteTimeUtc = default;
                }
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
