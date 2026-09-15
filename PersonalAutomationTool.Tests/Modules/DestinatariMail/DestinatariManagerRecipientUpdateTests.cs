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
    /// Copre l'aggiornamento dei destinatari "Passaggio di consegne" per E404P (ETR500), ETR700 ed
    /// ETR1000 (Sprint 33, PROJECT_MEMORY.md §6.1-tricies-quinquies). Come per il rename
    /// "ETR1000FH" → "ETR1001FH" (<see cref="DestinatariManagerEtr1001FhTests"/>), un
    /// <c>destinatari.json</c> già in uso su una macchina reale deve ricevere il nuovo valore SOLO se
    /// è ancora quello di default precedente — una personalizzazione fatta a mano dal tecnico su
    /// questa stessa azione non deve essere sovrascritta.
    /// </summary>
    [Collection("SharedAppDataState")]
    public sealed class DestinatariManagerRecipientUpdateTests : IDisposable
    {
        private readonly string _configPath;
        private readonly string? _preexistingContent;

        public DestinatariManagerRecipientUpdateTests()
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

        [Theory]
        [InlineData("E404P", "Service_ISCOT_IMC_AV_Milano@it.iscot.com",
            "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; matteo.masciocchi@iscot.it")]
        [InlineData("ETR700", "Service_ISCOT_IMC_AV_Milano@it.iscot.com",
            "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; matteo.masciocchi@iscot.it; mario.arcini@hitachirail.com")]
        [InlineData("ETR1000", "Service_ISCOT_IMC_AV_Milano@it.iscot.com",
            "vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; matteo.masciocchi@iscot.it; mario.arcini@hitachirail.com")]
        public void LoadConfig_DefaultConfig_UsaINuoviDestinatariPassaggioDiConsegne(string trainName, string expectedTo, string expectedCc)
        {
            // Nessun file esistente: LoadConfig genera il default, che deve già usare i nuovi valori.
            var config = DestinatariManager.LoadConfig();

            var azione = config.Single(t => t.TrainName == trainName).Actions
                .Single(a => a.ActionName == "Passaggio di consegne");

            Assert.Equal(expectedTo, azione.ToRecipients);
            Assert.Equal(expectedCc, azione.CcRecipients);
        }

        [Fact]
        public void LoadConfig_AggiornaAzioneAlValoreVecchio_EPersisteSuDisco()
        {
            // Simula un destinatari.json reale, ancora al valore di default precedente il fix.
            File.WriteAllText(_configPath, """
                [
                  {
                    "TrainName": "ETR700",
                    "Actions": [
                      { "ActionName": "Passaggio di consegne", "ToRecipients": "etr700_analisidiagssb_sts@hitachirail.com", "CcRecipients": "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com" }
                    ]
                  }
                ]
                """);

            var config = DestinatariManager.LoadConfig();

            var azione = config.Single(t => t.TrainName == "ETR700").Actions.Single();
            Assert.Equal("Service_ISCOT_IMC_AV_Milano@it.iscot.com", azione.ToRecipients);
            Assert.Equal("vincenzo.loporchio@hitachirail.com; alfredo.foti@hitachirail.com; matteo.masciocchi@iscot.it; mario.arcini@hitachirail.com", azione.CcRecipients);

            // Persistito, non solo in memoria: una lettura successiva deve vedere già il nuovo valore.
            string savedJson = File.ReadAllText(_configPath);
            Assert.Contains("matteo.masciocchi@iscot.it", savedJson);
            Assert.DoesNotContain("etr700_analisidiagssb_sts@hitachirail.com", savedJson);
        }

        [Fact]
        public void ApplyKnownRecipientUpdates_NonTocca_UnaPersonalizzazioneDelTecnico()
        {
            // Il tecnico ha già personalizzato a mano questa azione: il valore non coincide né con
            // il vecchio default né con il nuovo, quindi non deve essere toccato.
            var config = new ObservableCollection<TrainConfig>
            {
                new()
                {
                    TrainName = "ETR700",
                    Actions =
                    [
                        new() { ActionName = "Passaggio di consegne", ToRecipients = "tecnico.custom@hitachirail.com", CcRecipients = "" }
                    ]
                }
            };

            bool changed = DestinatariManager.ApplyKnownRecipientUpdates(config);

            Assert.False(changed);
            var azione = config.Single().Actions.Single();
            Assert.Equal("tecnico.custom@hitachirail.com", azione.ToRecipients);
            Assert.Equal("", azione.CcRecipients);
        }

        [Fact]
        public void ApplyKnownRecipientUpdates_NonTocca_ETR1000IFEETR1001FH()
        {
            // Il committente ha chiesto l'aggiornamento solo per E404P, ETR700 ed ETR1000: le altre
            // due flotte del report ETR1000 (ETR1000IF, ETR1001FH) devono restare come prima.
            const string oldTo = "etr1000_analisidiagssb_sts@hitachirail.com";
            const string oldCc = "Service_ISCOT_IMC_AV_Milano@it.iscot.com; vincenzo.loporchio@hitachirail.com; salvatore.cascegna@hitachirail.com; francesco.montanaro@hitachirail.com; team-adv@advservicesrl.it; salvatore.demartino@hitachirail.com; mario.arcini@hitachirail.com";

            var config = new ObservableCollection<TrainConfig>
            {
                new() { TrainName = "ETR1000IF", Actions = [new() { ActionName = "Passaggio di consegne", ToRecipients = oldTo, CcRecipients = oldCc }] },
                new() { TrainName = "ETR1001FH", Actions = [new() { ActionName = "Passaggio di consegne", ToRecipients = oldTo, CcRecipients = oldCc }] }
            };

            bool changed = DestinatariManager.ApplyKnownRecipientUpdates(config);

            Assert.False(changed);
            Assert.All(config, t =>
            {
                var azione = t.Actions.Single();
                Assert.Equal(oldTo, azione.ToRecipients);
                Assert.Equal(oldCc, azione.CcRecipients);
            });
        }
    }
}
