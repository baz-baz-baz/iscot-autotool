using System;
using System.IO;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2: <c>HitachiPathsManager</c> legge/scrive <c>hitachi_paths.json</c> in
    /// <c>AppDomain.CurrentDomain.BaseDirectory</c> (nessun parametro per un percorso alternativo,
    /// a differenza di <c>RenamerLog</c>), quindi questi test operano sul file reale nella cartella
    /// di output di <c>PersonalAutomationTool.Tests</c> — mai sull'installazione dell'applicazione.
    /// Ogni test elimina un eventuale file preesistente prima di partire: <c>!File.Exists</c> fa sì
    /// che <c>LoadConfig()</c> generi la configurazione di default senza passare dalla cache interna
    /// (invalidata comunque da <c>SaveConfig</c>), quindi il risultato non dipende dall'ordine di
    /// esecuzione degli altri test.
    /// </summary>
    /// <remarks>
    /// <b>Perché una collection dedicata.</b> xUnit esegue in parallelo classi di test appartenenti a
    /// collection diverse. Questa classe è l'unica che manipola uno stato **globale**: il file
    /// <c>hitachi_paths.json</c> nella cartella di output condivisa, più la cache statica interna di
    /// <see cref="HitachiPathsManager"/>. Cancellarlo e ricrearlo mentre un altro test in parallelo
    /// legge la stessa configurazione produrrebbe fallimenti sporadici e non riproducibili — la
    /// classe di instabilità più fastidiosa da diagnosticare a posteriori. Isolarla in una collection
    /// propria la serializza rispetto a chiunque altro dichiari la stessa collection.
    /// </remarks>
    [Collection("SharedBaseDirectoryState")]
    public sealed class HitachiPathsManagerTests : IDisposable
    {
        private readonly string _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hitachi_paths.json");
        private readonly string? _preexistingContent;

        public HitachiPathsManagerTests()
        {
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
            catch { /* best-effort cleanup */ }
        }

        /// <summary>
        /// Regressione mirata: la cartella "ITA-FR" senza la "A" finale (nome reale su disco,
        /// confermato dal committente dopo l'errore "Cartella Hitachi non trovata" in produzione).
        /// Il valore precedente, "ETR1000 ITA-FRA", superava la build e i test esistenti — nessuno
        /// verificava il contenuto della configurazione di default — ed è rimasto sbagliato fino a
        /// una segnalazione a runtime. Questo test blocca la stessa classe di regressione in futuro.
        /// </summary>
        [Fact]
        public void GetHitachiDir_Etr1000IF_UsaLaCartellaItaFrSenzaLaAFinale()
        {
            string? path = HitachiPathsManager.GetHitachiDir(@"C:\Users\utente", "ETR1000 I-F");

            Assert.NotNull(path);
            Assert.EndsWith(Path.Combine("SSB_SST - Interventi ETR1000", "ETR1000 ITA-FR"), path);
            Assert.DoesNotContain("ITA-FRA", path);
        }

        [Fact]
        public void GetHitachiDir_Etr1000IF_CartellaEDirettamenteDentroInterventiEtr1000()
        {
            // Stessa cartella "Interventi ETR1000" della voce non-I-F: solo il sotto-percorso finale
            // cambia. Un errore qui manderebbe "Sposta/Riporta Report" nell'albero sbagliato.
            string basePath = HitachiPathsManager.GetHitachiDir(@"C:\Users\utente", "ETR1000 / 1000FH")!;
            string ifPath = HitachiPathsManager.GetHitachiDir(@"C:\Users\utente", "ETR1000 I-F")!;

            Assert.Equal(Path.Combine(basePath, "ETR1000 ITA-FR"), ifPath);
        }

        [Fact]
        public void GetHitachiDir_TrenoNonConfigurato_RestituisceNull()
        {
            Assert.Null(HitachiPathsManager.GetHitachiDir(@"C:\Users\utente", "Treno Inesistente"));
        }

        [Fact]
        public void LoadConfig_ContieneUnaVoceDistintaPerCiascunaDelleQuattroEtichette()
        {
            var config = HitachiPathsManager.LoadConfig();

            Assert.Equal(4, config.Count);
            Assert.Contains(config, c => c.Train == "ETR700");
            Assert.Contains(config, c => c.Train == "E404P");
            Assert.Contains(config, c => c.Train == "ETR1000 / 1000FH");
            Assert.Contains(config, c => c.Train == "ETR1000 I-F");
        }
    }
}
