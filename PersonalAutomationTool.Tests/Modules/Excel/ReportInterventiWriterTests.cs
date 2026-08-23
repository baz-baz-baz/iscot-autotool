using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PersonalAutomationTool.Modules.Excel;
using Xunit;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Suite di **integrità strutturale** del Report Interventi: verifica che una scrittura di riga
    /// lasci il pacchetto OpenXML identico ovunque tranne che nei nuovi elementi
    /// <c>&lt;row&gt;</c>/<c>&lt;c&gt;</c>.
    ///
    /// <para>
    /// L'ispezione avviene sul pacchetto vero e proprio: il <c>.xlsm</c> è un archivio ZIP, e i test
    /// lo decomprimono per confrontare le singole parti byte per byte prima e dopo la scrittura.
    /// Nessuna asserzione si basa su ciò che la libreria <i>dichiara</i> di aver fatto.
    /// </para>
    /// </summary>
    public sealed class ReportInterventiWriterTests : IDisposable
    {
        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("ReportIntegrity_");

        public void Dispose()
        {
            try { _root.Delete(recursive: true); } catch { /* best-effort cleanup */ }
        }

        /// <summary>Crea il file di prova e ne conserva una copia intatta come termine di paragone.</summary>
        private (string Working, string Pristine) CreateTemplatePair(string name)
        {
            string working = Path.Combine(_root.FullName, name + ".xlsm");
            string pristine = Path.Combine(_root.FullName, name + ".before.xlsm");

            ReportTemplateBuilder.Create(working);
            File.Copy(working, pristine);

            return (working, pristine);
        }

        /// <summary>Contenuto grezzo di ogni voce dell'archivio ZIP, indicizzato per nome completo.</summary>
        private static Dictionary<string, byte[]> ReadPackageEntries(string filePath)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            using var archive = ZipFile.OpenRead(filePath);
            foreach (var entry in archive.Entries)
            {
                using var entryStream = entry.Open();
                using var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                result[entry.FullName] = buffer.ToArray();
            }

            return result;
        }

        private static readonly Dictionary<int, string?> RigaDiProva = new()
        {
            [1] = "15/03/2026",
            [2] = "Torino",
            [3] = "Preventiva",
            [4] = "42"
            // Colonna 5 (E) deliberatamente assente: contiene una formula e non deve essere toccata.
        };

        // -----------------------------------------------------------------------------------
        // 1. Parti binarie e condivise: devono restare identiche byte per byte
        // -----------------------------------------------------------------------------------

        [Fact]
        public void ScritturaRiga_VbaProjectBin_RestaIdenticoByteAByte()
        {
            var (working, pristine) = CreateTemplatePair("vba");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var before = ReadPackageEntries(pristine);
            var after = ReadPackageEntries(working);

            const string vbaPath = "xl/vbaProject.bin";
            Assert.True(before.ContainsKey(vbaPath), "Il file di prova deve contenere un progetto VBA.");
            Assert.True(after.ContainsKey(vbaPath), "Il progetto VBA è sparito dopo la scrittura.");
            Assert.Equal(before[vbaPath], after[vbaPath]);
            Assert.Equal(ReportTemplateBuilder.VbaProjectBytes, after[vbaPath]);
        }

        [Fact]
        public void ScritturaRiga_StylesESharedStrings_RestanoIdentici()
        {
            var (working, pristine) = CreateTemplatePair("styles");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var before = ReadPackageEntries(pristine);
            var after = ReadPackageEntries(working);

            // styles.xml: nessuno stile nuovo viene creato (le celle ereditano lo StyleIndex).
            Assert.Equal(before["xl/styles.xml"], after["xl/styles.xml"]);

            // sharedStrings.xml: la scrittura usa stringhe inline proprio per non toccarlo.
            Assert.Equal(before["xl/sharedStrings.xml"], after["xl/sharedStrings.xml"]);
        }

        [Fact]
        public void ScritturaRiga_TabellaERelazioni_RestanoIdentiche()
        {
            var (working, pristine) = CreateTemplatePair("table_rels");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var before = ReadPackageEntries(pristine);
            var after = ReadPackageEntries(working);

            foreach (var name in before.Keys.Where(k => k.Contains("/tables/") || k.EndsWith(".rels", StringComparison.Ordinal) || k == "[Content_Types].xml"))
            {
                Assert.True(after.ContainsKey(name), $"La parte '{name}' è sparita dopo la scrittura.");
                Assert.Equal(before[name], after[name]);
            }
        }

        [Fact]
        public void ScritturaRiga_NessunaParteDelPacchettoVieneAggiuntaOrimossa()
        {
            var (working, pristine) = CreateTemplatePair("parts");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var before = ReadPackageEntries(pristine).Keys.OrderBy(k => k, StringComparer.Ordinal);
            var after = ReadPackageEntries(working).Keys.OrderBy(k => k, StringComparer.Ordinal);

            Assert.Equal(before, after);
        }

        [Fact]
        public void ScritturaRiga_LUnicaParteModificataEIlFoglio()
        {
            var (working, pristine) = CreateTemplatePair("only_sheet");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var before = ReadPackageEntries(pristine);
            var after = ReadPackageEntries(working);

            var modified = before.Keys
                .Where(name => !before[name].SequenceEqual(after[name]))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(["xl/worksheets/sheet1.xml"], modified);
        }

        // -----------------------------------------------------------------------------------
        // 2. Elementi strutturali del foglio: presenti e formalmente validi dopo la scrittura
        // -----------------------------------------------------------------------------------

        private static XDocument ReadSheetXml(string filePath)
        {
            using var archive = ZipFile.OpenRead(filePath);
            var entry = archive.GetEntry("xl/worksheets/sheet1.xml")!;
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }

        private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        /// <summary>
        /// Confronto **semantico** di due elementi XML: nome completo, attributi (per nome, non per
        /// posizione) e figli in ordine.
        ///
        /// <para>
        /// Necessario perché <see cref="XNode.DeepEquals"/> tratta le dichiarazioni di namespace come
        /// attributi posizionali: dopo un salvataggio l'SDK può emettere <c>xmlns:r</c> prima di
        /// <c>r:id</c> anziché dopo, producendo XML byte-diverso ma **identico per significato**.
        /// Considerarlo una modifica strutturale sarebbe un falso positivo. Gli attributi di
        /// namespace vengono quindi esclusi dal confronto, mentre tutto il resto — compresi i valori
        /// degli attributi, che è ciò che conta davvero per convalide, filtri e tabelle — è
        /// confrontato in modo esatto.
        /// </para>
        /// </summary>
        private static bool XmlSemanticEquals(XElement? a, XElement? b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.Name != b.Name) return false;

            static Dictionary<XName, string> RealAttributes(XElement e) =>
                e.Attributes().Where(at => !at.IsNamespaceDeclaration).ToDictionary(at => at.Name, at => at.Value);

            var attributesA = RealAttributes(a);
            var attributesB = RealAttributes(b);
            if (attributesA.Count != attributesB.Count) return false;
            foreach (var (name, value) in attributesA)
            {
                if (!attributesB.TryGetValue(name, out var other) || other != value) return false;
            }

            var childrenA = a.Elements().ToList();
            var childrenB = b.Elements().ToList();
            if (childrenA.Count != childrenB.Count) return false;

            // Nodo foglia: confronta anche il testo.
            if (childrenA.Count == 0) return a.Value == b.Value;

            return !childrenA.Where((child, i) => !XmlSemanticEquals(child, childrenB[i])).Any();
        }

        [Theory]
        [InlineData("dataValidations")]
        [InlineData("conditionalFormatting")]
        [InlineData("autoFilter")]
        [InlineData("tableParts")]
        [InlineData("sheetProtection")]
        public void ScritturaRiga_ElementiStrutturali_RestanoPresentiEIdentici(string elementName)
        {
            var (working, pristine) = CreateTemplatePair("struct_" + elementName);

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var beforeElement = ReadSheetXml(pristine).Root!.Element(S + elementName);
            var afterElement = ReadSheetXml(working).Root!.Element(S + elementName);

            Assert.NotNull(beforeElement);
            Assert.NotNull(afterElement);
            // Confronto di struttura, attributi e contenuto: non solo della presenza.
            Assert.True(XmlSemanticEquals(beforeElement, afterElement),
                $"L'elemento <{elementName}> è cambiato:\nPRIMA: {beforeElement}\nDOPO:  {afterElement}");
        }

        [Fact]
        public void ScritturaRiga_IlPacchettoRestaApribileEValido()
        {
            var (working, _) = CreateTemplatePair("valid");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            // Se l'ordine degli elementi o dei riferimenti fosse errato, l'apertura fallirebbe.
            using var document = SpreadsheetDocument.Open(working, isEditable: false);
            var workbookPart = document.WorkbookPart!;
            Assert.NotNull(workbookPart.VbaProjectPart);
            Assert.Single(workbookPart.Workbook.Sheets!.Elements<Sheet>());

            var worksheetPart = workbookPart.WorksheetParts.First();
            Assert.NotNull(worksheetPart.Worksheet.GetFirstChild<SheetData>());
            Assert.Single(worksheetPart.TableDefinitionParts);
        }

        // -----------------------------------------------------------------------------------
        // 3. La differenza nel foglio è limitata ai nuovi <row>/<c>
        // -----------------------------------------------------------------------------------

        [Fact]
        public void ScritturaRiga_LUnicaDifferenzaNelFoglioELaNuovaRiga()
        {
            var (working, pristine) = CreateTemplatePair("diff");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var beforeSheet = ReadSheetXml(pristine).Root!;
            var afterSheet = ReadSheetXml(working).Root!;

            // Tutti gli elementi di primo livello diversi da sheetData devono essere invariati.
            var beforeOthers = beforeSheet.Elements().Where(e => e.Name != S + "sheetData").ToList();
            var afterOthers = afterSheet.Elements().Where(e => e.Name != S + "sheetData").ToList();

            Assert.Equal(beforeOthers.Count, afterOthers.Count);
            for (int i = 0; i < beforeOthers.Count; i++)
            {
                Assert.True(XmlSemanticEquals(beforeOthers[i], afterOthers[i]),
                    $"Elemento <{beforeOthers[i].Name.LocalName}> modificato dalla scrittura.");
            }

            // Dentro sheetData, le righe preesistenti devono essere invariate...
            var beforeRows = beforeSheet.Element(S + "sheetData")!.Elements(S + "row").ToList();
            var afterRows = afterSheet.Element(S + "sheetData")!.Elements(S + "row").ToList();

            Assert.Equal(beforeRows.Count + 1, afterRows.Count);
            for (int i = 0; i < beforeRows.Count; i++)
            {
                Assert.True(XmlSemanticEquals(beforeRows[i], afterRows[i]),
                    $"La riga preesistente r={beforeRows[i].Attribute("r")?.Value} è stata modificata.");
            }

            // ...e l'unica aggiunta è la riga 5.
            var newRow = afterRows[^1];
            Assert.Equal("5", newRow.Attribute("r")?.Value);
        }

        [Fact]
        public void ScritturaRiga_FormulaPreesistente_NonVieneToccata()
        {
            var (working, pristine) = CreateTemplatePair("formula");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            static XElement? CellOf(XDocument doc, string reference) =>
                doc.Root!.Element(S + "sheetData")!.Elements(S + "row")
                    .Elements(S + "c")
                    .FirstOrDefault(c => c.Attribute("r")?.Value == reference);

            var before = CellOf(ReadSheetXml(pristine), "E3");
            var after = CellOf(ReadSheetXml(working), "E3");

            Assert.NotNull(before);
            Assert.True(XmlSemanticEquals(before, after), "La formula preesistente in E3 è stata alterata.");
            Assert.Equal("D3*2", after!.Element(S + "f")?.Value);
        }

        [Fact]
        public void ScritturaRiga_ColonnaNonFornita_NonVieneCreata()
        {
            // Il percorso Interop salta le celle senza valore per non sovrascrivere formule o
            // formattazione: lo scrittore chirurgico deve comportarsi allo stesso modo.
            var (working, _) = CreateTemplatePair("skip_empty");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var newRow = ReadSheetXml(working).Root!
                .Element(S + "sheetData")!.Elements(S + "row")
                .First(r => r.Attribute("r")?.Value == "5");

            var references = newRow.Elements(S + "c").Select(c => c.Attribute("r")?.Value).ToList();

            Assert.Equal(["A5", "B5", "C5", "D5"], references);
            Assert.DoesNotContain("E5", references);
        }

        [Fact]
        public void ScritturaRiga_CelleNuove_EreditanoLoStileDallaRigaPrecedente()
        {
            var (working, _) = CreateTemplatePair("styleinherit");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var sheet = ReadSheetXml(working).Root!.Element(S + "sheetData")!;
            var previousRow = sheet.Elements(S + "row").First(r => r.Attribute("r")?.Value == "4");
            var newRow = sheet.Elements(S + "row").First(r => r.Attribute("r")?.Value == "5");

            static string? StyleOf(XElement row, string column) =>
                row.Elements(S + "c").FirstOrDefault(c => c.Attribute("r")?.Value?.StartsWith(column) == true)?.Attribute("s")?.Value;

            // La colonna A ha il formato data: senza ereditarietà la data apparirebbe come numero.
            Assert.Equal(StyleOf(previousRow, "A"), StyleOf(newRow, "A"));
            Assert.Equal(StyleOf(previousRow, "D"), StyleOf(newRow, "D"));
        }

        [Fact]
        public void ScritturaRiga_DataScrittaComeSerialeNonComeTesto()
        {
            var (working, _) = CreateTemplatePair("date");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var cell = ReadSheetXml(working).Root!
                .Element(S + "sheetData")!.Elements(S + "row")
                .First(r => r.Attribute("r")?.Value == "5")
                .Elements(S + "c").First(c => c.Attribute("r")?.Value == "A5");

            // Nessun t="inlineStr": è un numero, come Excel memorizza le date.
            Assert.Null(cell.Attribute("t"));
            double serial = double.Parse(cell.Element(S + "v")!.Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(new DateTime(2026, 3, 15), DateTime.FromOADate(serial));
        }

        [Fact]
        public void ScritturaRiga_TestoScrittoInlineSenzaToccareLaTabellaCondivisa()
        {
            var (working, pristine) = CreateTemplatePair("inline");

            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);

            var cell = ReadSheetXml(working).Root!
                .Element(S + "sheetData")!.Elements(S + "row")
                .First(r => r.Attribute("r")?.Value == "5")
                .Elements(S + "c").First(c => c.Attribute("r")?.Value == "B5");

            Assert.Equal("inlineStr", cell.Attribute("t")?.Value);
            Assert.Equal("Torino", cell.Element(S + "is")!.Element(S + "t")!.Value);

            var before = ReadPackageEntries(pristine)["xl/sharedStrings.xml"];
            var after = ReadPackageEntries(working)["xl/sharedStrings.xml"];
            Assert.Equal(before, after);
        }

        [Fact]
        public void ScrittureRipetute_SuRigheDiverse_MantengonoLOrdineERestanoValide()
        {
            var (working, pristine) = CreateTemplatePair("multi");

            // Inserimento volutamente fuori ordine: lo schema OpenXML richiede comunque che gli
            // elementi <row> risultino ordinati per indice crescente.
            ReportInterventiWriter.WriteRow(working, rowNumber: 7, RigaDiProva);
            ReportInterventiWriter.WriteRow(working, rowNumber: 5, RigaDiProva);
            ReportInterventiWriter.WriteRow(working, rowNumber: 6, RigaDiProva);

            var rows = ReadSheetXml(working).Root!
                .Element(S + "sheetData")!.Elements(S + "row")
                .Select(r => uint.Parse(r.Attribute("r")!.Value))
                .ToList();

            Assert.Equal(rows.OrderBy(r => r), rows);
            Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7 }, rows);

            // Le parti critiche restano intatte anche dopo tre scritture consecutive.
            var before = ReadPackageEntries(pristine);
            var after = ReadPackageEntries(working);
            Assert.Equal(before["xl/vbaProject.bin"], after["xl/vbaProject.bin"]);
            Assert.Equal(before["xl/styles.xml"], after["xl/styles.xml"]);
        }

        [Fact]
        public void ScritturaSuRigaEsistente_AggiornaSenzaDuplicareCelle()
        {
            var (working, _) = CreateTemplatePair("overwrite");

            ReportInterventiWriter.WriteRow(working, rowNumber: 3, new Dictionary<int, string?> { [2] = "Bologna" });

            var row = ReadSheetXml(working).Root!
                .Element(S + "sheetData")!.Elements(S + "row")
                .First(r => r.Attribute("r")?.Value == "3");

            var b3 = row.Elements(S + "c").Where(c => c.Attribute("r")?.Value == "B3").ToList();
            Assert.Single(b3);
            Assert.Equal("Bologna", b3[0].Element(S + "is")!.Element(S + "t")!.Value);
        }

        // -----------------------------------------------------------------------------------
        // 4. Confronto con un salvataggio ClosedXML: perché l'invariante §5.4 esiste
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// Misura la differenza fra i due approcci sullo stesso file, invece di darla per scontata.
        ///
        /// <para>
        /// <b>Esito documentato da questo test.</b> ClosedXML <i>non</i> elimina il progetto VBA —
        /// il timore più comune si rivela infondato — ma **riscrive l'intero pacchetto**: al
        /// salvataggio aggiunge parti che il file non conteneva (<c>docProps/app.xml</c>,
        /// <c>xl/calcChain.xml</c>, <c>xl/theme/theme1.xml</c> e i metadati di pacchetto) e
        /// ri-serializza quelle esistenti. Il writer chirurgico ne modifica **una sola**.
        /// </para>
        ///
        /// <para>
        /// È la giustificazione sperimentale dell'invariante §5.4 di PROJECT_MEMORY.md ("ClosedXML
        /// ammesso solo in lettura"): finora era una regola motivata a parole, ora è verificata da un
        /// test che fallirebbe se qualcuno introducesse un salvataggio ClosedXML sul percorso del
        /// Report Interventi.
        /// </para>
        /// </summary>
        [Fact]
        public void ConfrontoConClosedXml_IlSalvataggioDiClosedXmlRiscriveIlPacchetto()
        {
            var (viaClosedXml, pristine) = CreateTemplatePair("cx_compare");
            string viaSurgical = Path.Combine(_root.FullName, "cx_compare_surgical.xlsm");
            File.Copy(pristine, viaSurgical);

            // Percorso ClosedXML: apre, scrive una cella, salva.
            using (var workbook = new ClosedXML.Excel.XLWorkbook(viaClosedXml))
            {
                workbook.Worksheet(1).Cell(5, 1).Value = "Torino";
                workbook.Save();
            }

            // Percorso chirurgico: stessa scrittura logica.
            ReportInterventiWriter.WriteRow(viaSurgical, rowNumber: 5, new Dictionary<int, string?> { [1] = "Torino" });

            var before = ReadPackageEntries(pristine);
            var afterClosedXml = ReadPackageEntries(viaClosedXml);
            var afterSurgical = ReadPackageEntries(viaSurgical);

            var partsAddedByClosedXml = afterClosedXml.Keys.Except(before.Keys).ToList();
            var partsChangedByClosedXml = before.Keys
                .Where(k => !afterClosedXml.ContainsKey(k) || !before[k].SequenceEqual(afterClosedXml[k]))
                .ToList();
            var partsChangedBySurgical = before.Keys
                .Where(k => !afterSurgical.ContainsKey(k) || !before[k].SequenceEqual(afterSurgical[k]))
                .ToList();

            // ClosedXML introduce parti che il file non aveva.
            Assert.NotEmpty(partsAddedByClosedXml);

            // ...e tocca più parti preesistenti di quante ne tocchi la scrittura chirurgica, che ne
            // modifica esattamente una.
            Assert.True(partsChangedByClosedXml.Count > partsChangedBySurgical.Count,
                $"ClosedXML ha modificato {partsChangedByClosedXml.Count} parti, il writer chirurgico {partsChangedBySurgical.Count}.");
            Assert.Equal(["xl/worksheets/sheet1.xml"], partsChangedBySurgical);

            // Nota non ovvia, verificata qui: ClosedXML preserva comunque il binario VBA.
            // Il rischio reale non è la perdita delle macro, è la riscrittura dell'intero pacchetto.
            Assert.Equal(before["xl/vbaProject.bin"], afterClosedXml["xl/vbaProject.bin"]);
        }

        // -----------------------------------------------------------------------------------
        // 5. Conversione dei riferimenti di colonna
        // -----------------------------------------------------------------------------------

        [Theory]
        [InlineData(1, "A")]
        [InlineData(2, "B")]
        [InlineData(26, "Z")]
        [InlineData(27, "AA")]
        [InlineData(28, "AB")]
        [InlineData(55, "BC")]
        public void GetColumnName_ConverteNumeriInLettere(int columnNumber, string expected) =>
            Assert.Equal(expected, ReportInterventiWriter.GetColumnName(columnNumber));

        [Theory]
        [InlineData("A1", 1)]
        [InlineData("Z9", 26)]
        [InlineData("AA1", 27)]
        [InlineData("BC12", 55)]
        public void GetColumnNumber_ConverteLettereInNumeri(string reference, int expected) =>
            Assert.Equal(expected, ReportInterventiWriter.GetColumnNumber(reference));
    }
}
