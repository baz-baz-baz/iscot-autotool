using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Email.Dialogs;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Email
{
    /// <summary>
    /// Copre lo stesso rename "ETR1000FH" → "ETR1001FH" di
    /// <see cref="PersonalAutomationTool.Tests.Modules.DestinatariMail.DestinatariManagerEtr1001FhTests"/>,
    /// ma sul lato shortcuts.json: senza la migrazione, uno shortcuts.json già in uso con la vecchia
    /// chiave farebbe ripiegare <see cref="ShortcutsManager.GetShortcutsForTrain"/> sui default
    /// generici invece delle scorciatoie scelte dal tecnico (non un errore, ma una regressione
    /// silenziosa dell'esperienza d'uso).
    /// <para>
    /// Stesso schema backup/restore di <see cref="PersonalAutomationTool.Tests.Core.HitachiPathsManagerTests"/>:
    /// <c>shortcuts.json</c> vive sotto <c>%APPDATA%\PersonalAutomationTool</c>, lo stesso percorso
    /// letto dall'applicazione installata.
    /// </para>
    /// </summary>
    [Collection("SharedAppDataState")]
    public sealed class ShortcutsManagerEtr1001FhTests : IDisposable
    {
        private readonly string _configPath;
        private readonly string? _preexistingContent;

        public ShortcutsManagerEtr1001FhTests()
        {
            AppPaths.Initialize();
            _configPath = AppPaths.DataFile("shortcuts.json");

            if (File.Exists(_configPath))
            {
                _preexistingContent = File.ReadAllText(_configPath);
                File.Delete(_configPath);
            }
        }

        public void Dispose()
        {
            try
            {
                if (_preexistingContent != null) File.WriteAllText(_configPath, _preexistingContent);
                else if (File.Exists(_configPath)) File.Delete(_configPath);
            }
            catch { /* pulizia best-effort */ }
        }

        [Fact]
        public void LoadConfig_MigratesLegacyEtr1000FHEntry_PreservingCustomShortcuts()
        {
            File.WriteAllText(_configPath, """
                [
                  { "TrainName": "ETR1000FH", "Shortcuts": ["Scorciatoia personalizzata"] }
                ]
                """);

            var config = ShortcutsManager.LoadConfig();

            Assert.DoesNotContain(config, t => t.TrainName.Equals("ETR1000FH", StringComparison.OrdinalIgnoreCase));
            var migrated = config.FirstOrDefault(t => t.TrainName == "ETR1001FH");
            Assert.NotNull(migrated);
            Assert.Equal(["Scorciatoia personalizzata"], migrated!.Shortcuts);

            string savedJson = File.ReadAllText(_configPath);
            Assert.DoesNotContain("ETR1000FH", savedJson);
        }

        [Fact]
        public void LoadConfig_DefaultConfig_UsesEtr1001FH()
        {
            var config = ShortcutsManager.LoadConfig();

            Assert.Contains(config, t => t.TrainName == "ETR1001FH");

            var shortcuts = ShortcutsManager.GetShortcutsForTrain("ETR1001FH");
            Assert.NotEmpty(shortcuts);
        }

        [Fact]
        public void MigrateLegacyTrainName_DoesNothing_WhenNewNameAlreadyPresent()
        {
            var config = new List<TrainShortcutsModel>
            {
                new() { TrainName = "ETR1000FH", Shortcuts = ["Vecchia"] },
                new() { TrainName = "ETR1001FH", Shortcuts = ["Nuova"] }
            };

            bool migrated = ShortcutsManager.MigrateLegacyTrainName(config, "ETR1000FH", "ETR1001FH");

            Assert.False(migrated);
            Assert.Equal(2, config.Count);
        }
    }
}
