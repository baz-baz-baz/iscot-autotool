using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.DestinatariMail;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.DestinatariMail
{
    /// <summary>
    /// Copre il rename "ETR1000FH" → "ETR1001FH" (quest'ultimo è il valore reale della colonna
    /// `tipo` di flotte.db, PROJECT_MEMORY.md §5.3-bis, usato ora anche come etichetta visibile
    /// nella UI email). Due cose devono restare vere dopo il rename: un <c>destinatari.json</c> già
    /// in uso su una macchina reale (con la vecchia chiave e indirizzi personalizzati a mano) non
    /// deve perdere quei destinatari, e una configurazione nuova deve usare direttamente la chiave
    /// aggiornata.
    /// <para>
    /// Stesso schema backup/restore di <see cref="PersonalAutomationTool.Tests.Modules.PassaggioConsegne.AzioneDestinatariTests"/>:
    /// <c>destinatari.json</c> vive in <c>AppPaths.DataFolder</c> (<c>%LOCALAPPDATA%\iscot-autotool</c>), lo stesso percorso
    /// letto dall'applicazione installata.
    /// </para>
    /// </summary>
    [Collection("SharedAppDataState")]
    public sealed class DestinatariManagerEtr1001FhTests : IDisposable
    {
        private readonly string _configPath;
        private readonly string? _preexistingContent;

        public DestinatariManagerEtr1001FhTests()
        {
            AppPaths.Initialize();
            _configPath = AppPaths.DataFile("destinatari.json");

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
        public void LoadConfig_MigratesLegacyEtr1000FHEntry_PreservingCustomRecipients()
        {
            // Simula un destinatari.json reale già in uso, con la vecchia chiave e un indirizzo
            // personalizzato a mano dal tecnico (diverso dal default di GenerateDefaultConfig).
            File.WriteAllText(_configPath, """
                [
                  {
                    "TrainName": "ETR1000FH",
                    "Actions": [
                      { "ActionName": "Chiusura Ticket", "ToRecipients": "tecnico.custom@hitachirail.com", "CcRecipients": "" }
                    ]
                  }
                ]
                """);

            var config = DestinatariManager.LoadConfig();

            Assert.DoesNotContain(config, t => t.TrainName.Equals("ETR1000FH", StringComparison.OrdinalIgnoreCase));
            var migrated = config.FirstOrDefault(t => t.TrainName == "ETR1001FH");
            Assert.NotNull(migrated);
            Assert.Equal("tecnico.custom@hitachirail.com", migrated!.Actions.Single().ToRecipients);

            // La migrazione deve anche persistere su disco, non solo in memoria: una lettura
            // successiva (nuovo processo, nuovo avvio dell'app) deve vedere già la chiave nuova.
            string savedJson = File.ReadAllText(_configPath);
            Assert.DoesNotContain("ETR1000FH", savedJson);
        }

        [Fact]
        public void LoadConfig_DefaultConfig_UsesEtr1001FHAndResolvesRecipients()
        {
            // Nessun file esistente: LoadConfig genera il default, che deve già usare la chiave
            // aggiornata.
            var config = DestinatariManager.LoadConfig();

            Assert.Contains(config, t => t.TrainName == "ETR1001FH");

            var destinatari = DestinatariManager.GetRecipients("ETR1001FH", "Chiusura Ticket");
            Assert.NotNull(destinatari);
            Assert.False(string.IsNullOrWhiteSpace(destinatari!.ToRecipients));
        }

        [Fact]
        public void MigrateLegacyTrainName_DoesNothing_WhenNewNameAlreadyPresent()
        {
            // Difensivo: se per qualunque motivo esistessero già entrambe le chiavi, la migrazione
            // non deve sovrascrivere la voce nuova (già eventualmente personalizzata) con quella
            // legacy, né duplicare voci.
            var config = new ObservableCollection<TrainConfig>
            {
                new() { TrainName = "ETR1000FH", Actions = [] },
                new() { TrainName = "ETR1001FH", Actions = [] }
            };

            bool migrated = DestinatariManager.MigrateLegacyTrainName(config, "ETR1000FH", "ETR1001FH");

            Assert.False(migrated);
            Assert.Equal(2, config.Count);
        }

        [Fact]
        public void MigrateLegacyTrainName_DoesNothing_WhenLegacyNameAbsent()
        {
            var config = new ObservableCollection<TrainConfig>
            {
                new() { TrainName = "ETR1001FH", Actions = [] }
            };

            bool migrated = DestinatariManager.MigrateLegacyTrainName(config, "ETR1000FH", "ETR1001FH");

            Assert.False(migrated);
            Assert.Single(config);
        }
    }
}
