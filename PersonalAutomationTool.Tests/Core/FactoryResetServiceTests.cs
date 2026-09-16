using System;
using System.IO;
using PersonalAutomationTool.Core;
using Xunit;

namespace PersonalAutomationTool.Tests.Core
{
    /// <summary>
    /// Tier 1/2: le parti deterministiche del "Reset a fabbrica" (<see cref="FactoryResetService"/>) —
    /// la guardia sul percorso da spostare, il calcolo della cartella di backup e la scrittura
    /// dell'helper. Fuori dalla suite restano l'avvio del processo <c>.cmd</c> e il riavvio
    /// dell'eseguibile, per la stessa ragione già annotata in <c>AutoUpdateServiceTests</c>: il loro
    /// effetto è terminare l'applicazione, non osservabile da un test host.
    ///
    /// <para>
    /// <b>Perché la guardia sul percorso ha un test tutto suo.</b> L'helper riceve una cartella e la
    /// sposta senza fare domande. È l'unico punto del progetto in cui un percorso non risolto — la
    /// stringa vuota che <c>Environment.GetFolderPath</c> restituisce per una cartella speciale non
    /// esistente, il difetto già visto nello Sprint 29 — si tradurrebbe in uno spostamento di qualcosa
    /// che non è la cartella dell'applicazione.
    /// </para>
    /// </summary>
    public sealed class FactoryResetServiceTests : IDisposable
    {
        private readonly string _radice;

        public FactoryResetServiceTests()
        {
            _radice = Path.Combine(Path.GetTempPath(), "PatTests_FactoryReset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_radice);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_radice)) Directory.Delete(_radice, true);
            }
            catch { /* pulizia best-effort, come nelle altre suite su file veri */ }
        }

