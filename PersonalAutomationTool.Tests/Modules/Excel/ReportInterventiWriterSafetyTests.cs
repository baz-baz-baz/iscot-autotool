using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PersonalAutomationTool.Core;
using PersonalAutomationTool.Modules.Excel;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Le due anomalie critiche dello Sprint 34 (PROJECT_MEMORY.md §6.1-tricies-septies):
    /// <list type="number">
    /// <item><b>ETR1000 I-F</b> — riga di inserimento calcolata sulle righe già formattate ma vuote, con
    /// il risultato di scrivere in fondo alla zona preformattata (o di annunciare una scrittura che nel
    /// foglio non c'è).</item>
    /// <item><b>ETR500</b> — il report che sparisce dalla cartella sincronizzata durante il
    /// salvataggio.</item>
    /// </list>
    /// Qui si verifica ciò che protegge i dati: <b>dove</b> finisce la riga e <b>che l'originale non
    /// sparisca mai</b>, nemmeno quando la scrittura fallisce.
    /// </summary>
    public sealed class ReportInterventiWriterSafetyTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ReportSafety_");
        private readonly int _delayOriginale = ReportInterventiWriter.SafeSaveDelayMsSostitutivo;

        public ReportInterventiWriterSafetyTests()
        {
            // Le riprove servono ad aspettare OneDrive; qui il blocco è simulato e definitivo.
            ReportInterventiWriter.SafeSaveDelayMsSostitutivo = 1;
        }

        public void Dispose()
        {
            ReportInterventiWriter.SafeSaveDelayMsSostitutivo = _delayOriginale;

            // I test che provocano volutamente un errore lasciano il file di lavoro: è il comportamento
            // voluto in esercizio, ma la cartella temporanea reale della macchina non deve accumularli.
            // Si ripuliscono **solo** i file di lavoro dei report di questa classe: le altre suite girano
            // in parallelo sulla stessa cartella e stanno scrivendo i propri.
            try
            {
                foreach (string report in Directory.GetFiles(_root.FullName))
                {
                    foreach (string workingCopy in WorkingCopiesOf(report))
                    {
                        try { File.Delete(workingCopy); } catch { /* best-effort cleanup */ }
                    }
                }
            }
            catch { /* best-effort cleanup */ }

            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private static readonly Dictionary<int, string?> RigaDiProva = new()
        {
            [2] = "15/03/2026",
            [3] = "Napoli Gianturco",
            [4] = "1258999",
            [7] = "15/03/2026"
        };

        private static XElement SheetRoot(string filePath, string partName = "xl/worksheets/sheet1.xml")
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            using var stream = archive.GetEntry(partName)!.Open();
            return XDocument.Load(stream).Root!;
        }

        private static List<int> RowIndexes(string filePath) =>
            [.. SheetRoot(filePath).Element(S + "sheetData")!.Elements(S + "row")
                .Select(r => int.Parse(r.Attribute("r")!.Value))];

        // -----------------------------------------------------------------------------------
        // 1. ETR1000 I-F — la riga di inserimento è la prima DAVVERO libera
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Il caso del bug report: dieci righe compilate seguite da cinque righe che hanno solo stile e
        /// bordi. La riga di inserimento deve essere l'11 — non la 16 (contando le righe fantasma come
        /// occupate) e non la 1 (ignorando i dati esistenti).
        /// </summary>
        [Fact]
        public void PrimaRigaLibera_IgnoraLeRigheFormattateMaVuote()
        {
            string file = Path.Combine(_root.FullName, "ghost.xlsx");
            GhostRowsReportBuilder.Create(file);

            int lastFilled = ReportInterventiWriter.FindLastFilledRow(file, GhostRowsReportBuilder.SheetName, [2, 3, 4, 7]);

            Assert.Equal(GhostRowsReportBuilder.LastFilledRow, lastFilled);   // 10, non 15
            Assert.Equal(GhostRowsReportBuilder.FirstGhostRow, lastFilled + 1); // si scrive alla 11
        }

        /// <summary>
        /// Ognuna delle cinque forme di cella vuota, presa singolarmente, non deve qualificare la riga
        /// come occupata. Sono righe distinte nella fixture, quindi se anche una sola forma sfuggisse al
        /// controllo il conteggio sopra cambierebbe — questo test dice **quale**.
        /// </summary>
        [Theory]
        [InlineData(11)] // <c r="B11" s="3"/>
        [InlineData(12)] // <c r="B12" s="3"></c>
        [InlineData(13)] // <c r="B13" s="3"><v></v></c>
        [InlineData(14)] // stringa inline di soli spazi
        [InlineData(15)] // stringa condivisa che punta a una voce vuota
        public void RigaFantasma_NonContaComeOccupata(int ghostRow)
        {
            string file = Path.Combine(_root.FullName, $"ghost_{ghostRow}.xlsx");
            GhostRowsReportBuilder.Create(file);

            // La riga esiste e ha celle: è proprio questo che inganna un controllo strutturale.
            var row = SheetRoot(file).Element(S + "sheetData")!.Elements(S + "row")
                .Single(r => r.Attribute("r")!.Value == ghostRow.ToString());
            Assert.NotEmpty(row.Elements(S + "c"));

            Assert.True(ReportInterventiWriter.FindLastFilledRow(file, GhostRowsReportBuilder.SheetName, [2, 3, 4, 7]) < ghostRow,
                $"La riga {ghostRow}, vuota ma formattata, è stata contata come compilata.");
        }

        [Fact]
        public void ScritturaSullaPrimaRigaLibera_AtterraDoveIlCalcoloDice()
        {
            string file = Path.Combine(_root.FullName, "ghost_write.xlsx");
            GhostRowsReportBuilder.Create(file);

            int lastFilled = ReportInterventiWriter.FindLastFilledRow(file, GhostRowsReportBuilder.SheetName, [2, 3, 4, 7]);
            var esito = ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, lastFilled + 1, RigaDiProva);

            Assert.Equal(GhostRowsReportBuilder.FirstGhostRow, esito.RowNumber);
            Assert.False(esito.RowWasOccupied, "La riga 11 era libera: non doveva risultare occupata.");

            // I valori risultano davvero nel foglio, riletti dal file salvato.
            Assert.Equal("Napoli Gianturco", esito.ValuesAfter[3]);
            Assert.Equal("1258999", esito.ValuesAfter[4]);

            // Le dieci righe di dati precedenti sono intatte.
            var rows = RowIndexes(file);
            Assert.Equal(Enumerable.Range(1, GhostRowsReportBuilder.LastGhostRow), rows);
        }

        /// <summary>
        /// Scrivendo due volte di seguito, la seconda riga deve andare **sotto** la prima: è la
        /// controprova che la scrittura precedente viene riconosciuta come dato vero e non come riga
        /// fantasma. Era il difetto che sovrascriveva sempre la stessa riga.
        /// </summary>
        [Fact]
        public void ScrittureConsecutive_NonSiSovrascrivono()
        {
            string file = Path.Combine(_root.FullName, "ghost_twice.xlsx");
            GhostRowsReportBuilder.Create(file);

            int primaRiga = ReportInterventiWriter.FindLastFilledRow(file, GhostRowsReportBuilder.SheetName, [2, 3, 4, 7]) + 1;
            ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, primaRiga, RigaDiProva);

            int secondaRiga = ReportInterventiWriter.FindLastFilledRow(file, GhostRowsReportBuilder.SheetName, [2, 3, 4, 7]) + 1;
            var esito = ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, secondaRiga, RigaDiProva);

            Assert.Equal(primaRiga + 1, secondaRiga);
            Assert.False(esito.RowWasOccupied);

            // La prima scrittura è ancora al suo posto.
            var primaRigaValori = SheetRoot(file).Element(S + "sheetData")!.Elements(S + "row")
                .Single(r => r.Attribute("r")!.Value == primaRiga.ToString());
            Assert.Contains(primaRigaValori.Elements(S + "c"), c => c.Value.Contains("Napoli Gianturco"));
        }

        // -----------------------------------------------------------------------------------
        // 2. ETR500 — l'originale non sparisce mai
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Il cuore del bug ETR500: un errore di I/O durante il salvataggio non deve <b>mai</b> lasciare
        /// il tecnico senza il report. Qui l'errore è reale, non simulato con un mock: il file di
        /// destinazione viene tenuto aperto in sola lettura, esattamente come farebbe il client
        /// OneDrive/SharePoint mentre sincronizza, così la sostituzione finale fallisce davvero.
        /// </summary>
        [Fact]
        public void ErroreDiScrittura_LasciaIlFileOriginaleIntattoEValido()
        {
            string file = Path.Combine(_root.FullName, "locked.xlsx");
            GhostRowsReportBuilder.Create(file);
            byte[] prima = File.ReadAllBytes(file);

            // FileShare.Read: la copia di lavoro iniziale riesce (lettura), la sostituzione no (scrittura).
            using (var blocco = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.ThrowsAny<IOException>(
                    () => ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, 11, RigaDiProva));
            }

            // Il file è ancora lì, con lo stesso contenuto byte per byte: nessuna cancellazione, nessuno
            // spostamento, nessun troncamento a metà scrittura.
            Assert.True(File.Exists(file), "Il report originale è sparito dopo un errore di scrittura.");
            Assert.Equal(prima, File.ReadAllBytes(file));

            // E resta leggibile: il calcolo della riga funziona ancora come prima del tentativo.
            Assert.Equal(GhostRowsReportBuilder.LastFilledRow,
                ReportInterventiWriter.FindLastFilledRow(file, GhostRowsReportBuilder.SheetName, [2, 3, 4, 7]));
        }

        [Fact]
        public void ScritturaRiuscita_NonLasciaFileDiLavoroInGiro()
        {
            string file = Path.Combine(_root.FullName, "temp_cleanup.xlsx");
            GhostRowsReportBuilder.Create(file);

            ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, 11, RigaDiProva);

            Assert.Empty(WorkingCopiesOf(file));
        }

        /// <summary>
        /// Il rovescio della medaglia, ed è deliberato: quando la sostituzione fallisce il file di
        /// lavoro <b>resta</b>, perché contiene la scrittura appena eseguita ed è l'unica via di
        /// recupero. Il nome lo lega al report di provenienza.
        /// </summary>
        [Fact]
        public void ScritturaFallita_ConservaIlFileDiLavoroPerIlRecupero()
        {
            string file = Path.Combine(_root.FullName, "recupero.xlsx");
            GhostRowsReportBuilder.Create(file);

            using (var blocco = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.ThrowsAny<IOException>(
                    () => ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, 11, RigaDiProva));
            }

            string[] superstiti = WorkingCopiesOf(file);
            Assert.Single(superstiti);

            // È un pacchetto valido e contiene davvero la riga che il tecnico credeva di aver salvato.
            Assert.Equal("Napoli Gianturco", ReadCell(superstiti[0], 11, "C11"));
        }

        private static string ReadCell(string filePath, int rowNumber, string reference)
        {
            var row = SheetRoot(filePath).Element(S + "sheetData")!.Elements(S + "row")
                .Single(r => r.Attribute("r")!.Value == rowNumber.ToString());
            return row.Elements(S + "c").Single(c => c.Attribute("r")!.Value == reference).Value;
        }

        /// <summary>
        /// Solo i file di lavoro del report indicato: la suite gira in parallelo e altre classi stanno
        /// scrivendo i propri report nella stessa cartella temporanea.
        /// </summary>
        private static string[] WorkingCopiesOf(string reportPath) =>
            Directory.Exists(AppPaths.TempFolder)
                ? Directory.GetFiles(AppPaths.TempFolder, ReportInterventiWriter.WorkingCopyPrefix(reportPath) + "_*")
                : [];

        /// <summary>
        /// <see cref="FileOperationRetry.MoveSafely"/> è ciò che sostituisce <c>File.Move</c> in "Sposta
        /// report" e "Riporta report". Se la destinazione non è scrivibile, l'origine deve restare dov'è:
        /// è la differenza fra un'operazione non riuscita e un report perso.
        /// </summary>
        [Fact]
        public void MoveSafely_DestinazioneBloccata_LasciaLOrigineAlSuoPosto()
        {
            string source = Path.Combine(_root.FullName, "origine.xlsx");
            string destination = Path.Combine(_root.FullName, "destinazione.xlsx");
            File.WriteAllText(source, "contenuto del report");
            File.WriteAllText(destination, "file esistente, tenuto aperto");

            using (var blocco = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Assert.ThrowsAny<IOException>(() => FileOperationRetry.MoveSafely(source, destination, maxAttempts: 2, initialDelayMs: 1));
            }

            Assert.True(File.Exists(source), "L'origine è stata rimossa nonostante la copia non sia riuscita.");
            Assert.Equal("contenuto del report", File.ReadAllText(source));
        }

        [Fact]
        public void MoveSafely_QuandoRiesce_SpostaDavvero()
        {
            string source = Path.Combine(_root.FullName, "origine_ok.xlsx");
            string destination = Path.Combine(_root.FullName, "destinazione_ok.xlsx");
            File.WriteAllText(source, "contenuto del report");

            FileOperationRetry.MoveSafely(source, destination);

            Assert.False(File.Exists(source));
            Assert.Equal("contenuto del report", File.ReadAllText(destination));
        }

        // -----------------------------------------------------------------------------------
        // 3. Verifica dopo la scrittura
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// La riga di destinazione già occupata non viene nascosta: l'esito la segnala, ed è ciò che
        /// finisce nel log diagnostico. Prima, una sovrascrittura era indistinguibile da un inserimento.
        /// </summary>
        [Fact]
        public void ScritturaSuRigaOccupata_VieneSegnalataNellEsito()
        {
            string file = Path.Combine(_root.FullName, "occupata.xlsx");
            GhostRowsReportBuilder.Create(file);

            var esito = ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, rowNumber: 5, RigaDiProva);

            Assert.True(esito.RowWasOccupied, "La riga 5 conteneva dati: l'esito doveva dirlo.");
            Assert.Contains("riga già occupata: SÌ", esito.ToLogEntry());
            Assert.Equal("Milano Martesana", esito.ValuesBefore[3]); // stringa condivisa risolta
            Assert.Equal("Napoli Gianturco", esito.ValuesAfter[3]);
        }

        // -----------------------------------------------------------------------------------
        // 4. Tabelle strutturate (ListObject)
        // -----------------------------------------------------------------------------------

        private static XElement TableXml(string filePath)
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            using var stream = archive.GetEntry("xl/tables/table1.xml")!.Open();
            return XDocument.Load(stream).Root!;
        }

        /// <summary>
        /// Una riga scritta sotto l'ultima riga di una tabella strutturata deve entrare nel suo
        /// intervallo: altrimenti Excel la mostra fuori tabella, i filtri la ignorano e in alcuni casi
        /// il file viene segnalato come da riparare. Nessuno dei quattro report aziendali usa oggi una
        /// tabella — questo copre la struttura, non il file di oggi.
        /// </summary>
        [Fact]
        public void ScritturaSottoUnaTabella_NeEstendeLIntervallo()
        {
            string file = Path.Combine(_root.FullName, "tabella.xlsm");
            ReportTemplateBuilder.Create(file);

            Assert.Equal("G1:H10", TableXml(file).Attribute("ref")?.Value);

            // La tabella copre le righe 1-10: la riga 11 le sta subito sotto.
            ReportInterventiWriter.WriteRow(file, ReportTemplateBuilder.SheetName, rowNumber: 11,
                new Dictionary<int, string?> { [7] = "COD-11", [8] = "Undicesima voce" });

            var table = TableXml(file);
            Assert.Equal("G1:H11", table.Attribute("ref")?.Value);
            Assert.Equal("G1:H11", table.Element(S + "autoFilter")?.Attribute("ref")?.Value);

            // Il contatore delle COLONNE non c'entra con le righe e non deve essere toccato: è
            // l'incremento che farebbe dichiarare il file danneggiato.
            Assert.Equal("2", table.Element(S + "tableColumns")?.Attribute("count")?.Value);
            Assert.Equal(2, table.Element(S + "tableColumns")?.Elements().Count());
        }

        [Fact]
        public void ScritturaDentroLaTabella_NonNeCambiaLIntervallo()
        {
            string file = Path.Combine(_root.FullName, "tabella_interna.xlsm");
            ReportTemplateBuilder.Create(file);

            ReportInterventiWriter.WriteRow(file, ReportTemplateBuilder.SheetName, rowNumber: 5, RigaDiProva);

            Assert.Equal("G1:H10", TableXml(file).Attribute("ref")?.Value);
        }

        [Fact]
        public void EsitoDellaScrittura_RiportaFoglioRigaEValoriRiletti()
        {
            string file = Path.Combine(_root.FullName, "esito.xlsx");
            GhostRowsReportBuilder.Create(file);

            var esito = ReportInterventiWriter.WriteRow(file, GhostRowsReportBuilder.SheetName, 11, RigaDiProva);

            Assert.Equal(GhostRowsReportBuilder.SheetName, esito.SheetName);
            Assert.Equal(11, esito.RowNumber);

            // La data è riletta come seriale OADate, la forma in cui Excel la memorizza davvero.
            Assert.Equal(new DateTime(2026, 3, 15).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture),
                esito.ValuesAfter[2]);

            string log = esito.ToLogEntry();
            Assert.Contains(GhostRowsReportBuilder.SheetName, log);
            Assert.Contains("riga 11", log);
        }
    }
}
