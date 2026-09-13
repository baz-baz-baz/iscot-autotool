using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2 (<see cref="FileSystemWatcher"/> reale su una cartella temporanea): l'aggiornamento
    /// automatico dei moduli su LOG &amp; DUMP, che nella 2.0.0 distribuita non partiva mai
    /// (§6.1-tricies-semel di PROJECT_MEMORY.md).
    ///
    /// <para>
    /// <b>Cosa non si può verificare qui, e dove è stato verificato.</b> La consegna della notifica sul
    /// dispatcher WPF: xUnit non ha un'<c>Application</c>, quindi
    /// <see cref="AppWatcher.EseguiSuThreadUi"/> è sostituito da un'esecuzione diretta. Il percorso
    /// completo (evento su disco → HOME ridisegnata) è stato verificato sul pacchetto pubblicato.
    /// </para>
    ///
    /// <para>
    /// <see cref="AppWatcher"/> è statico: questa classe ne ripristina lo stato in <c>Dispose</c>, e
    /// condivide la collection con <see cref="CrashReporterTests"/> perché dirotta il log degli errori
    /// su un file temporaneo invece del <c>crash.log</c> reale della macchina.
    /// </para>
    /// </summary>
    [Collection("CrashReporterLog")]
    public sealed class AppWatcherTests : IDisposable
    {
        private static readonly TimeSpan AttesaMassima = TimeSpan.FromSeconds(10);

        /// <summary>Oltre la finestra di debounce (500 ms): basta a far emergere notifiche tardive o doppie.</summary>
        private static readonly TimeSpan OltreIlDebounce = TimeSpan.FromSeconds(1.5);

        private readonly string _radice;
        private readonly string _logDump;
        private readonly string _logErrori;
        private readonly Action<Action> _consegnaOriginale;
        private readonly List<Action> _sottoscrizioni = [];

        public AppWatcherTests()
        {
            _radice = Path.Combine(Path.GetTempPath(), "PatTests_AppWatcher_" + Guid.NewGuid().ToString("N"));
            _logDump = Path.Combine(_radice, "LOG & DUMP");
            _logErrori = Path.Combine(_radice, "crash.log");
            Directory.CreateDirectory(_logDump);

            _consegnaOriginale = AppWatcher.EseguiSuThreadUi;
            AppWatcher.EseguiSuThreadUi = azione => azione();
            CrashReporter.PercorsoLogSostitutivo = _logErrori;
        }

        public void Dispose()
        {
            AppWatcher.Stop();
            foreach (Action sottoscrizione in _sottoscrizioni) AppWatcher.OnLogDumpFolderChanged -= sottoscrizione;
            AppWatcher.EseguiSuThreadUi = _consegnaOriginale;
            CrashReporter.PercorsoLogSostitutivo = null;

            try
            {
                Directory.Delete(_radice, true);
            }
            catch { /* pulizia best-effort */ }
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Initialize_SenzaCartellaFallisceSubitoInveceDiSpegnereLAggiornamentoInSilenzio(string cartella)
        {
            // Il difetto della 2.0.0: MainWindow chiamava Initialize prima di AppConfig.Initialize(), con la
            // cartella ancora vuota. L'eccezione veniva inghiottita e il watcher non partiva mai.
            var eccezione = Assert.Throws<InvalidOperationException>(() => AppWatcher.Initialize(cartella));

            Assert.Contains("AppConfig.Initialize", eccezione.Message);
        }

        [Fact]
        public async Task LaCreazioneDiUnaCartellaLogNotificaISottoscrittori()
        {
            int notifiche = 0;
            Sottoscrivi(() => Interlocked.Increment(ref notifiche));
            AppWatcher.Initialize(_logDump);

            Directory.CreateDirectory(Path.Combine(_logDump, "ETR700 12", "SR1247654 LOG ETR700 117 04.02HR 300526 Todde"));

            Assert.True(await AttendiFinoA(() => notifiche > 0, AttesaMassima));
        }

        [Fact]
        public async Task LaRinominaDiUnFileNotificaISottoscrittori()
        {
            // "Rinomina PDF" e il salvataggio di Excel (file temporaneo → rinomina) arrivano come Renamed.
            string madre = Path.Combine(_logDump, "E404P 30");
            Directory.CreateDirectory(madre);
            string pdf = Path.Combine(madre, "scansione.pdf");
            File.WriteAllText(pdf, "%PDF");

            int notifiche = 0;
            Sottoscrivi(() => Interlocked.Increment(ref notifiche));
            AppWatcher.Initialize(_logDump);

            File.Move(pdf, Path.Combine(madre, "FL SR1247654 E404P 627 IMC AV Milano 300526 Todde.pdf"));

            Assert.True(await AttendiFinoA(() => notifiche > 0, AttesaMassima));
        }

        [Fact]
        public async Task UnaRafficaDiEventiProduceUnSoloAggiornamento()
        {
            int notifiche = 0;
            Sottoscrivi(() => Interlocked.Increment(ref notifiche));
            AppWatcher.Initialize(_logDump);

            // Come la copia di una cartella di log dal treno: decine di eventi in pochi millisecondi.
            string cartellaLog = Path.Combine(_logDump, "ETR1000 5", "SR1300001 LOG ETR1000 5 01.01 140926 Rossi");
            Directory.CreateDirectory(cartellaLog);
            for (int i = 0; i < 50; i++)
            {
                File.WriteAllText(Path.Combine(cartellaLog, $"log_{i:D3}.bin"), "x");
            }

            Assert.True(await AttendiFinoA(() => notifiche > 0, AttesaMassima));
            await Task.Delay(OltreIlDebounce);

            // 52 eventi su disco, al massimo 2 ricaricamenti dei moduli (il secondo solo come tolleranza per
            // una macchina rallentata dal resto della suite).
            Assert.InRange(notifiche, 1, 2);
        }

        [Fact]
        public async Task UnSottoscrittoreCheLanciaNonBloccaGliAltriEFinisceNelLogErrori()
        {
            int notificheSecondo = 0;
            Sottoscrivi(() => throw new IOException("cartella bloccata da OneDrive"));
            Sottoscrivi(() => Interlocked.Increment(ref notificheSecondo));
            AppWatcher.Initialize(_logDump);

            Directory.CreateDirectory(Path.Combine(_logDump, "ETR1000 7"));

            Assert.True(await AttendiFinoA(() => notificheSecondo > 0, AttesaMassima));
            Assert.True(await AttendiFinoA(() => LeggiLogErrori().Contains("cartella bloccata da OneDrive"), AttesaMassima));
        }

        [Fact]
        public async Task DopoStopNessunaNotifica()
        {
            int notifiche = 0;
            Sottoscrivi(() => Interlocked.Increment(ref notifiche));
            AppWatcher.Initialize(_logDump);

            AppWatcher.Stop();
            Directory.CreateDirectory(Path.Combine(_logDump, "ETR421 1"));
            await Task.Delay(OltreIlDebounce);

            Assert.Equal(0, notifiche);
        }

        [Fact]
        public async Task InitializeRipetutoNonDuplicaLeNotifiche()
        {
            int notifiche = 0;
            Sottoscrivi(() => Interlocked.Increment(ref notifiche));

            AppWatcher.Initialize(_logDump);
            AppWatcher.Initialize(_logDump);

            Directory.CreateDirectory(Path.Combine(_logDump, "ETR522 2"));

            Assert.True(await AttendiFinoA(() => notifiche > 0, AttesaMassima));
            await Task.Delay(OltreIlDebounce);
            Assert.Equal(1, notifiche);
        }

        // ------------------------------------------------------------------
        // Supporto
        // ------------------------------------------------------------------

        private void Sottoscrivi(Action sottoscrizione)
        {
            _sottoscrizioni.Add(sottoscrizione);
            AppWatcher.OnLogDumpFolderChanged += sottoscrizione;
        }

        private string LeggiLogErrori()
        {
            try
            {
                if (!File.Exists(_logErrori)) return string.Empty;
                using var stream = new FileStream(_logErrori, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }

        private static async Task<bool> AttendiFinoA(Func<bool> condizione, TimeSpan limite)
        {
            var cronometro = Stopwatch.StartNew();
            while (cronometro.Elapsed < limite)
            {
                if (condizione()) return true;
                await Task.Delay(25);
            }
            return condizione();
        }
    }
}
