using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PersonalAutomationTool.Tests.Modules.Excel
{
    /// <summary>
    /// Costruisce un report con **righe fantasma**: righe già formattate, in coda all'area dati, che
    /// contengono celle ma nessun valore. Sono la trappola del bug ETR1000 I-F
    /// (PROJECT_MEMORY.md §6.1-tricies-septies): a un controllo strutturale — contare i nodi
    /// <c>&lt;row&gt;</c>, o fermarsi a "la cella non è self-closing" — sembrano righe compilate, e la
    /// riga di inserimento scivola in fondo alla zona preformattata invece di atterrare sulla prima
    /// riga davvero libera.
    ///
    /// <para>
    /// Le cinque righe fantasma usano <b>cinque forme diverse</b> di cella vuota, perché è la varietà a
    /// rendere il caso reale: Excel scrive normalmente la prima, ma le altre arrivano da salvataggi di
    /// altri produttori, da copia-incolla e da celle svuotate a mano.
    /// <list type="number">
    /// <item>self-closing: <c>&lt;c r="B11" s="3"/&gt;</c></item>
    /// <item>aperta e chiusa ma vuota: <c>&lt;c r="B12" s="3"&gt;&lt;/c&gt;</c></item>
    /// <item>valore vuoto: <c>&lt;c r="B13" s="3"&gt;&lt;v&gt;&lt;/v&gt;&lt;/c&gt;</c></item>
    /// <item>stringa inline di soli spazi</item>
    /// <item>stringa condivisa che punta a una voce vuota</item>
    /// </list>
    /// La riga 1 è l'intestazione e le righe 2-10 sono dati: dieci righe compilate in tutto, quindi la
    /// prima riga libera è la <b>11</b>.
    /// </para>
    /// </summary>
    internal static class GhostRowsReportBuilder
    {
        internal const string SheetName = "Interventi 500";

        /// <summary>Ultima riga con dati veri: l'inserimento deve avvenire subito dopo, alla riga 11.</summary>
        internal const int LastFilledRow = 10;

        /// <summary>Prima riga fantasma (formattata ma vuota).</summary>
        internal const int FirstGhostRow = 11;

        /// <summary>Ultima riga fantasma.</summary>
        internal const int LastGhostRow = 15;

        internal static void Create(string filePath)
        {
            using var document = SpreadsheetDocument.Create(filePath, SpreadsheetDocumentType.Workbook);

            var workbookPart = document.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            // Indice 0 deliberatamente VUOTO: una cella t="s" che vi punta ha un <v> valorizzato
            // ("0") pur non contenendo alcun testo. È l'errore più facile da commettere in una
            // scansione che non risolve le stringhe condivise.
            var sharedStringPart = workbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringPart.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text(string.Empty)),
                new SharedStringItem(new Text("Milano Martesana")));
            sharedStringPart.SharedStringTable.Save();

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
            WriteSheetXml(writer);
        }

        private static void WriteSheetXml(TextWriter writer)
        {
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            writer.Write("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            writer.Write($"<dimension ref=\"A1:X{LastGhostRow}\"/>");
            writer.Write("<sheetData>");

            // Riga 1: intestazioni, nelle stesse colonne chiave del report reale.
            writer.Write("<row r=\"1\">");
            writer.Write("<c r=\"B1\" t=\"inlineStr\"><is><t>DATA CHIAMATA</t></is></c>");
            writer.Write("<c r=\"C1\" t=\"inlineStr\"><is><t>SITO INTERVENTO</t></is></c>");
            writer.Write("<c r=\"D1\" t=\"inlineStr\"><is><t>TICKET</t></is></c>");
            writer.Write("<c r=\"G1\" t=\"inlineStr\"><is><t>DATA INTERVENTO</t></is></c>");
            writer.Write("</row>");

            // Righe 2-10: dati veri, con le tre forme in cui un valore può presentarsi.
            for (int r = 2; r <= LastFilledRow; r++)
            {
                writer.Write($"<row r=\"{r}\">");
                writer.Write($"<c r=\"B{r}\" s=\"2\"><v>{46000 + r}</v></c>");        // data (seriale)
                writer.Write($"<c r=\"C{r}\" t=\"s\"><v>1</v></c>");                   // stringa condivisa NON vuota
                writer.Write($"<c r=\"D{r}\" t=\"inlineStr\"><is><t>125{r:D4}</t></is></c>"); // ticket inline
                writer.Write($"<c r=\"G{r}\" s=\"2\"><v>{46000 + r}</v></c>");
                writer.Write("</row>");
            }

            // Righe 11-15: formattate ma VUOTE, una forma diversa per riga.
            WriteGhostRow(writer, 11, static (w, r, col) => w.Write($"<c r=\"{col}{r}\" s=\"3\"/>"));
            WriteGhostRow(writer, 12, static (w, r, col) => w.Write($"<c r=\"{col}{r}\" s=\"3\"></c>"));
            WriteGhostRow(writer, 13, static (w, r, col) => w.Write($"<c r=\"{col}{r}\" s=\"3\"><v></v></c>"));
            WriteGhostRow(writer, 14, static (w, r, col) =>
                w.Write($"<c r=\"{col}{r}\" s=\"3\" t=\"inlineStr\"><is><t xml:space=\"preserve\">   </t></is></c>"));
            WriteGhostRow(writer, 15, static (w, r, col) => w.Write($"<c r=\"{col}{r}\" s=\"3\" t=\"s\"><v>0</v></c>"));

            writer.Write("</sheetData>");
            writer.Write("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>");
            writer.Write("</worksheet>");
        }

        private static void WriteGhostRow(TextWriter writer, int rowIndex, System.Action<TextWriter, int, string> cell)
        {
            writer.Write($"<row r=\"{rowIndex}\">");
            foreach (string column in new[] { "B", "C", "D", "G" }) cell(writer, rowIndex, column);
            writer.Write("</row>");
        }
    }
}
