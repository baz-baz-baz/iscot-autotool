using System;
using System.Globalization;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Costruisce un report che riproduce la **patologia dimensionale** del Report Interventi reale:
    /// poche migliaia di righe con dati seguite da centinaia di migliaia di elementi
    /// <c>&lt;row/&gt;</c> vuoti, e <c>sheetFormatPr zeroHeight="1"</c>.
    ///
    /// <para>
    /// Sul file aziendale reale (ETR1000, misurato nello Sprint 22) il rapporto è: 20.370 righe con
    /// celle e <b>1.028.205 righe vuote</b>, per una parte <c>sheet1.xml</c> da 59 MB di cui 40 di
    /// soli segnaposto. È quel rapporto — non la dimensione assoluta — che questa fixture riproduce.
    /// </para>
    ///
    /// <para>
    /// <b>Perché la parte viene scritta come XML grezzo</b> invece che con <c>OpenXmlWriter</c>: una
    /// riga vuota emessa dall'SDK diventa <c>&lt;row&gt;&lt;/row&gt;</c>, mentre Excel scrive
    /// <c>&lt;row/&gt;</c>. Sono equivalenti per lo schema, ma la fixture deve riprodurre la forma
    /// reale, che è quella che <c>CompactEmptyRows</c> deve saper riconoscere.
    /// </para>
    /// </summary>
    internal static class BloatedReportBuilder
    {
        internal const string SheetName = "Interventi";

        /// <summary>Colonne per riga, come nel report reale (A..AA).</summary>
        private const int Columns = 27;

        /// <summary>
        /// Righe **preformattate**: hanno tutte e 27 le celle, con stile, ma nessun valore. Nel report
        /// reale sono le righe fra l'ultimo intervento (17442) e la fine dell'area formattata (20369).
        /// Non sono segnaposto vuoti e non vanno rimosse dalla compattazione.
        /// </summary>
        internal const int PreformattedRows = 5;

        /// <summary>
        /// Crea il file di prova.
        /// </summary>
        /// <param name="filePath">Percorso del file da creare.</param>
        /// <param name="dataRows">Righe con celle, intestazione esclusa: la riga 1 è l'intestazione.</param>
        /// <param name="emptyRows">Righe segnaposto vuote aggiunte in coda.</param>
        internal static void Create(string filePath, int dataRows, int emptyRows)
        {
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();

            workbookPart.Workbook.AppendChild(new Sheets(new Sheet
            {
                Id = workbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1U,
                Name = SheetName
            }));
            workbookPart.Workbook.Save();

            using var stream = worksheetPart.GetStream(FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            WriteSheetXml(writer, dataRows, emptyRows);
        }

        /// <summary>Indice della riga vuota che dichiara un'altezza propria: deve sopravvivere alla compattazione.</summary>
        internal static int InformativeEmptyRowIndex(int dataRows, int emptyRows) =>
            1 + dataRows + PreformattedRows + emptyRows + 1;

        private static void WriteSheetXml(TextWriter writer, int dataRows, int emptyRows)
        {
            int lastRow = InformativeEmptyRowIndex(dataRows, emptyRows);

            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" ");
            writer.Write("xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");

            writer.Write($"<dimension ref=\"A1:AA{lastRow}\"/>");
            writer.Write("<sheetViews><sheetView tabSelected=\"1\" workbookViewId=\"0\"/></sheetViews>");

            // zeroHeight="1": senza un <row> esplicito le righe sarebbero nascoste. È l'attributo che
            // rende la compattazione non banale — vedi ReportInterventiWriter.CompactEmptyRows.
            writer.Write("<sheetFormatPr defaultColWidth=\"8.5546875\" defaultRowHeight=\"13.2\" zeroHeight=\"1\"/>");
            writer.Write("<cols><col min=\"1\" max=\"1\" width=\"8.33203125\" style=\"1\" customWidth=\"1\"/></cols>");

            writer.Write("<sheetData>");

            // Riga 1: intestazioni.
            writer.Write("<row r=\"1\" spans=\"1:27\">");
            for (int c = 1; c <= Columns; c++)
            {
                writer.Write($"<c r=\"{ColumnName(c)}1\" t=\"inlineStr\"><is><t>Col{c}</t></is></c>");
            }
            writer.Write("</row>");

            // Righe con dati: valori nelle colonne chiave B, C, D, G; le altre celle esistono ma sono
            // vuote, esattamente come nel report reale (pre-formattate).
            for (int r = 2; r <= dataRows + 1; r++)
            {
                writer.Write($"<row r=\"{r}\" spans=\"1:27\">");
                for (int c = 1; c <= Columns; c++)
                {
                    string reference = ColumnName(c) + r.ToString(CultureInfo.InvariantCulture);
                    switch (c)
                    {
                        case 1: // numerazione progressiva con formula, come la colonna A reale
                            writer.Write($"<c r=\"{reference}\" s=\"1\"><f>SUM(A{r - 1},1)</f><v>{r - 1}</v></c>");
                            break;
                        case 2:
                            writer.Write($"<c r=\"{reference}\" s=\"2\"><v>{45000 + r}</v></c>");
                            break;
                        case 3:
                            writer.Write($"<c r=\"{reference}\" t=\"inlineStr\"><is><t>Milano</t></is></c>");
                            break;
                        case 4:
                            writer.Write($"<c r=\"{reference}\"><v>{1200000 + r}</v></c>");
                            break;
                        case 7:
                            writer.Write($"<c r=\"{reference}\"><v>{800 + (r % 50)}</v></c>");
                            break;
                        default:
                            writer.Write($"<c r=\"{reference}\" s=\"3\"/>");
                            break;
                    }
                }
                writer.Write("</row>");
            }

            // Righe preformattate: 27 celle con stile ma senza valori, come fra l'ultimo intervento
            // e la fine dell'area formattata del report reale.
            for (int r = dataRows + 2; r <= dataRows + 1 + PreformattedRows; r++)
            {
                writer.Write($"<row r=\"{r}\" spans=\"1:27\">");
                for (int c = 1; c <= Columns; c++)
                {
                    writer.Write($"<c r=\"{ColumnName(c)}{r}\" s=\"3\"/>");
                }
                writer.Write("</row>");
            }

            // Righe segnaposto: vuote e self-closing, la forma che scrive Excel.
            for (int r = dataRows + 2 + PreformattedRows; r < lastRow; r++)
            {
                writer.Write($"<row r=\"{r}\" spans=\"1:27\"/>");
            }

            // Riga vuota ma con un'altezza propria: porta informazione, non è un segnaposto.
            writer.Write($"<row r=\"{lastRow}\" spans=\"1:27\" ht=\"30\" customHeight=\"1\"/>");

            writer.Write("</sheetData>");

            // Elementi strutturali che la scrittura non deve toccare.
            writer.Write($"<autoFilter ref=\"A1:AA1\"/>");
            writer.Write("<mergeCells count=\"1\"><mergeCell ref=\"Y1:Z1\"/></mergeCells>");
            writer.Write("<dataValidations count=\"1\">");
            writer.Write("<dataValidation type=\"list\" allowBlank=\"1\" showInputMessage=\"1\" showErrorMessage=\"1\" sqref=\"C2:C1000\">");
            writer.Write("<formula1>\"Milano,Roma,Napoli\"</formula1></dataValidation></dataValidations>");
            writer.Write("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");

            writer.Write("</worksheet>");
        }

        private static string ColumnName(int columnNumber)
        {
            string name = string.Empty;
            while (columnNumber > 0)
            {
                int remainder = (columnNumber - 1) % 26;
                name = (char)('A' + remainder) + name;
                columnNumber = (columnNumber - 1) / 26;
            }
            return name;
        }
    }
}