        [Fact]
        public void CartellaResettabile_AccettaLaCartellaDatiReale()
        {
            Assert.True(FactoryResetService.CartellaResettabile(AppPaths.CartellaDatiPredefinita));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CartellaResettabile_RifiutaUnPercorsoNonValorizzato(string? percorso)
        {
            // Il caso reale da cui protegge: DataFolder mai inizializzato (stringa vuota) oppure una
            // cartella speciale di Windows non risolvibile.
            Assert.False(FactoryResetService.CartellaResettabile(percorso));
        }

        [Fact]
        public void CartellaResettabile_RifiutaUnPercorsoRelativo()
        {
            Assert.False(FactoryResetService.CartellaResettabile(@"iscot-autotool"));
        }

        [Fact]
        public void CartellaResettabile_RifiutaUnaCartellaConUnAltroNome()
        {
            Assert.False(FactoryResetService.CartellaResettabile(@"C:\Users\tecnico\AppData\Local"));
            Assert.False(FactoryResetService.CartellaResettabile(@"C:\"));
        }

        [Fact]
        public void CartellaResettabile_AccettaQualunqueRadicePurcheLUltimoSegmentoSiaQuelloApplicativo()
        {
            string atteso = Path.GetFileName(AppPaths.CartellaDatiPredefinita);

            Assert.True(FactoryResetService.CartellaResettabile(Path.Combine(@"D:\altro\profilo", atteso)));
            Assert.True(FactoryResetService.CartellaResettabile(Path.Combine(@"D:\altro\profilo", atteso) + Path.DirectorySeparatorChar));
        }

        [Fact]
        public void ComponiPercorsoBackup_RestaFuoriDallaCartellaDiPartenza()
        {
            // L'invariante che rende possibile lo spostamento: "move" di una cartella dentro sé stessa
            // fallisce, e il reset si risolverebbe in un nulla di fatto silenzioso.
            string cartellaDati = Path.Combine(_radice, "iscot-autotool");

            string backup = FactoryResetService.ComponiPercorsoBackup(cartellaDati, new DateTime(2026, 9, 16, 14, 30, 5));

            Assert.Equal(_radice, Path.GetDirectoryName(backup));
            Assert.DoesNotContain(cartellaDati + Path.DirectorySeparatorChar, backup);
            Assert.Equal("iscot-autotool-backup-20260916143005", Path.GetFileName(backup));
        }

        [Fact]
        public void ComponiPercorsoBackup_DueResetNellaStessaCartellaNonSiSovrascrivono()
        {
            string cartellaDati = Path.Combine(_radice, "iscot-autotool");

            string primo = FactoryResetService.ComponiPercorsoBackup(cartellaDati, new DateTime(2026, 9, 16, 14, 30, 5));
            string secondo = FactoryResetService.ComponiPercorsoBackup(cartellaDati, new DateTime(2026, 9, 16, 14, 30, 6));

            Assert.NotEqual(primo, secondo);
        }

        [Fact]
        public void ComponiPercorsoBackup_IgnoraUnSeparatoreFinale()
        {
            string atteso = FactoryResetService.ComponiPercorsoBackup(
                Path.Combine(_radice, "iscot-autotool"), new DateTime(2026, 9, 16, 14, 30, 5));

            string conSeparatore = FactoryResetService.ComponiPercorsoBackup(
                Path.Combine(_radice, "iscot-autotool") + Path.DirectorySeparatorChar, new DateTime(2026, 9, 16, 14, 30, 5));

            Assert.Equal(atteso, conSeparatore);
        }

        [Fact]
        public void ScriviScriptHelper_ScriveUnCmdNellaCartellaIndicata()
        {
            string percorso = FactoryResetService.ScriviScriptHelper(_radice);

            Assert.True(File.Exists(percorso));
            Assert.Equal(_radice, Path.GetDirectoryName(percorso));
            Assert.Equal(".cmd", Path.GetExtension(percorso));
        }

        [Fact]
        public void ScriviScriptHelper_DueChiamateNonSiSovrascrivono()
        {
            // L'helper si auto-cancella a fine esecuzione: un nome fisso farebbe collidere due reset
            // ravvicinati sulla stessa macchina.
            string primo = FactoryResetService.ScriviScriptHelper(_radice);
            string secondo = FactoryResetService.ScriviScriptHelper(_radice);

            Assert.NotEqual(primo, secondo);
        }

        [Fact]
        public void ScriviScriptHelper_LoScriptAttendeIlProcessoSpostaLaCartellaERiavvia()
        {
            string contenuto = File.ReadAllText(FactoryResetService.ScriviScriptHelper(_radice));

            // Le tre righe portanti, nell'ordine che rende sicura l'operazione: prima la fine del
            // processo (altrimenti i .db sono ancora agganciati), poi lo spostamento, poi il riavvio.
            int attesa = contenuto.IndexOf("tasklist", StringComparison.Ordinal);
            int spostamento = contenuto.IndexOf(@"move ""%DATAFOLDER%"" ""%BACKUPFOLDER%""", StringComparison.Ordinal);
            int riavvio = contenuto.IndexOf(@"start """" ""%TARGETEXE%""", StringComparison.Ordinal);

            Assert.True(attesa >= 0, "lo script non attende la terminazione del processo");
            Assert.True(spostamento > attesa, "lo spostamento non avviene dopo l'attesa del processo");
            Assert.True(riavvio > spostamento, "il riavvio non avviene dopo lo spostamento");
        }

        [Fact]
        public void ScriviScriptHelper_NonCancellaMaiRicorsivamente()
        {
            // Scelta deliberata: la cartella viene spostata, mai rimossa. Un "rmdir /s /q" introdotto in
            // futuro renderebbe il reset irreversibile e porterebbe via anche lo storico locale.
            string contenuto = File.ReadAllText(FactoryResetService.ScriviScriptHelper(_radice));

            Assert.DoesNotContain("rmdir", contenuto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rd /s", contenuto, StringComparison.OrdinalIgnoreCase);
        }
    }
}
