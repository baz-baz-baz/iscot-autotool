using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PersonalAutomationTool.Modules.Verifiche
{
    /// <summary>
    /// Lettura **in streaming (SAX)** del foglio "Verifiche", in sostituzione del caricamento
    /// dell'intero DOM di ClosedXML (intervento 3.4 della roadmap, rimandato per due sprint per
    /// mancanza di un file reale su cui validare — vedi PROJECT_MEMORY.md §6.1-sexies per come è
    /// stato sbloccato senza).
    ///
    /// <para>
    /// <b>Perché.</b> <c>XLWorkbook</c> materializza in memoria l'intero workbook — tutte le celle
    /// di tutti i fogli, con stili, formule e formati — per estrarne poi tre sole colonne. Su un
    /// file "Verifiche" di qualche migliaio di righe questo significa decine di MB in Large Object
    /// Heap e una pausa del garbage collector percepibile, ripetuti a **ogni** ricarica: e le
    /// ricariche non sono rare, perché le scatenano sia i <c>FileSystemWatcher</c> sia il timer di
    /// backstop (§2.5). <see cref="OpenXmlReader"/> attraversa invece il file una volta sola, un
    /// elemento per volta, senza costruire alcun albero: la memoria occupata non dipende più dalla
    /// dimensione del foglio.
    /// </para>
    ///
    /// <para>
    /// <b>Vincolo di correttezza.</b> Questa classe deve produrre <b>esattamente</b> le stesse righe
    /// del percorso ClosedXML che sostituisce, comprese le sue convenzioni non ovvie:
    /// <list type="bullet">
    /// <item><c>RowsUsed()</c> restituisce solo le righe che hanno almeno una cella con contenuto —
    /// le righe interamente vuote non compaiono e <b>non</b> spostano l'indice dell'intestazione;</item>
    /// <item><c>CellsUsed()</c>, usata per localizzare l'intestazione, considera solo le celle con
    /// contenuto;</item>
    /// <item>le colonne cercate sono individuate per <b>numero di colonna assoluto</b>
    /// (<c>cell.Address.ColumnNumber</c>), non per posizione fra le celle non vuote.</item>
    /// </list>
    /// L'equivalenza non è affidata alla lettura del codice: è verificata da test che eseguono
    /// entrambe le implementazioni sullo stesso file e ne confrontano l'output riga per riga
    /// (<c>VerificheExcelReaderTests</c>).
    /// </para>
    /// </summary>
    internal static class VerificheExcelReader
    {
        /// <summary>
        /// Una riga grezza del foglio: i valori delle tre colonne di interesse, già ripuliti con
        /// <c>Trim</c>, più il <b>numero di riga Excel</b> da cui provengono.
        ///
        /// <para>
        /// <b><see cref="RowNumber"/> serve all'archiviazione, non alla lettura.</b> Treno, Loco e
        /// Avaria non identificano una riga in modo univoco — nei file reali capitano righe con lo
        /// stesso treno e la stessa loco (es. ETR1000 31/831, due richieste distinte sulla stessa
        /// macchina). Senza il numero di riga, "Verifica Eseguita" potrebbe archiviare e cancellare
        /// la riga sbagliata. Vale 0 quando la riga non proviene da un foglio (percorso di ripiego
        /// ClosedXML su file di formato imprevisto).
        /// </para>
        /// </summary>
        internal sealed record VerificaRow(string Treno, string Loco, string Avaria, int RowNumber = 0);

        /// <summary>
        /// Legge il primo foglio di <paramref name="filePath"/> restituendo le righe dati.
        /// Lancia se il file non è un pacchetto OpenXML valido: il chiamante
        /// (<c>VerificheViewModel.ParseExcelFile</c>) intercetta e ricade sul percorso ClosedXML,
        /// così un formato imprevisto degrada le prestazioni ma non perde dati.
        /// </summary>
        internal static List<VerificaRow> Read(string filePath)
        {
            // FileShare.Delete oltre a ReadWrite: i file Verifiche stanno in cartelle sincronizzate,
            // dove il client OneDrive può doverli sostituire o rinominare mentre li stiamo leggendo.
            // Senza questo flag la nostra lettura farebbe fallire quelle operazioni.
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = SpreadsheetDocument.Open(stream, isEditable: false);

            var workbookPart = document.WorkbookPart;
            if (workbookPart == null) return [];

            var worksheetPart = GetFirstWorksheetPart(workbookPart);
            if (worksheetPart == null) return [];

            // La tabella delle stringhe condivise è indicizzata dalle celle di tipo "s". Viene letta
            // una volta sola, anch'essa in streaming: è l'unica struttura che resta in memoria, e
            // solo perché gli indici possono essere referenziati in qualsiasi ordine.
            var sharedStrings = ReadSharedStrings(workbookPart);

            var usedRows = new List<RigaGrezza>();
            using (var reader = OpenXmlReader.Create(worksheetPart))
            {
                while (reader.Read())
                {
                    if (reader.ElementType != typeof(Row)) continue;

                    var row = (Row)reader.LoadCurrentElement()!;
                    int rowNumber = row.RowIndex?.Value is uint ri ? (int)ri : 0;
                    var cells = new Dictionary<int, string>();

                    foreach (var cell in row.Elements<Cell>())
                    {
                        string value = GetCellValue(cell, sharedStrings);
                        // "Cella usata" secondo ClosedXML: una cella che esiste nell'XML ma è vuota
                        // (tipico delle celle toccate solo da una formattazione) non conta.
                        if (value.Length == 0) continue;

                        int columnNumber = GetColumnNumber(cell.CellReference?.Value);
                        if (columnNumber > 0) cells[columnNumber] = value;
                    }

                    // Riga interamente vuota: RowsUsed() la salterebbe, quindi la saltiamo anche noi.
                    if (cells.Count > 0) usedRows.Add(new RigaGrezza(rowNumber, cells));
                }
            }

            return ExtractRows(usedRows);
        }

        /// <summary>Una riga non vuota del foglio, con il suo numero di riga Excel.</summary>
        internal sealed record RigaGrezza(int RowNumber, Dictionary<int, string> Cells);

        /// <summary>
        /// Individua l'intestazione e proietta le righe dati. Separata da <see cref="Read"/> e
        /// <c>internal</c> per poter essere verificata anche senza un file su disco.
        /// </summary>
        internal static List<VerificaRow> ExtractRows(List<RigaGrezza> usedRows)
        {
            var result = new List<VerificaRow>();
            if (usedRows.Count < 2) return result;

            // Prima riga che contiene una cella con "TRENO": stessa ricerca euristica dell'originale.
            int headerIndex = -1;
            for (int i = 0; i < usedRows.Count; i++)
            {
                if (usedRows[i].Cells.Values.Any(v => v.Trim().Contains("TRENO", StringComparison.OrdinalIgnoreCase)))
                {
                    headerIndex = i;
                    break;
                }
            }

            // Nessuna intestazione riconosciuta: l'originale ripiega sulla prima riga usata.
            if (headerIndex == -1) headerIndex = 0;

            int trenoIdx = -1, locoIdx = -1, avariaIdx = -1;
            foreach (var (columnNumber, rawValue) in usedRows[headerIndex].Cells)
            {
                string headerText = rawValue.Trim();
                if (headerText.Contains("TRENO", StringComparison.OrdinalIgnoreCase)) trenoIdx = columnNumber;
                else if (headerText.Contains("LOCO", StringComparison.OrdinalIgnoreCase)) locoIdx = columnNumber;
                else if (headerText.Contains("AVARIA", StringComparison.OrdinalIgnoreCase) || headerText.Contains("ING/SVI", StringComparison.OrdinalIgnoreCase)) avariaIdx = columnNumber;
            }

            if (trenoIdx == -1) trenoIdx = 1;
            if (locoIdx == -1) locoIdx = 2;
            if (avariaIdx == -1) avariaIdx = 3;

            for (int i = headerIndex + 1; i < usedRows.Count; i++)
            {
                var row = usedRows[i].Cells;
                string treno = GetTrimmed(row, trenoIdx);
                string loco = GetTrimmed(row, locoIdx);
                string avaria = GetTrimmed(row, avariaIdx);

                if (!string.IsNullOrWhiteSpace(treno) || !string.IsNullOrWhiteSpace(loco) || !string.IsNullOrWhiteSpace(avaria))
                {
                    result.Add(new VerificaRow(treno, loco, avaria, usedRows[i].RowNumber));
                }
            }

            return result;
        }

        private static string GetTrimmed(Dictionary<int, string> row, int columnNumber) =>
            row.TryGetValue(columnNumber, out var value) ? value.Trim() : string.Empty;

        /// <summary>
        /// Il foglio corrispondente alla prima scheda del workbook. <c>workbookPart.Worksheets</c>
        /// non è ordinato come le schede visibili: l'ordine autorevole è quello degli elementi
        /// <see cref="Sheet"/>, e <c>ClosedXML.Worksheet(1)</c> segue quello.
        /// </summary>
        private static WorksheetPart? GetFirstWorksheetPart(WorkbookPart workbookPart)
        {
            var firstSheet = workbookPart.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault();
            if (firstSheet?.Id?.Value is string relationshipId)
            {
                return workbookPart.GetPartById(relationshipId) as WorksheetPart;
            }
            return workbookPart.WorksheetParts.FirstOrDefault();
        }

        private static List<string> ReadSharedStrings(WorkbookPart workbookPart)
        {
            var result = new List<string>();
            var part = workbookPart.SharedStringTablePart;
            if (part == null) return result;

            using var reader = OpenXmlReader.Create(part);
            while (reader.Read())
            {
                if (reader.ElementType == typeof(SharedStringItem))
                {
                    var item = (SharedStringItem)reader.LoadCurrentElement()!;
                    result.Add(item.InnerText);
                }
            }
            return result;
        }

        private static string GetCellValue(Cell cell, List<string> sharedStrings)
        {
            // Stringa inline: il testo sta dentro la cella, non nella tabella condivisa.
            if (cell.DataType?.Value == CellValues.InlineString)
            {
                return cell.InlineString?.InnerText ?? string.Empty;
            }

            string? raw = cell.CellValue?.InnerText;
            if (raw == null) return string.Empty;

            if (cell.DataType?.Value == CellValues.SharedString)
            {
                return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                       && index >= 0 && index < sharedStrings.Count
                    ? sharedStrings[index]
                    : string.Empty;
            }

            if (cell.DataType?.Value == CellValues.Boolean)
            {
                // ClosedXML.GetString() rende i booleani come "TRUE"/"FALSE".
                return raw == "1" ? "TRUE" : "FALSE";
            }

            // Numeri (e tutto il resto) come compaiono nel file. ClosedXML.GetString() su una cella
            // numerica restituisce il valore non formattato, quindi la rappresentazione coincide.
            return raw;
        }

        /// <summary>
        /// Numero di colonna (1-based) da un riferimento di cella tipo <c>"BC12"</c>. Restituisce 0
        /// se il riferimento manca o non è interpretabile, così il chiamante scarta la cella invece
        /// di attribuirla alla colonna sbagliata.
        /// </summary>
        internal static int GetColumnNumber(string? cellReference)
        {
            if (string.IsNullOrEmpty(cellReference)) return 0;

            int result = 0;
            foreach (char c in cellReference)
            {
                if (c is >= 'A' and <= 'Z') result = result * 26 + (c - 'A' + 1);
                else if (c is >= 'a' and <= 'z') result = result * 26 + (c - 'a' + 1);
                else break;
            }
            return result;
        }
    }
}
