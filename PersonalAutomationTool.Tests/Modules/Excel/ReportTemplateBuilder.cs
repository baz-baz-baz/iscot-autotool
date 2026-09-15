using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Costruisce un file <c>.xlsm</c> che riproduce le caratteristiche strutturali del "Report
    /// Interventi" reale, quelle che un salvataggio distruttivo perderebbe: progetto VBA binario,
    /// convalide dati, formattazione condizionale, filtro automatico, tabella (ListObject),
    /// protezione del foglio, formule, stili personalizzati e stringhe condivise.
    ///
    /// <para>
    /// Serve perché il file aziendale reale non è disponibile in questo ambiente e non potrebbe
    /// comunque essere versionato nel repository. Ciò che i test devono dimostrare non è «funziona
    /// su quel file», ma «nessuna di queste parti viene toccata»: per quello basta un pacchetto che
    /// le contenga tutte, costruito qui in modo esplicito e ispezionabile.
    /// </para>
    /// </summary>
    internal static class ReportTemplateBuilder
    {
        /// <summary>Contenuto binario fittizio del progetto VBA: ciò che conta nei test è che resti identico byte per byte.</summary>
        internal static readonly byte[] VbaProjectBytes =
            [.. Enumerable.Range(0, 512).Select(i => (byte)((i * 37 + 11) % 256))];

        internal const string SheetName = "Report";

        /// <summary>
        /// Crea il file di prova. Il foglio ha intestazioni in riga 1 e tre righe di dati (2-4), così
        /// che la scrittura di prova possa inserire la riga 5 come farebbe l'applicazione reale.
        /// </summary>
        internal static void Create(string filePath)
        {
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.MacroEnabledWorkbook);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // --- Progetto VBA (la parte binaria che un risalvataggio non consapevole eliminerebbe) ---
            var vbaPart = workbookPart.AddNewPart<VbaProjectPart>();
            using (var vbaStream = new MemoryStream(VbaProjectBytes))
            {
                vbaPart.FeedData(vbaStream);
            }

            // --- Stili personalizzati ---
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = BuildStylesheet();
            stylesPart.Stylesheet.Save();

            // --- Stringhe condivise (devono restare invariate: la scrittura usa stringhe inline) ---
            var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringPart.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("Milano")),
                new SharedStringItem(new Text("Correttiva")));
            sharedStringPart.SharedStringTable.Save();

            // --- Foglio ---
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = BuildSheetData();

            // L'ordine degli elementi dentro <worksheet> è vincolato dallo schema OpenXML:
            // sheetPr, dimension, sheetViews, sheetFormatPr, cols, sheetData, sheetProtection,
            // autoFilter, conditionalFormatting, dataValidations, ..., tableParts.
            var worksheet = new Worksheet();
            worksheet.AppendChild(new SheetDimension { Reference = "A1:E100" });
            worksheet.AppendChild(new SheetViews(new SheetView { WorkbookViewId = 0 }));
            worksheet.AppendChild(new SheetFormatProperties { DefaultRowHeight = 15D });
            worksheet.AppendChild(sheetData);

            worksheet.AppendChild(new SheetProtection
            {
                Sheet = true,
                Objects = true,
                Scenarios = true,
                SelectLockedCells = false,
                SelectUnlockedCells = false
            });

            worksheet.AppendChild(new AutoFilter { Reference = "A1:E100" });

            worksheet.AppendChild(new ConditionalFormatting(
                new ConditionalFormattingRule(
                    new Formula("$D2>100"))
                {
                    Type = ConditionalFormatValues.Expression,
                    FormatId = 0U,
                    Priority = 1
                })
            {
                SequenceOfReferences = new ListValue<StringValue> { InnerText = "A2:E100" }
            });

            worksheet.AppendChild(new DataValidations(
                new DataValidation(new Formula1("\"Correttiva,Preventiva,Mis\""))
                {
                    Type = DataValidationValues.List,
                    AllowBlank = true,
                    ShowInputMessage = true,
                    ShowErrorMessage = true,
                    SequenceOfReferences = new ListValue<StringValue> { InnerText = "C2:C100" }
                })
            { Count = 1U });

            worksheetPart.Worksheet = worksheet;

            // --- Tabella (ListObject) ---
            // La tabella sta su un range DIVERSO da quello dell'autoFilter di foglio (colonne G-H,
            // come una tabella di supporto per gli elenchi di convalida): Excel non consente a un
            // filtro automatico di foglio e a una tabella di insistere sullo stesso intervallo, e un
            // template che lo facesse non rappresenterebbe un file reale.
            var tableDefinitionPart = worksheetPart.AddNewPart<TableDefinitionPart>("rIdTable1");
            tableDefinitionPart.Table = new Table
            {
                Id = 1U,
                Name = "TabellaSupporto",
                DisplayName = "TabellaSupporto",
                Reference = "G1:H10",
                TotalsRowShown = false,
                AutoFilter = new AutoFilter { Reference = "G1:H10" },
                TableColumns = new TableColumns(
                    new TableColumn { Id = 1U, Name = "Codice" },
                    new TableColumn { Id = 2U, Name = "Descrizione" })
                { Count = 2U },
                TableStyleInfo = new TableStyleInfo
                {
                    Name = "TableStyleMedium2",
                    ShowRowStripes = true
                }
            };
            tableDefinitionPart.Table.Save();

            worksheet.AppendChild(new TableParts(new TablePart { Id = "rIdTable1" }) { Count = 1U });
            worksheetPart.Worksheet.Save();

            workbookPart.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = SheetName
            }));

            // --- Nome definito, usato dalle convalide dei report reali ---
            workbookPart.Workbook.AppendChild(new DefinedNames(new DefinedName("Report!$A$1:$E$100")
            {
                Name = "AreaDati"
            }));

            workbookPart.Workbook.Save();
        }

        /// <summary>
        /// Crea un workbook a **due fogli**, che riproduce la forma del report reale che ha causato il
        /// bug ETR500 (PROJECT_MEMORY.md): una scheda "istruzioni" **prima**, in ordine di tabulazione,
        /// del foglio Interventi. Un writer che risolve il foglio per posizione invece che per nome
        /// scriverebbe qui nel foglio sbagliato — esattamente ciò che
        /// <c>ScritturaRiga_ScriveSoloNelFoglioInterventi_NonInIstruzioni</c> verifica.
        /// </summary>
        /// <param name="filePath">Percorso del file da creare.</param>
        /// <param name="targetSheetName">Nome del foglio Interventi (secondo in ordine di scheda).</param>
        /// <param name="instructionsSheetName">Nome del foglio istruzioni (primo in ordine di scheda).</param>
        internal static void CreateMultiSheet(string filePath, string targetSheetName, string instructionsSheetName = "Istruzioni")
        {
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            stylesPart.Stylesheet = BuildStylesheet();
            stylesPart.Stylesheet.Save();

            // BuildSheetData() referenzia le stringhe condivise "Milano"/"Correttiva" per indice:
            // la parte deve esistere perché il pacchetto resti valido, come in Create().
            var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringPart.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("Milano")),
                new SharedStringItem(new Text("Correttiva")));
            sharedStringPart.SharedStringTable.Save();

            // --- Foglio istruzioni: contenuto arbitrario che deve restare intatto. ---
            var instructionsPart = workbookPart.AddNewPart<WorksheetPart>();
            var instructionsSheetData = new SheetData();
            instructionsSheetData.AppendChild(BuildRow(1, (1, "A1", "Leggere attentamente prima di compilare", 1U)));
            instructionsSheetData.AppendChild(BuildRow(2, (1, "A2", "Non modificare le altre schede", 0U)));
            var instructionsWorksheet = new Worksheet(new SheetFormatProperties { DefaultRowHeight = 15D }, instructionsSheetData);
            instructionsPart.Worksheet = instructionsWorksheet;
            instructionsPart.Worksheet.Save();

            // --- Foglio Interventi: stessa forma di Create() (intestazioni + 3 righe dati). ---
            var targetPart = workbookPart.AddNewPart<WorksheetPart>();
            var targetSheetData = BuildSheetData();
            var targetWorksheet = new Worksheet(new SheetFormatProperties { DefaultRowHeight = 15D }, targetSheetData);
            targetPart.Worksheet = targetWorksheet;
            targetPart.Worksheet.Save();

            // L'ordine delle schede è deliberato: "istruzioni" prima, il foglio Interventi dopo — la
            // forma esatta del report ETR500 reale che scriveva nel foglio sbagliato.
            workbookPart.Workbook.AppendChild(new Sheets(
                new Sheet { Id = workbookPart.GetIdOfPart(instructionsPart), SheetId = 1U, Name = instructionsSheetName },
                new Sheet { Id = workbookPart.GetIdOfPart(targetPart), SheetId = 2U, Name = targetSheetName }));

            workbookPart.Workbook.Save();
        }

        private static Stylesheet BuildStylesheet()
        {
            return new Stylesheet(
                new NumberingFormats(
                    new NumberingFormat { NumberFormatId = 164U, FormatCode = "dd/mm/yyyy" })
                { Count = 1U },
                new Fonts(
                    new Font(new FontSize { Val = 11D }, new FontName { Val = "Calibri" }),
                    new Font(new Bold(), new FontSize { Val = 12D }, new FontName { Val = "Calibri" }))
                { Count = 2U },
                new Fills(
                    new Fill(new PatternFill { PatternType = PatternValues.None }),
                    new Fill(new PatternFill { PatternType = PatternValues.Gray125 }))
                { Count = 2U },
                new Borders(new Border())
                { Count = 1U },
                new CellStyleFormats(new CellFormat())
                { Count = 1U },
                new CellFormats(
                    new CellFormat { FontId = 0U, FillId = 0U, BorderId = 0U },                                  // 0: normale
                    new CellFormat { FontId = 1U, FillId = 0U, BorderId = 0U, ApplyFont = true },                 // 1: intestazione
                    new CellFormat { NumberFormatId = 164U, FontId = 0U, ApplyNumberFormat = true },              // 2: data
                    new CellFormat { FontId = 0U, Protection = new Protection { Locked = true } })                // 3: bloccata
                { Count = 4U },
                // I formati differenziali a cui punta il FormatId delle regole di formattazione
                // condizionale. Senza questa sezione il pacchetto è internamente incoerente: la
                // regola rimanderebbe a un dxf inesistente.
                new DifferentialFormats(
                    new DifferentialFormat(
                        new Fill(new PatternFill(new BackgroundColor { Rgb = "FFFFC7CE" }) { PatternType = PatternValues.Solid })))
                { Count = 1U });
        }

        private static SheetData BuildSheetData()
        {
            var sheetData = new SheetData();

            // Riga 1: intestazioni dell'area dati (A-E) e della tabella di supporto (G-H).
            sheetData.AppendChild(BuildRow(1,
                (1, "A1", "Data", 1U),
                (2, "B1", "Sito", 1U),
                (3, "C1", "Tipologia", 1U),
                (4, "D1", "Valore", 1U),
                (5, "E1", "Totale", 1U),
                (7, "G1", "Codice", 1U),
                (8, "H1", "Descrizione", 1U)));

            for (uint r = 2; r <= 4; r++)
            {
                var row = new Row { RowIndex = r };

                row.AppendChild(NumericCell($"A{r}", 45000 + r, styleIndex: 2U));   // data (stile con formato)
                row.AppendChild(SharedStringCell($"B{r}", 0));                       // "Milano" da sharedStrings
                row.AppendChild(SharedStringCell($"C{r}", 1));                       // "Correttiva" da sharedStrings
                row.AppendChild(NumericCell($"D{r}", 10 * r, styleIndex: 0U));

                // Formula preesistente: deve restare intatta nelle righe non toccate.
                row.AppendChild(new Cell
                {
                    CellReference = $"E{r}",
                    CellFormula = new CellFormula($"D{r}*2"),
                    CellValue = new CellValue((20 * r).ToString()),
                    StyleIndex = 3U
                });

                sheetData.AppendChild(row);
            }

            return sheetData;
        }

        private static Row BuildRow(uint rowIndex, params (int Col, string Reference, string Text, uint Style)[] cells)
        {
            var row = new Row { RowIndex = rowIndex };
            foreach (var (_, reference, text, style) in cells)
            {
                row.AppendChild(new Cell
                {
                    CellReference = reference,
                    DataType = CellValues.InlineString,
                    StyleIndex = style,
                    InlineString = new InlineString(new Text(text))
                });
            }
            return row;
        }

        private static Cell NumericCell(string reference, double value, uint styleIndex) => new()
        {
            CellReference = reference,
            StyleIndex = styleIndex,
            CellValue = new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };

        private static Cell SharedStringCell(string reference, int sharedStringIndex) => new()
        {
            CellReference = reference,
            DataType = CellValues.SharedString,
            CellValue = new CellValue(sharedStringIndex.ToString())
        };
    }
}
