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
    /// Legge le configurazioni reali dalla cartella dati di <c>AppPaths</c> (<c>HitachiPathsManager</c>,
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
        // CheckExcelFolder / CheckExcelFile / TrovaFilePiuRecente — pipeline profonda anti-falsi-
        // positivi (PROJECT_MEMORY.md §6.1-quadragies-quinquies): non basta più che la cartella
        // esista, serve trovare ed effettivamente leggere un file Excel attivo.
        // ------------------------------------------------------------------

        private static readonly byte[] FirmaZipValida = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];

        [Fact]
        public void CheckExcelFolder_CartellaEsistenteMaSenzaFileExcel_NonERestituisceOk()
        {
            // Il falso positivo esatto segnalato dal committente: la cartella c'è, ma dentro non c'è
            // nulla da leggere. Deve essere AVVISO, non OK e non ERRORE (il percorso di per sé è
            // corretto).
            var item = PathHealthCheckService.CheckExcelFolder("Test", _cartella, "*Verifiche*.xlsx", recursive: true);

            Assert.Equal(PathHealthStatus.Avviso, item.Stato);
            Assert.Equal("AVVISO", item.StatoTesto);
            Assert.Contains("nessun file Excel attivo", item.Dettaglio);
        }

        [Fact]
        public void CheckExcelFolder_SoloFileDiLockExcel_NonLoContaComeFileReale()
        {
            // "~$Verifiche ETR500.xlsx" risponde al pattern glob (contiene "Verifiche", finisce per
            // ".xlsx") ma è il lucchetto che Excel crea mentre il foglio è aperto altrove, non il
            // foglio stesso: deve essere scartato, lasciando la cartella "senza file attivi".
            File.WriteAllBytes(Path.Combine(_cartella, "~$Verifiche ETR500.xlsx"), FirmaZipValida);

            var item = PathHealthCheckService.CheckExcelFolder("Test", _cartella, "*Verifiche*.xlsx", recursive: true);

            Assert.Equal(PathHealthStatus.Avviso, item.Stato);
            Assert.Contains("nessun file Excel attivo", item.Dettaglio);
        }

        [Fact]
        public void CheckExcelFolder_ConFileValido_RestituisceOkConNomeDimensioneEData()
        {
            string file = Path.Combine(_cartella, "Verifiche ETR500.xlsx");
            File.WriteAllBytes(file, FirmaZipValida);

            var item = PathHealthCheckService.CheckExcelFolder("Test", _cartella, "*Verifiche*.xlsx", recursive: true);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
            Assert.Equal("OK", item.StatoTesto);
            Assert.Contains("Verifiche ETR500.xlsx", item.Dettaglio);
            Assert.Contains("KB", item.Dettaglio);
            Assert.Contains("modificato il", item.Dettaglio);
        }

        [Fact]
        public void CheckExcelFolder_ConPiuFile_SceglieIlPiuRecente()
        {
            string vecchio = Path.Combine(_cartella, "Verifiche ETR500 vecchio.xlsx");
            string nuovo = Path.Combine(_cartella, "Verifiche ETR500 nuovo.xlsx");
            File.WriteAllBytes(vecchio, FirmaZipValida);
            File.SetLastWriteTime(vecchio, DateTime.Now.AddDays(-2));
            File.WriteAllBytes(nuovo, FirmaZipValida);
            File.SetLastWriteTime(nuovo, DateTime.Now);

            var item = PathHealthCheckService.CheckExcelFolder("Test", _cartella, "*Verifiche*.xlsx", recursive: true);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
            Assert.Contains("nuovo.xlsx", item.Dettaglio);
            Assert.DoesNotContain("vecchio.xlsx", item.Dettaglio);
        }

        [Fact]
        public void CheckExcelFolder_CartellaInesistente_RestituisceErroreSenzaCrearla()
        {
            // Vincolo critico invariato: nemmeno la pipeline profonda deve mai materializzare la
            // cartella che sta segnalando come mancante.
            string percorsoInesistente = Path.Combine(_cartella, "non_esiste");

            var item = PathHealthCheckService.CheckExcelFolder("Test", percorsoInesistente, "*Verifiche*.xlsx", recursive: true);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
            Assert.False(Directory.Exists(percorsoInesistente));
        }

        [Fact]
        public void CheckExcelFile_HeaderZipValido_RestituisceOkConMetadatiNelDettaglio()
        {
            string file = Path.Combine(_cartella, "Verifiche ETR700.xlsx");
            File.WriteAllBytes(file, FirmaZipValida);

            var item = PathHealthCheckService.CheckExcelFile("Test", file);

            Assert.Equal(PathHealthStatus.Ok, item.Stato);
            Assert.Contains("Verifiche ETR700.xlsx", item.Dettaglio);
        }

        [Fact]
        public void CheckExcelFile_FileVuoto_RestituisceErrore()
        {
            string file = Path.Combine(_cartella, "vuoto.xlsx");
            File.WriteAllBytes(file, []);

            var item = PathHealthCheckService.CheckExcelFile("Test", file);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
            Assert.Contains("non è un file .xlsx valido", item.Dettaglio);
        }

        [Fact]
        public void CheckExcelFile_HeaderNonZip_RestituisceErrore()
        {
            // Quattro byte reali ma non la firma ZIP: un file rinominato per errore, o un .xls
            // legacy (formato OLE, non ZIP) salvato con estensione .xlsx.
            string file = Path.Combine(_cartella, "corrotto.xlsx");
            File.WriteAllBytes(file, [0x00, 0x01, 0x02, 0x03]);

            var item = PathHealthCheckService.CheckExcelFile("Test", file);

            Assert.Equal(PathHealthStatus.Errore, item.Stato);
        }

        [Fact]
        public void CheckExcelFile_AttributoOffline_RestituisceAvvisoInvecediErrore()
        {
            // Il segnaposto cloud OneDrive non è un file corrotto: è un file non ancora scaricato.
            // Deve essere AVVISO (recuperabile aspettando la sincronizzazione), non ERRORE.
            string file = Path.Combine(_cartella, "cloud.xlsx");
            File.WriteAllBytes(file, FirmaZipValida);
            File.SetAttributes(file, File.GetAttributes(file) | FileAttributes.Offline);

            try
            {
                var item = PathHealthCheckService.CheckExcelFile("Test", file);

                Assert.Equal(PathHealthStatus.Avviso, item.Stato);
                Assert.Contains("OneDrive", item.Dettaglio);
                Assert.Contains("non scaricato", item.Dettaglio);
            }
            finally
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
        }

        // ------------------------------------------------------------------
        // TrovaFilePiuRecente
        // ------------------------------------------------------------------

        [Fact]
        public void TrovaFilePiuRecente_EscludeSempreIFileDiLock()
        {
            File.WriteAllBytes(Path.Combine(_cartella, "~$Verifiche ETR500.xlsx"), FirmaZipValida);
            string reale = Path.Combine(_cartella, "Verifiche ETR500.xlsx");
            File.WriteAllBytes(reale, FirmaZipValida);

            string? trovato = PathHealthCheckService.TrovaFilePiuRecente(_cartella, "*Verifiche*.xlsx", recursive: true);

            Assert.Equal(reale, trovato);
        }

        [Fact]
        public void TrovaFilePiuRecente_NessunFileCorrispondente_RestituisceNull()
        {
            File.WriteAllText(Path.Combine(_cartella, "altro.txt"), "contenuto");

            string? trovato = PathHealthCheckService.TrovaFilePiuRecente(_cartella, "*Verifiche*.xlsx", recursive: true);

            Assert.Null(trovato);
        }

        // ------------------------------------------------------------------
        // StatoTesto — le quattro diciture del badge
        // ------------------------------------------------------------------

        [Theory]
        [InlineData(PathHealthStatus.Ok, "OK")]
        [InlineData(PathHealthStatus.Avviso, "AVVISO")]
        [InlineData(PathHealthStatus.Errore, "ERRORE")]
        [InlineData(PathHealthStatus.AccessoNegato, "ACCESSO NEGATO")]
        public void StatoTesto_RestituisceLaDicituraCorrettaPerOgniStato(PathHealthStatus stato, string atteso)
        {
            var item = new PathHealthCheckItem("Test", "C:\\qualsiasi", stato, "dettaglio");

            Assert.Equal(atteso, item.StatoTesto);
        }
    }
}
