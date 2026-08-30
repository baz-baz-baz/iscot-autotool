using System;
using System.IO;
using System.Linq;
using PdfSharp.Pdf.IO;
using PersonalAutomationTool.Modules.PassaggioConsegne;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.PassaggioConsegne
{
    /// <summary>
    /// Tier 2 (produce file veri su disco): generazione del PDF del rapportino.
    ///
    /// <para>
    /// <b>Perché questa suite esiste ed è possibile.</b> Nella prima versione del modulo il PDF era una
    /// cattura <c>RenderTargetBitmap</c> della vista WPF: verificarlo avrebbe richiesto di istanziare
    /// una finestra, quindi non era testabile qui e finiva nella checklist manuale (§7.1). Ora
    /// l'esportatore riceve un <see cref="RapportinoSnapshot"/> e non tocca WPF, quindi il PDF si
    /// genera e si riapre dentro xUnit. In particolare questi test coprono i due rischi che a occhio
    /// non si vedono: che i font si risolvano davvero a runtime, e che il rapportino resti su
    /// <b>una sola pagina</b> come il template Excel.
    /// </para>
    /// </summary>
    public sealed class PassaggioConsegnePdfExporterTests : IDisposable
    {
        private readonly string _cartella;

        public PassaggioConsegnePdfExporterTests()
        {
            _cartella = Path.Combine(Path.GetTempPath(), "PatTests_PassaggioConsegne_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cartella);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_cartella)) Directory.Delete(_cartella, true); }
            catch { /* pulizia best-effort: un file ancora agganciato non deve far fallire la suite */ }
        }

        [Fact]
        public void GeneraUnFilePdfNonVuoto()
        {
            string percorso = PassaggioConsegnePdfExporter.ExportToPdf(SnapshotDiProva(), _cartella);

            Assert.True(File.Exists(percorso));
            Assert.True(new FileInfo(percorso).Length > 0);
        }

        [Fact]
        public void IlFileGeneratoEUnPdfValido()
        {
            string percorso = PassaggioConsegnePdfExporter.ExportToPdf(SnapshotDiProva(), _cartella);

            byte[] intestazione = new byte[5];
            using (var stream = File.OpenRead(percorso)) _ = stream.Read(intestazione, 0, 5);

            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(intestazione));
        }

        [Fact]
        public void IlRapportinoOccupaUnaSolaPagina()
        {
            string percorso = PassaggioConsegnePdfExporter.ExportToPdf(SnapshotDiProva(), _cartella);

            using var documento = PdfReader.Open(percorso, PdfDocumentOpenMode.Import);

            Assert.Equal(1, documento.PageCount);
        }

        /// <summary>
        /// L'invariante che conta davvero: il contenuto, una volta ridotto in scala, sta dentro i
        /// margini della pagina.
        ///
        /// <para>
        /// Contare le pagine <b>non</b> basta a dimostrarlo: PDFsharp non impagina da solo, quindi un
        /// rapportino troppo alto non produrrebbe una seconda pagina ma verrebbe tagliato al bordo,
        /// lasciando verde un test sul solo <c>PageCount</c>. Qui si verifica l'ingombro reale su A4
        /// orizzontale (842 × 595 punti) al crescere del numero di righe.
        /// </para>
        /// </summary>
        [Theory]
        [InlineData(10, 5, 10)]     // il rapportino del template Excel
        [InlineData(0, 0, 0)]       // completamente vuoto
        [InlineData(25, 20, 25)]    // turno molto carico
        [InlineData(60, 40, 60)]    // caso limite, ben oltre l'uso reale
        public void IlContenutoInScalaRestaDentroLaPagina(int movimenti, int interventi, int nonSvolti)
        {
            const double larghezzaA4Orizzontale = 842;
            const double altezzaA4Orizzontale = 595;

            var snapshot = SnapshotDiProva(movimenti, interventi, nonSvolti);

            var (larghezza, altezza) = PassaggioConsegnePdfExporter.CalcolaIngombro(
                snapshot, larghezzaA4Orizzontale, altezzaA4Orizzontale);

            Assert.InRange(larghezza, 1, larghezzaA4Orizzontale);
            Assert.InRange(altezza, 1, altezzaA4Orizzontale);
        }

        [Fact]
        public void UnRapportinoCorto_EVincolatoDallaLarghezzaNonDallAltezza()
        {
            // Con poche righe l'altezza non è il vincolo: la tabella si allarga fino a occupare la
            // larghezza utile della pagina e lì si ferma, lasciando spazio verticale libero. È il
            // comportamento del template Excel, che centra il contenuto sul foglio.
            var (larghezza, altezza) = PassaggioConsegnePdfExporter.CalcolaIngombro(
                SnapshotDiProva(movimenti: 1, interventi: 1, nonSvolti: 1), 842, 595);

            Assert.True(larghezza > 750, $"la tabella doveva occupare la larghezza utile, era {larghezza}");
            Assert.True(altezza < 595, $"con poche righe doveva avanzare spazio verticale, era {altezza}");
        }

        [Fact]
        public void LaScalaNonSuperaMaiUno()
        {
            // Cappello di sicurezza: nemmeno un rapportino ridicolmente corto viene ingrandito oltre
            // la dimensione naturale, altrimenti il corpo del carattere cambierebbe a ogni turno.
            Assert.True(PassaggioConsegnePdfExporter.CalcolaScala(1, 842, 595) <= 1.0);
        }

        [Fact]
        public void PiuRigheProduconoUnaScalaMinore()
        {
            double scalaStandard = PassaggioConsegnePdfExporter.CalcolaScala(
                PassaggioConsegnePdfExporter.CalcolaAltezzaNaturale(SnapshotDiProva()), 842, 595);
            double scalaCarica = PassaggioConsegnePdfExporter.CalcolaScala(
                PassaggioConsegnePdfExporter.CalcolaAltezzaNaturale(SnapshotDiProva(40, 30, 40)), 842, 595);

            Assert.True(scalaCarica < scalaStandard,
                $"la scala doveva ridursi: standard={scalaStandard}, carica={scalaCarica}");
        }

        [Fact]
        public void IlNomeDelFileEFissoIndipendentementeDaFlottaEData()
        {
            // Requisito esplicito: il PDF allegato all'email deve chiamarsi sempre "Rapportino di
            // Turno.pdf", non variare per flotta o data come nella versione precedente.
            string percorso = PassaggioConsegnePdfExporter.ExportToPdf(SnapshotDiProva(), _cartella);
            string nome = Path.GetFileName(percorso);

            Assert.Equal("Rapportino di Turno.pdf", nome);
        }

        [Fact]
        public void UnRapportinoCompletamenteVuotoNonFaFallireLEsportazione()
        {
            // Caso reale: il tecnico apre il modulo e preme "Genera Mail" senza aver compilato nulla.
            var vuoto = new RapportinoSnapshot(
                TipoTreno: "ETR 500",
                Sottotitolo: "ETR 500 (da aggiornare durante il turno con verifica presso ufficio CT Trenitalia)",
                Nome: string.Empty,
                Cognome: string.Empty,
                Data: "24/08/2026",
                OraInizio: string.Empty,
                OraFine: string.Empty,
                Movimenti: [],
                Interventi: [],
                InterventiNonSvolti: []);

            string percorso = PassaggioConsegnePdfExporter.ExportToPdf(vuoto, _cartella);

            Assert.True(File.Exists(percorso));
        }

        [Fact]
        public void DueEsportazioniConsecutiveNonSiOstacolano()
        {
            // Il file di destinazione ha un nome deterministico: la seconda esportazione dello stesso
            // turno deve sovrascrivere la prima senza errori di file già in uso.
            var snapshot = SnapshotDiProva();

            string primo = PassaggioConsegnePdfExporter.ExportToPdf(snapshot, _cartella);
            string secondo = PassaggioConsegnePdfExporter.ExportToPdf(snapshot, _cartella);

            Assert.Equal(primo, secondo);
            Assert.True(new FileInfo(secondo).Length > 0);
        }

        [Fact]
        public void TestoLungoNelleCelle_NonFaFallireIlDisegno()
        {
            // Le intestazioni del template sono già lunghe ("CHIUSURA TICKET MAXIMO+ EMAIL"); qui si
            // verifica che anche un contenuto utente sproporzionato venga mandato a capo e ridotto
            // invece di far esplodere il layout.
            var snapshot = SnapshotDiProva() with
            {
                Interventi =
                [
                    new InterventoSnapshot(
                        TrenoLoco: "ETR700 101",
                        Descrizione: string.Join(' ', Enumerable.Repeat("descrizione molto lunga", 40)),
                        CompilazioneOdl: "Si", ChiusuraTicket: "No", CompReport: "Si",
                        EmailIngegneria: "No", AggiornareVerifiche: "Si")
                ]
            };

            string percorso = PassaggioConsegnePdfExporter.ExportToPdf(snapshot, _cartella);

            Assert.True(new FileInfo(percorso).Length > 0);
        }

        [Fact]
        public void SnapshotNullo_SollevaArgumentNullException() =>
            Assert.Throws<ArgumentNullException>(() => PassaggioConsegnePdfExporter.ExportToPdf(null!, _cartella));

        private static RapportinoSnapshot SnapshotDiProva(int movimenti = 10, int interventi = 5, int nonSvolti = 10) => new(
            TipoTreno: "ETR 700",
            Sottotitolo: "ETR 700 (da aggiornare durante il turno con verifica presso ufficio CT Hitachi)",
            Nome: "Alessio",
            Cognome: "Bassetto",
            Data: "24/08/2026",
            OraInizio: "06:00",
            OraFine: "14:00",
            Movimenti: [.. Enumerable.Range(1, movimenti).Select(i =>
                new MovimentoSnapshot(i, $"ETR700 {i}", $"{100 + i}", "No", "No", "No", "No"))],
            Interventi: [.. Enumerable.Range(1, interventi).Select(i =>
                new InterventoSnapshot($"ETR700 {100 + i}", $"Intervento {i}", "Si", "No", "Si", "No", "Si"))],
            InterventiNonSvolti: [.. Enumerable.Range(1, nonSvolti).Select(i =>
                new InterventoNonSvoltoSnapshot(i, $"ETR700 {200 + i}", "Mancanza ricambi", "14:30", "Rossi", "Si", "No"))]);
    }
}
