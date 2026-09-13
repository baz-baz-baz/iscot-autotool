using System;
using System.IO;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 2 (file veri, in una cartella temporanea): il log degli errori di <see cref="CrashReporter"/>,
    /// la rete di sicurezza introdotta dopo le chiusure silenziose della 2.0.0 (§6.1-tricies-semel di
    /// PROJECT_MEMORY.md).
    ///
    /// <para>
    /// <b>Cosa conta davvero qui.</b> Il log è l'unica fonte di diagnosi su una workstation d'officina:
    /// deve contenere lo stack trace <i>completo</i> con l'intera catena delle eccezioni interne, non deve
    /// mai perdere le voci precedenti, e soprattutto <b>non deve mai lanciare</b> — un log che fallisce
    /// dentro un gestore di crash diventerebbe a sua volta un crash. I tre intercettori globali in sé non
    /// sono testabili da xUnit (servono un'<c>Application</c> WPF e un processo che muore): sono stati
    /// verificati sul pacchetto pubblicato.
    /// </para>
    ///
    /// <para>
    /// Nessun test scrive nel <c>crash.log</c> reale: ogni scrittura usa un percorso esplicito sotto
    /// <c>%TEMP%</c>. Collection condivisa con <see cref="AppWatcherTests"/>, che dirotta temporaneamente
    /// <see cref="CrashReporter.PercorsoLogSostitutivo"/>: in parallelo, il test sul percorso reale
    /// leggerebbe quello dirottato.
    /// </para>
    /// </summary>
    [Collection("CrashReporterLog")]
    public sealed class CrashReporterTests : IDisposable
    {
        private readonly string _cartella =
            Path.Combine(Path.GetTempPath(), "PatTests_CrashReporter_" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_cartella)) Directory.Delete(_cartella, true);
            }
            catch { /* pulizia best-effort */ }
        }

        [Fact]
        public void LaVoceContieneMomentoOrigineTipoMessaggioEStackTrace()
        {
            Exception eccezione = EccezioneAnnidataLanciataDavvero();
            var momento = new DateTimeOffset(2026, 9, 14, 8, 30, 15, 123, TimeSpan.FromHours(2));

            string voce = CrashReporter.FormattaVoce("ERRORE NON GESTITO", "Thread UI (DispatcherUnhandledException)", eccezione, momento);

            Assert.Contains("2026-09-14 08:30:15.123 +02:00", voce);
            Assert.Contains("ERRORE NON GESTITO", voce);
            Assert.Contains("Thread UI (DispatcherUnhandledException)", voce);
            Assert.Contains("System.InvalidOperationException: Scrittura del report non riuscita", voce);
            // Lo stack trace, non solo il messaggio: è ciò che permette di trovare la riga colpevole.
            Assert.Contains(nameof(LanciaEccezioneAnnidata), voce);
        }

        [Fact]
        public void LaVoceIncludeLInteraCatenaDelleEccezioniInterne()
        {
            string voce = CrashReporter.FormattaVoce("ERRORE", "test", EccezioneAnnidataLanciataDavvero(), DateTimeOffset.Now);

            Assert.Contains("System.IO.IOException: file bloccato da SharePoint", voce);
        }

        [Fact]
        public void LaVoceElencaTutteLeEccezioniDiUnAggregateException()
        {
            // È la forma in cui arrivano le eccezioni di TaskScheduler.UnobservedTaskException.
            var aggregata = new AggregateException(
                new IOException("primo errore"), new UnauthorizedAccessException("secondo errore"));

            string voce = CrashReporter.FormattaVoce("ERRORE", "Task non atteso", aggregata, DateTimeOffset.Now);

            Assert.Contains("primo errore", voce);
            Assert.Contains("secondo errore", voce);
        }

        [Fact]
        public void LaVoceRiportaIlContestoPerDiagnosticareSenzaChiedereNullaAlTecnico()
        {
            string voce = CrashReporter.FormattaVoce("ERRORE", "test", new Exception("x"), DateTimeOffset.Now);

            Assert.Contains("Versione", voce);
            Assert.Contains("Processo", voce);
            Assert.Contains("Sistema", voce);
            Assert.Contains("Cartella dati", voce);
            Assert.Contains("LOG & DUMP", voce);
        }

        [Fact]
        public void LaVoceSenzaEccezioneNonLancia()
        {
            string voce = CrashReporter.FormattaVoce("ANOMALIA GESTITA", "watcher non avviato", null, DateTimeOffset.Now);

            Assert.Contains("(nessuna eccezione associata)", voce);
        }

        [Fact]
        public void ScriviVoce_CreaLaCartellaMancanteEAccodaSenzaSovrascrivere()
        {
            string log = Path.Combine(_cartella, "sottocartella", "crash.log");

            Assert.True(CrashReporter.ScriviVoce(log, "PRIMA\n", 1024 * 1024));
            Assert.True(CrashReporter.ScriviVoce(log, "SECONDA\n", 1024 * 1024));

            Assert.Equal("PRIMA\nSECONDA\n", File.ReadAllText(log));
        }

        [Fact]
        public void ScriviVoce_OltreLaDimensioneMassimaRuotaIlLogInOldLog()
        {
            string log = Path.Combine(_cartella, "crash.log");
            CrashReporter.ScriviVoce(log, new string('a', 2000), 1024);

            CrashReporter.ScriviVoce(log, "NUOVA VOCE", 1024);

            Assert.Equal("NUOVA VOCE", File.ReadAllText(log));
            Assert.Equal(new string('a', 2000), File.ReadAllText(Path.Combine(_cartella, "crash.old.log")));
        }

        [Fact]
        public void ScriviVoce_FunzionaAncheConIlLogApertoInLetturaDaUnAltroProgramma()
        {
            // Il tecnico (o chi assiste) può tenere crash.log aperto nel Blocco note mentre l'app scrive.
            string log = Path.Combine(_cartella, "crash.log");
            CrashReporter.ScriviVoce(log, "PRIMA\n", 1024 * 1024);

            using var lettore = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            Assert.True(CrashReporter.ScriviVoce(log, "SECONDA\n", 1024 * 1024));
        }

        [Fact]
        public void ScriviVoce_SuUnPercorsoNonScrivibileRestituisceFalseSenzaLanciare()
        {
            // Una cartella con il nome del file di log: l'apertura in scrittura fallisce sempre.
            string log = Path.Combine(_cartella, "crash.log");
            Directory.CreateDirectory(log);

            Assert.False(CrashReporter.ScriviVoce(log, "voce", 1024));
        }

        [Fact]
        public void IlMessaggioAVideoMostraLaCausaPiuInternaEDoveTrovareLoStackTrace()
        {
            const string percorsoLog = @"C:\Users\tecnico\AppData\Local\iscot-autotool\crash.log";

            string testo = CrashReporter.ComponiMessaggio(EccezioneAnnidataLanciataDavvero(), percorsoLog, fatale: false);

            Assert.Contains("InvalidOperationException: Scrittura del report non riuscita", testo);
            Assert.Contains("Causa: IOException: file bloccato da SharePoint", testo);
            Assert.Contains(percorsoLog, testo);
            Assert.Contains("resta aperta", testo);
        }

        [Fact]
        public void IlMessaggioFataleAnnunciaLaChiusuraESegnalaIlLogNonScritto()
        {
            string testo = CrashReporter.ComponiMessaggio(new Exception("errore senza cause"), percorsoLog: null, fatale: true);

            Assert.Contains("verrà chiusa", testo);
            Assert.Contains("Non è stato possibile salvare", testo);
            Assert.DoesNotContain("Causa:", testo);
        }

        [Fact]
        public void IlLogRealeStaInLocalAppDataIscotAutotool()
        {
            string atteso = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "iscot-autotool", "crash.log");

            Assert.Equal(atteso, CrashReporter.PercorsoLog, ignoreCase: true);
        }

        // ------------------------------------------------------------------
        // Supporto
        // ------------------------------------------------------------------

        /// <summary>Un'eccezione davvero lanciata (quindi con stack trace) che ne avvolge un'altra.</summary>
        private static Exception EccezioneAnnidataLanciataDavvero()
        {
            try
            {
                LanciaEccezioneAnnidata();
            }
            catch (Exception ex)
            {
                return ex;
            }

            throw new InvalidOperationException("irraggiungibile");
        }

        private static void LanciaEccezioneAnnidata()
        {
            try
            {
                throw new IOException("file bloccato da SharePoint");
            }
            catch (IOException io)
            {
                throw new InvalidOperationException("Scrittura del report non riuscita", io);
            }
        }
    }
}
