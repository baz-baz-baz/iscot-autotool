using System;
using System.IO;
using PersonalAutomationTool.Modules.Home;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Home
{
    /// <summary>
    /// <see cref="PathHealthCheckService"/>: verifica in sola lettura dei percorsi
    /// Hitachi/SharePoint/OneDrive.
    ///
    /// <para>
    /// <b>Perché <see cref="PathHealthCheckService.EseguiControllo"/> non è testato direttamente qui.</b>
    /// Legge le configurazioni reali da <c>%APPDATA%\PersonalAutomationTool</c> (<c>HitachiPathsManager</c>,
    /// <c>VerifichePathsManager</c>) apposta — è il punto di forza del servizio, l'elenco controllato è
    /// sempre quello davvero in uso — ma questo lo rende dipendente dalla macchina su cui gira la suite,
    /// non isolabile in una cartella temporanea usa-e-getta. Sono testate invece le due funzioni pure
    /// che decidono <i>come</i> ogni singolo percorso viene classificato: <c>CheckDirectory</c>,
    /// <c>CheckFile</c> e la mappatura delle eccezioni. Nessuna di queste tocca mai
    /// <c>%APPDATA%</c> né le configurazioni reali.
    /// </para>
    ///
    /// <para>
    /// <b>Perché <c>UnauthorizedAccessException</c> non è riprodotta con un vero permesso NTFS
    /// negato.</b> Manipolare le ACL su una cartella temporanea per farla fallire davvero è fragile:
    /// dipende dai privilegi dell'account che esegue la suite e si comporta diversamente a seconda dei
    /// criteri di dominio della macchina — lo stesso motivo per cui
    /// <c>FileOperationRetryTests.IsSharingViolation_NonRiconosceEccezioniNonCorrelate</c> classifica
    /// eccezioni costruite direttamente invece di provocarle su disco. <c>MappaEccezione</c>, estratta
    /// apposta come funzione pura, permette lo stesso qui: si verifica la classificazione, non la
    /// capacità di Windows di negare un permesso.
    /// </para>
    /// </summary>
    public sealed class PathHealthCheckServiceTests : IDisposable
    {
        private readonly string _cartella;

        public PathHealthCheckServiceTests()
        {
            _cartella = Path.Combine(Path.GetTempPath(), "PatTests_PathHealthCheck_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cartella);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_cartella)) Directory.Delete(_cartella, true); }
            catch { /* pulizia best-effort */ }
        }

        // ------------------------------------------------------------------
        // MappaEccezione — classificazione pura, senza I/O
        // ------------------------------------------------------------------

        [Fact]
        public void MappaEccezione_AccessoNegato_ProduceStatoAccessoNegato()
        {
            var (stato, dettaglio) = PathHealthCheckService.MappaEccezione(new UnauthorizedAccessException());

            Assert.Equal(PathHealthStatus.AccessoNegato, stato);
            Assert.Contains("Accesso negato", dettaglio);
        }

        [Fact]
        public void MappaEccezione_PercorsoTroppoLungo_ProduceStatoErrore()
        {
            var (stato, dettaglio) = PathHealthCheckService.MappaEccezione(new PathTooLongException());

            Assert.Equal(PathHealthStatus.Errore, stato);
            Assert.Contains("troppo lungo", dettaglio);
        }

        [Fact]
        public void MappaEccezione_ErroreIO_ProduceStatoErroreConMessaggioOriginale()
        {
            var (stato, dettaglio) = PathHealthCheckService.MappaEccezione(new IOException("volume smontato"));

            Assert.Equal(PathHealthStatus.Errore, stato);
            Assert.Contains("volume smontato", dettaglio);
        }

        [Fact]
        public void MappaEccezione_EccezioneNonCorrelata_ProduceComunqueStatoErrore()
        {
            // Un'eccezione mai prevista non deve far crashare l'health-check: ricade sullo stato
            // generico "Errore" invece di propagarsi (punto 3 della specifica: "senza far crashare l'app").
            var (stato, _) = PathHealthCheckService.MappaEccezione(new InvalidOperationException());

            Assert.Equal(PathHealthStatus.Errore, stato);
        }

        // ------------------------------------------------------------------
        // CheckDirectory
        // ------------------------------------------------------------------

        [Fact]
        public void CheckDirectory_CartellaEsistente_RestituisceOk()
        {
            var item = PathHealthCheckService.CheckDirectory("Test", _cartella);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
            Assert.Equal("OK", item.StatoTesto);
            Assert.Equal("Cartella raggiungibile.", item.Dettaglio);
        }

        [Fact]
        public void CheckDirectory_CartellaConContenuto_RestituisceOk()
        {
            File.WriteAllText(Path.Combine(_cartella, "file.txt"), "contenuto");

            var item = PathHealthCheckService.CheckDirectory("Test", _cartella);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
        }

        [Fact]
        public void CheckDirectory_CartellaVuota_RestituisceComunqueOk()
        {
            // Directory.Exists=true ma nessuna voce da enumerare: FirstOrDefault() restituisce null
            // senza sollevare eccezioni, il percorso resta comunque "raggiungibile".
            var item = PathHealthCheckService.CheckDirectory("Test", _cartella);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
        }

        [Fact]
        public void CheckDirectory_PercorsoInesistente_RestituisceErrore()
        {
            string percorsoInesistente = Path.Combine(_cartella, "non_esiste");

            var item = PathHealthCheckService.CheckDirectory("Test", percorsoInesistente);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
            Assert.Equal("ERRORE", item.StatoTesto);
            Assert.Contains("non trovato", item.Dettaglio);
        }

        [Fact]
        public void CheckDirectory_PercorsoInesistente_NonLoCreaMai()
        {
            // Il vincolo critico della specifica: la sola verifica non deve mai materializzare la
            // cartella mancante che sta segnalando.
            string percorsoInesistente = Path.Combine(_cartella, "non_esiste");

            PathHealthCheckService.CheckDirectory("Test", percorsoInesistente);

            Assert.False(Directory.Exists(percorsoInesistente));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CheckDirectory_PercorsoNonConfigurato_RestituisceErroreSenzaToccareIlDisco(string? percorso)
        {
            var item = PathHealthCheckService.CheckDirectory("Test", percorso!);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
            Assert.Equal("Percorso non configurato.", item.Dettaglio);
        }

        [Fact]
        public void CheckDirectory_PropagaLaFunzioneEIlPercorsoNelRisultato()
        {
            var item = PathHealthCheckService.CheckDirectory("Verifiche ETR500", _cartella);

            Assert.Equal("Verifiche ETR500", item.Funzione);
            Assert.Equal(_cartella, item.Percorso);
        }

        // ------------------------------------------------------------------
        // CheckFile
        // ------------------------------------------------------------------

        [Fact]
        public void CheckFile_FileEsistente_RestituisceOk()
        {
            string percorsoFile = Path.Combine(_cartella, "report.xlsx");
            File.WriteAllText(percorsoFile, "dati");

            var item = PathHealthCheckService.CheckFile("Test", percorsoFile);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
            Assert.Equal("File raggiungibile.", item.Dettaglio);
        }

        [Fact]
        public void CheckFile_FileGiaApertoInLetturaDaAltri_RestituisceComunqueOk()
        {
            // La sonda usa FileShare.ReadWrite: un altro processo che tiene il file aperto in
            // lettura/scrittura condivisa non deve far apparire un falso "ERRORE".
            string percorsoFile = Path.Combine(_cartella, "report.xlsx");
            File.WriteAllText(percorsoFile, "dati");

            using var altroHandle = File.Open(percorsoFile, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            var item = PathHealthCheckService.CheckFile("Test", percorsoFile);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
        }

        [Fact]
        public void CheckFile_PercorsoInesistente_RestituisceErrore()
        {
            string percorsoInesistente = Path.Combine(_cartella, "assente.xlsx");

            var item = PathHealthCheckService.CheckFile("Test", percorsoInesistente);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
            Assert.Contains("non trovato", item.Dettaglio);
        }

        [Fact]
        public void CheckFile_PercorsoInesistente_NonLoCreaMai()
        {
            string percorsoInesistente = Path.Combine(_cartella, "assente.xlsx");

            PathHealthCheckService.CheckFile("Test", percorsoInesistente);

            Assert.False(File.Exists(percorsoInesistente));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void CheckFile_PercorsoNonConfigurato_RestituisceErroreSenzaToccareIlDisco(string? percorso)
        {
            var item = PathHealthCheckService.CheckFile("Test", percorso!);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
            Assert.Equal("Percorso non configurato.", item.Dettaglio);
        }

        // ------------------------------------------------------------------
        // StatoTesto — le tre diciture del badge
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(PathHealthStatus.Ok, "OK")]
        [InlineData(PathHealthStatus.Errore, "ERRORE")]
        [InlineData(PathHealthStatus.AccessoNegato, "ACCESSO NEGATO")]
        public void StatoTesto_RestituisceLaDicituraCorrettaPerOgniStato(PathHealthStatus stato, string atteso)
        {
            var item = new PathHealthCheckItem("Test", "C:\\qualsiasi", stato, "dettaglio");

            Assert.Equal(atteso, item.StatoTesto);
        }
    }
}
