using System;
using System.IO;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2: <c>HitachiPathsManager</c> legge/scrive <c>hitachi_paths.json</c> in
    /// <c>Core.AppPaths.DataFolder</c> (<c>%APPDATA%\PersonalAutomationTool</c> dallo Sprint 16,
    /// §6.1-duodevicies di PROJECT_MEMORY.md — prima era <c>AppDomain.CurrentDomain.BaseDirectory</c>,
    /// e questi test operavano su un file isolato nella cartella di output di
    /// <c>PersonalAutomationTool.Tests</c>, mai sull'installazione reale). Da quel percorso in poi
    /// **non esiste più un "posto sicuro per i test"** distinto dalla configurazione reale del
    /// tecnico: il file che questi test toccano è lo stesso che legge/scrive l'applicazione
    /// installata. Il test resta comunque non distruttivo perché fa esattamente ciò che faceva già
    /// prima — backup del contenuto preesistente prima di ogni test, ripristino dopo — solo ora sul
    /// percorso vero invece che su una copia isolata. <c>AppPaths.Initialize()</c> in ogni test è
    /// necessario perché il percorso dipenda in modo deterministico da <c>%APPDATA%</c> e non
    /// dall'ordine casuale con cui gira il resto della suite (vedi il <c>remarks</c> sotto).
    /// </summary>
    /// <remarks>
    /// <b>Perché una collection dedicata.</b> xUnit esegue in parallelo classi di test appartenenti a
    /// collection diverse. Questa classe manipola uno stato **globale**: il file reale
    /// <c>hitachi_paths.json</c> sotto <c>%APPDATA%</c>, più la cache statica interna di
    /// <see cref="HitachiPathsManager"/>. Cancellarlo e ricrearlo mentre un altro test in parallelo
    /// legge la stessa configurazione produrrebbe fallimenti sporadici e non riproducibili — la
    /// classe di instabilità più fastidiosa da diagnosticare a posteriori. Isolarla in una collection
    /// propria la serializza rispetto a chiunque altro dichiari la stessa collection.
    /// </remarks>
    [Collection("SharedAppDataState")]
    public sealed class HitachiPathsManagerTests : IDisposable
    {
        private readonly string _configPath;
        private readonly string? _preexistingContent;

        public HitachiPathsManagerTests()
        {
            // Idempotente: se un altro test lo ha già chiamato in questo processo, non fa nulla di
            // nuovo. Chiamarlo comunque qui rende _configPath deterministico indipendentemente
            // dall'ordine con cui xUnit esegue le altre classi.
            AppPaths.Initialize();
            _configPath = AppPaths.DataFile("hitachi_paths.json");

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

        // ------------------------------------------------------------------
        // GetReportOldFolder — estratta dall'inline di ExecuteSpostaReport (Sprint 17,
        // §6.1-undevicies) perché anche PathHealthCheckService deve conoscere questi stessi percorsi.
        // ------------------------------------------------------------------

        [Fact]
        public void GetReportOldFolder_Etr700_IncludeLAnnoESottocartellaDedicata()
        {
            string? path = HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "ETR700", 2026);

            Assert.NotNull(path);
            Assert.EndsWith(Path.Combine("REPORT INTERVENTI ETR700 OLD", "REPORT OLD ETR700 ANNO 2026"), path);
        }

        [Fact]
        public void GetReportOldFolder_E404P_IncludeLAnnoNelNomeDellaCartella()
        {
            string? path = HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "E404P", 2026);

            Assert.NotNull(path);
            Assert.EndsWith("REPORT INTERVENTI OLD_ModifyYear2026", path);
        }

        [Fact]
        public void GetReportOldFolder_Etr1000EFh_NonDipendeDallAnno()
        {
            string? conAnnoCorrente = HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "ETR1000 / 1000FH", 2026);
            string? conAltroAnno = HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "ETR1000 / 1000FH", 2030);

            Assert.NotNull(conAnnoCorrente);
            Assert.EndsWith("OLD REPORT", conAnnoCorrente);
            Assert.Equal(conAnnoCorrente, conAltroAnno);
        }

        [Fact]
        public void GetReportOldFolder_Etr1000IF_UsaLaGraficaMaiuscolaDistintaDaEtr1000EFh()
        {
            // "OLD Report" (I-F) contro "OLD REPORT" (ETR1000/1000FH): differiscono solo per
            // maiuscole/minuscole, esattamente come nel codice sorgente originale — un dettaglio
            // facile da appiattire per errore in un refactor.
            string? path = HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "ETR1000 I-F", 2026);

            Assert.NotNull(path);
            Assert.EndsWith("OLD Report", path);
        }

        [Fact]
        public void GetReportOldFolder_TrenoNonConfigurato_RestituisceNull()
        {
            Assert.Null(HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "Treno Inesistente", 2026));
        }

        [Fact]
        public void GetReportOldFolder_StaSottoLaStessaCartellaHitachiDiGetHitachiDir()
        {
            // Le due funzioni devono restare coerenti fra loro: GetReportOldFolder aggiunge solo la
            // sottocartella "vecchi report" sopra la stessa base che EXCEL usa per il report corrente.
            string hitachiDir = HitachiPathsManager.GetHitachiDir(@"C:\Users\utente", "ETR700")!;
            string oldFolder = HitachiPathsManager.GetReportOldFolder(@"C:\Users\utente", "ETR700", 2026)!;

            Assert.StartsWith(hitachiDir, oldFolder);
        }
    }
}
