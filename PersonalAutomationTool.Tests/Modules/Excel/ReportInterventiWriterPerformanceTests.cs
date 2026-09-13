using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using PersonalAutomationTool.Modules.Excel;
using Xunit;
using Xunit.Abstractions;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Fixture condivisa: il report "gonfio" viene costruito **una volta sola** per l'intera classe.
    /// Generarlo a ogni test costerebbe più dei test stessi.
    /// </summary>
    public sealed class BloatedReportFixture : IDisposable
    {
        internal const int DataRows = 2_000;
        internal const int EmptyRows = 300_000;

        internal string TemplatePath { get; }

        public BloatedReportFixture()
        {
            TemplatePath = Path.Combine(Path.GetTempPath(), "pat_bloated_" + Guid.NewGuid().ToString("N") + ".xlsx");
            BloatedReportBuilder.Create(TemplatePath, DataRows, EmptyRows);
        }

        public void Dispose()
        {
            try { if (File.Exists(TemplatePath)) File.Delete(TemplatePath); } catch { /* pulizia best-effort */ }
        }
    }

    /// <summary>
    /// Tier 2 (file veri su disco): verifica che la scrittura del Report Interventi resti **in
    /// streaming** anche su un foglio con centinaia di migliaia di righe, e che la compattazione
    /// rimuova i segnaposto senza toccare nient'altro.
    ///
    /// <para>
    /// <b>Perché queste prove esistono.</b> Il committente ha segnalato "Scrivi report"
    /// estremamente lento. La diagnosi (Sprint 22, §6.1-vicies-quater) ha trovato un report reale con
    /// <c>sheet1.xml</c> da 59 MB, di cui 40 di soli elementi <c>&lt;row/&gt;</c> vuoti, letto per
    /// intero **due volte** a ogni salvataggio: una da ClosedXML e una da Excel via COM. La difesa
    /// contro il ritorno di quel costo non è il cronometro — troppo instabile fra macchine — ma la
    /// **memoria allocata**: un percorso che materializza il DOM alloca un ordine di grandezza in
    /// più di uno che trasmette in streaming, indipendentemente da quanto è veloce il computer.
    /// I tempi vengono comunque stampati, perché sono l'informazione che serve a chi rimisura.
    /// </para>
    /// </summary>
    public sealed class ReportInterventiWriterPerformanceTests : IClassFixture<BloatedReportFixture>, IDisposable
    {
        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        private readonly BloatedReportFixture _fixture;
        private readonly ITestOutputHelper _output;
        private readonly List<string> _temporaryFiles = [];

        /// <summary>Prima riga libera del foglio generato: l'intestazione occupa la 1.</summary>
        private static int FirstFreeRow => BloatedReportFixture.DataRows + 2;

        private static readonly Dictionary<int, string?> Riga = new()
        {
            [2] = "13/09/2026",
            [3] = "Milano",
            [4] = "1258999",
            [7] = "811",
            [12] = "Intervento di prova"
        };

        public ReportInterventiWriterPerformanceTests(BloatedReportFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        public void Dispose()
        {
            foreach (var file in _temporaryFiles)
            {
                try { if (File.Exists(file)) File.Delete(file); } catch { /* pulizia best-effort */ }
            }
        }

        private string WorkingCopy()
        {
            string path = Path.Combine(Path.GetTempPath(), "pat_bench_" + Guid.NewGuid().ToString("N") + ".xlsx");
            File.Copy(_fixture.TemplatePath, path);
            _temporaryFiles.Add(path);
            return path;
        }

        private static XElement SheetRoot(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            using var stream = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            return XDocument.Load(stream).Root!;
        }

        private static long SheetPartSize(string path)
        {
            using var archive = ZipFile.OpenRead(path);
            return archive.GetEntry("xl/worksheets/sheet1.xml")!.Length;
        }

        // -----------------------------------------------------------------------------------
        // Individuazione della riga di destinazione
        // -----------------------------------------------------------------------------------

        [Fact]
        public void FindLastFilledRow_IgnoraSegnapostoERighePreformattate()
        {
            string path = WorkingCopy();

            var stopwatch = Stopwatch.StartNew();
            int lastFilled = ReportInterventiWriter.FindLastFilledRow(path, [2, 3, 4, 7]);
            stopwatch.Stop();
            _output.WriteLine($"FindLastFilledRow: {stopwatch.ElapsedMilliseconds} ms");

            // Le righe preformattate hanno 27 celle ciascuna, ma tutte prive di valore: non contano.
            Assert.Equal(BloatedReportFixture.DataRows + 1, lastFilled);
        }

        [Fact]
        public void FindLastFilledRow_ContaSoloLeColonneChieste_EIntestazioneInclusa()
        {
            string path = WorkingCopy();

            // Nella fixture la colonna E è valorizzata solo nella riga 1 (intestazione): tutte le
            // righe dati la lasciano vuota. Il risultato è quindi 1, non l'ultima riga dati.
            //
            // L'intestazione NON è un caso speciale, ed è corretto così: su un report con le sole
            // intestazioni il chiamante ottiene 1 e riparte dalla riga 2, cioè la prima riga utile.
            Assert.Equal(1, ReportInterventiWriter.FindLastFilledRow(path, [5]));
        }

        // -----------------------------------------------------------------------------------
        // Scrittura su foglio enorme
        // -----------------------------------------------------------------------------------

        [Fact]
        public void WriteRow_SuFoglioEnorme_NonMaterializzaIlFoglioInMemoria()
        {
            string path = WorkingCopy();
            long partSize = SheetPartSize(path);

            long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = Stopwatch.StartNew();
            ReportInterventiWriter.WriteRow(path, FirstFreeRow, Riga);
            stopwatch.Stop();
            long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

            _output.WriteLine($"parte sheet1: {partSize / 1024 / 1024} MB");
            _output.WriteLine($"WriteRow: {stopwatch.ElapsedMilliseconds} ms, allocati {allocated / 1024 / 1024} MB");

            // Il percorso DOM precedente allocava ~16 volte la dimensione della parte (925 MB per una
            // parte da 59 MB, misurato sul report reale); lo streaming ne alloca ~6. Il limite è a 10:
            // abbastanza alto da non dipendere dal rumore di misura, abbastanza basso da scattare se
            // qualcuno rimettesse in mezzo un caricamento del foglio in memoria.
            Assert.True(allocated < partSize * 10,
                $"Allocati {allocated / 1024 / 1024} MB per una parte da {partSize / 1024 / 1024} MB: " +
                "sembra essere tornato un percorso che carica il foglio in memoria.");
        }

        [Fact]
        public void WriteRow_SuFoglioEnorme_ScriveIValoriEPreservaLaStruttura()
        {
            string path = WorkingCopy();
            var before = SheetRoot(path);

            ReportInterventiWriter.WriteRow(path, FirstFreeRow, Riga);

            var after = SheetRoot(path);

            // Elementi strutturali invariati.
            Assert.Equal(
                before.Element(S + "autoFilter")?.Attribute("ref")?.Value,
                after.Element(S + "autoFilter")?.Attribute("ref")?.Value);
            Assert.Equal(
                before.Element(S + "dataValidations")!.Elements().Count(),
                after.Element(S + "dataValidations")!.Elements().Count());
            Assert.Equal(
                before.Element(S + "mergeCells")!.Elements().Count(),
                after.Element(S + "mergeCells")!.Elements().Count());
            Assert.NotNull(after.Element(S + "pageMargins"));

            // Valori scritti nella riga giusta.
            var row = after.Element(S + "sheetData")!.Elements(S + "row")
                .Single(r => r.Attribute("r")!.Value == FirstFreeRow.ToString());

            static string CellText(XElement row, string reference) =>
                row.Elements(S + "c").Single(c => c.Attribute("r")!.Value == reference).Value;

            Assert.Equal(new DateTime(2026, 9, 13).ToOADate().ToString(System.Globalization.CultureInfo.InvariantCulture), CellText(row, "B" + FirstFreeRow));
            Assert.Equal("Milano", CellText(row, "C" + FirstFreeRow));
            Assert.Equal("1258999", CellText(row, "D" + FirstFreeRow));
            Assert.Equal("Intervento di prova", CellText(row, "L" + FirstFreeRow));

            // La riga di destinazione era preformattata: le sue celle esistevano già con il loro
            // stile, che deve essere sopravvissuto alla scrittura.
            Assert.Equal("3", row.Elements(S + "c").Single(c => c.Attribute("r")!.Value == "C" + FirstFreeRow).Attribute("s")?.Value);
        }

        [Fact]
        public void WriteRow_NonToccaLeRighePreesistenti()
        {
            string path = WorkingCopy();
            var before = SheetRoot(path).Element(S + "sheetData")!.Elements(S + "row")
                .First(r => r.Attribute("r")!.Value == "2");

            ReportInterventiWriter.WriteRow(path, FirstFreeRow, Riga);

            var after = SheetRoot(path).Element(S + "sheetData")!.Elements(S + "row")
                .First(r => r.Attribute("r")!.Value == "2");

            // Comprese la formula della numerazione progressiva e il valore in cache.
            Assert.True(XNode.DeepEquals(before, after), "Una riga dati preesistente è stata modificata.");
        }

        // -----------------------------------------------------------------------------------
        // Compattazione
        // -----------------------------------------------------------------------------------

        [Fact]
        public void CompactEmptyRows_RimuoveISoliSegnaposto()
        {
            string path = WorkingCopy();
            long sizeBefore = SheetPartSize(path);

            var stopwatch = Stopwatch.StartNew();
            int removed = ReportInterventiWriter.CompactEmptyRows(path);
            stopwatch.Stop();

            long sizeAfter = SheetPartSize(path);
            _output.WriteLine($"CompactEmptyRows: {removed} righe in {stopwatch.ElapsedMilliseconds} ms, " +
                              $"parte {sizeBefore / 1024 / 1024} MB -> {sizeAfter / 1024 / 1024} MB");

            Assert.Equal(BloatedReportFixture.EmptyRows, removed);
            Assert.True(sizeAfter < sizeBefore);

            var root = SheetRoot(path);
            var rows = root.Element(S + "sheetData")!.Elements(S + "row").ToList();

            // Restano: intestazione + righe dati + righe preformattate + la riga con altezza propria.
            Assert.Equal(1 + BloatedReportFixture.DataRows + BloatedReportBuilder.PreformattedRows + 1, rows.Count);
        }

        [Fact]
        public void CompactEmptyRows_ConservaLeRigheCheportanoInformazione()
        {
            string path = WorkingCopy();
            int informative = BloatedReportBuilder.InformativeEmptyRowIndex(
                BloatedReportFixture.DataRows, BloatedReportFixture.EmptyRows);

            ReportInterventiWriter.CompactEmptyRows(path);

            var rows = SheetRoot(path).Element(S + "sheetData")!.Elements(S + "row").ToList();

            // Riga vuota ma con altezza esplicita: non è un segnaposto.
            var survivor = rows.SingleOrDefault(r => r.Attribute("r")!.Value == informative.ToString());
            Assert.NotNull(survivor);
            Assert.Equal("30", survivor!.Attribute("ht")?.Value);

            // Le righe preformattate (celle senza valori) non sono segnaposto e restano.
            Assert.Contains(rows, r => r.Attribute("r")!.Value == (BloatedReportFixture.DataRows + 2).ToString());
        }

        [Fact]
        public void CompactEmptyRows_AzzeraZeroHeight_AltrimentiLeRigheRimosseSparirebberoAVideo()
        {
            string path = WorkingCopy();
            Assert.Equal("1", SheetRoot(path).Element(S + "sheetFormatPr")!.Attribute("zeroHeight")?.Value);

            ReportInterventiWriter.CompactEmptyRows(path);

            var format = SheetRoot(path).Element(S + "sheetFormatPr")!;
            Assert.Equal("0", format.Attribute("zeroHeight")?.Value);

            // Gli altri attributi di formato restano quelli di prima.
            Assert.Equal("13.2", format.Attribute("defaultRowHeight")?.Value);
            Assert.Equal("8.5546875", format.Attribute("defaultColWidth")?.Value);
        }

        [Fact]
        public void CompactEmptyRows_NonAlteraDatiNeStruttura()
        {
            string path = WorkingCopy();
            var before = SheetRoot(path);
            var rowBefore = before.Element(S + "sheetData")!.Elements(S + "row").First(r => r.Attribute("r")!.Value == "2");

            ReportInterventiWriter.CompactEmptyRows(path);

            var after = SheetRoot(path);
            var rowAfter = after.Element(S + "sheetData")!.Elements(S + "row").First(r => r.Attribute("r")!.Value == "2");

            Assert.True(XNode.DeepEquals(rowBefore, rowAfter), "La compattazione ha alterato una riga con dati.");
            Assert.Equal(before.Element(S + "autoFilter")?.Attribute("ref")?.Value, after.Element(S + "autoFilter")?.Attribute("ref")?.Value);
            Assert.Equal(before.Element(S + "dataValidations")!.Elements().Count(), after.Element(S + "dataValidations")!.Elements().Count());
            Assert.Equal(before.Element(S + "mergeCells")!.Elements().Count(), after.Element(S + "mergeCells")!.Elements().Count());
            Assert.Equal(before.Element(S + "cols")!.Elements().Count(), after.Element(S + "cols")!.Elements().Count());
        }

        [Fact]
        public void ScritturaDopoCompattazione_RestaCorrettaEPiuLeggera()
        {
            string path = WorkingCopy();

            var stopwatch = Stopwatch.StartNew();
            ReportInterventiWriter.WriteRow(path, FirstFreeRow, Riga);
            stopwatch.Stop();
            long bloated = stopwatch.ElapsedMilliseconds;
            long sizeBloated = SheetPartSize(path);

            ReportInterventiWriter.CompactEmptyRows(path);

            stopwatch.Restart();
            ReportInterventiWriter.WriteRow(path, FirstFreeRow + 1, Riga);
            stopwatch.Stop();
            long compacted = stopwatch.ElapsedMilliseconds;

            _output.WriteLine($"scrittura su foglio gonfio: {bloated} ms ({sizeBloated / 1024 / 1024} MB)");
            _output.WriteLine($"scrittura dopo compattazione: {compacted} ms ({SheetPartSize(path) / 1024 / 1024} MB)");

            // Il dato verificabile in modo stabile è la dimensione, non il cronometro.
            Assert.True(SheetPartSize(path) < sizeBloated);

            var row = SheetRoot(path).Element(S + "sheetData")!.Elements(S + "row")
                .Single(r => r.Attribute("r")!.Value == (FirstFreeRow + 1).ToString());
            Assert.Equal("Milano", row.Elements(S + "c").Single(c => c.Attribute("r")!.Value == "C" + (FirstFreeRow + 1)).Value);
        }
    }
}
