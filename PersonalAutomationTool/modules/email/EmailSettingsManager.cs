using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PersonalAutomationTool.Modules.Email
{
    public class EmailRecipientsConfig
    {
        public List<string> To { get; set; } = new List<string>();
        public List<string> Cc { get; set; } = new List<string>();
        public List<string> Bcc { get; set; } = new List<string>();
    }

    public static class EmailSettingsManager
    {
        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PersonalAutomationTool",
            "email_recipients.json"
        );

        public static EmailRecipientsConfig LoadRecipients(string trainType)
        {
            var allSettings = LoadAllSettings();
            if (allSettings.TryGetValue(trainType, out var config))
            {
                return config;
            }
            return new EmailRecipientsConfig();
        }

        public static void SaveRecipients(string trainType, EmailRecipientsConfig config)
        {
            var allSettings = LoadAllSettings();
            allSettings[trainType] = config;
            SaveAllSettings(allSettings);
        }

        private static Dictionary<string, EmailRecipientsConfig> LoadAllSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<Dictionary<string, EmailRecipientsConfig>>(json) ?? new Dictionary<string, EmailRecipientsConfig>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading email settings: {ex.Message}");
            }
            return new Dictionary<string, EmailRecipientsConfig>();
        }

        private static void SaveAllSettings(Dictionary<string, EmailRecipientsConfig> settings)
        {
            try
            {
                string directory = Path.GetDirectoryName(SettingsFile)!;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving email settings: {ex.Message}");
            }
        }
    }
}
