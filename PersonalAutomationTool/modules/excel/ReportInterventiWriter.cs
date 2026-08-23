using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace PersonalAutomationTool.Modules.Excel
{
    /// <summary>
    /// Scrittura **chirurgica** di una riga nel "Report Interventi": modifica il solo
    /// <c>&lt;sheetData&gt;</c> del foglio interessato e lascia intatto tutto il resto del pacchetto
    /// OpenXML — parti binarie (<c>vbaProject.bin</c>), <c>styles.xml</c>, convalide dati,
    /// formattazione condizionale, <c>autoFilter</c>, <c>tableParts</c>, protezioni, relazioni
    /// (<c>.rels</c>) e metadati.
    ///
    /// <para>
    /// <b>Rapporto con Excel Interop.</b> Il percorso di scrittura predefinito dell'applicazione
    /// resta <c>ExcelViewModel.ExecuteScriviReport</c> via Interop, ed è corretto che lo resti: è
    /// Excel stesso a riscrivere il file, quindi la conservazione è totale per costruzione
    /// (invariante §5.4 di PROJECT_MEMORY.md). Questa classe **non lo sostituisce**. Esiste per due
    /// motivi concreti:
    /// <list type="number">
    /// <item>rende la scrittura verificabile in modo automatico, senza Excel installato: la suite di
    /// integrità strutturale (<c>ReportInterventiWriterTests</c>) apre il pacchetto prodotto e ne
    /// confronta le parti con l'originale, cosa impossibile da fare contro un processo COM in un
    /// test headless;</item>
    /// <item>offre un percorso di scrittura utilizzabile dove Excel non è installato, dove oggi
    /// "Scrivi report" si limita a mostrare un errore.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>Scelte fatte per non alterare nulla di implicito.</b>
    /// <list type="bullet">
    /// <item>Le stringhe sono scritte come <see cref="CellValues.InlineString"/> e non nella tabella
    /// delle stringhe condivise: <c>sharedStrings.xml</c> è una parte condivisa da tutti i fogli, e
    /// modificarla per aggiungere una voce cambierebbe un file che non ha alcun bisogno di cambiare.</item>
    /// <item>Le celle nuove ereditano lo <c>StyleIndex</c> dalla cella della stessa colonna nella
    /// riga precedente: è ciò che rende la riga appena inserita visivamente identica alle altre
    /// (bordi, sfondo, formato data) senza toccare <c>styles.xml</c>, dove nessun nuovo stile viene
    /// creato.</item>
    /// <item>Le date sono scritte come numero seriale OADate, la rappresentazione nativa di Excel; il
    /// formato di visualizzazione arriva dallo stile ereditato, esattamente come per le righe già
    /// presenti.</item>
    /// <item>Righe e celle sono inserite mantenendo l'ordine crescente richiesto dallo schema
    /// OpenXML: un ordinamento errato produce un file che Excel considera danneggiato.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ReportInterventiWriter
    {
        /// <summary>
        /// Scrive i valori indicati nella riga <paramref name="rowNumber"/> del primo foglio.
        /// </summary>
        /// <param name="filePath">Percorso del workbook (.xlsx o .xlsm) da modificare sul posto.</param>
        /// <param name="rowNumber">Riga di destinazione, 1-based.</param>
        /// <param name="valuesByColumn">
        /// Valori da scrivere, indicizzati per numero di colonna 1-based. Una colonna assente dal
        /// dizionario <b>non viene toccata</b>: è il comportamento del percorso Interop, che salta le
        /// celle senza valore per non sovrascrivere formule o formattazione preesistenti.
        /// </param>
        public static void WriteRow(string filePath, int rowNumber, IReadOnlyDictionary<int, string?> valuesByColumn)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            if (rowNumber < 1) throw new ArgumentOutOfRangeException(nameof(rowNumber));

            using var document = SpreadsheetDocument.Open(filePath, isEditable: true);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Workbook privo di WorkbookPart: pacchetto non valido.");

            var worksheetPart = GetFirstWorksheetPart(workbookPart)
                ?? throw new InvalidOperationException("Nessun foglio di lavoro trovato nel workbook.");

            var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>()
                ?? throw new InvalidOperationException("Foglio privo di sheetData: pacchetto non valido.");

            var row = GetOrCreateRow(sheetData, rowNumber);
            var previousRow = sheetData.Elements<Row>().LastOrDefault(r => r.RowIndex != null && r.RowIndex.Value < (uint)rowNumber);

            foreach (var (columnNumber, rawValue) in valuesByColumn.OrderBy(kv => kv.Key))
            {
                // Colonna senza valore: lasciata esattamente com'era, come fa il percorso Interop.
                if (string.IsNullOrWhiteSpace(rawValue)) continue;

                var cell = GetOrCreateCell(row, columnNumber, previousRow);
                SetCellValue(cell, rawValue);
            }

            worksheetPart.Worksheet.Save();
        }

        /// <summary>
        /// Il foglio corrispondente alla prima scheda del workbook.
        ///
        /// <para>
        /// <b>Legge <c>workbook.xml</c> come XML grezzo invece di usare il DOM dell'SDK</b>
        /// (<c>workbookPart.Workbook</c>): il semplice accesso a quella proprietà carica il DOM in
        /// memoria e l'SDK lo **riscrive alla chiusura del documento**, modificando i byte di una
        /// parte che non abbiamo alcun motivo di toccare. Rilevato da
        /// <c>ScritturaRiga_LUnicaParteModificataEIlFoglio</c>, che elencava <c>xl/workbook.xml</c>
        /// fra le parti alterate: un effetto collaterale invisibile a un'ispezione del codice.
        /// </para>
        /// </summary>
        private static WorksheetPart? GetFirstWorksheetPart(WorkbookPart workbookPart)
        {
            try
            {
                using var stream = workbookPart.GetStream(System.IO.FileMode.Open, System.IO.FileAccess.Read);
                var document = System.Xml.Linq.XDocument.Load(stream);

                System.Xml.Linq.XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                System.Xml.Linq.XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

                string? relationshipId = document.Root?
                    .Element(main + "sheets")?
                    .Elements(main + "sheet")
                    .FirstOrDefault()?
                    .Attribute(rel + "id")?.Value;

                if (!string.IsNullOrEmpty(relationshipId) &&
                    workbookPart.GetPartById(relationshipId) is WorksheetPart part)
                {
                    return part;
                }
            }
            catch
            {
                // Workbook non interpretabile come XML: si ripiega sull'ordine delle parti.
            }

            return workbookPart.WorksheetParts.FirstOrDefault();
        }

        /// <summary>
        /// La riga richiesta, creandola nella posizione corretta se non esiste. Lo schema OpenXML
        /// richiede che gli elementi <c>&lt;row&gt;</c> siano in ordine di <c>r</c> crescente.
        /// </summary>
        private static Row GetOrCreateRow(SheetData sheetData, int rowNumber)
        {
            var existing = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == (uint)rowNumber);
            if (existing != null) return existing;

            var newRow = new Row { RowIndex = (uint)rowNumber };
            var following = sheetData.Elements<Row>().FirstOrDefault(r => r.RowIndex != null && r.RowIndex.Value > (uint)rowNumber);

            if (following != null) sheetData.InsertBefore(newRow, following);
            else sheetData.AppendChild(newRow);

            return newRow;
        }

        /// <summary>
        /// La cella richiesta nella riga, creandola in ordine di colonna se non esiste. Una cella
        /// nuova eredita lo <c>StyleIndex</c> dalla stessa colonna della riga precedente, così la
        /// riga inserita mantiene l'aspetto delle altre senza aggiungere stili al pacchetto.
        /// </summary>
        private static Cell GetOrCreateCell(Row row, int columnNumber, Row? previousRow)
        {
            string columnName = GetColumnName(columnNumber);
            string cellReference = columnName + row.RowIndex!.Value.ToString(CultureInfo.InvariantCulture);

            var existing = row.Elements<Cell>().FirstOrDefault(c =>
                string.Equals(c.CellReference?.Value, cellReference, StringComparison.OrdinalIgnoreCase));
            if (existing != null) return existing;

            var newCell = new Cell { CellReference = cellReference };

            var styleSource = previousRow?.Elements<Cell>()
                .FirstOrDefault(c => GetColumnNumber(c.CellReference?.Value) == columnNumber);
            if (styleSource?.StyleIndex != null)
            {
                newCell.StyleIndex = styleSource.StyleIndex.Value;
            }

            var following = row.Elements<Cell>()
                .FirstOrDefault(c => GetColumnNumber(c.CellReference?.Value) > columnNumber);

            if (following != null) row.InsertBefore(newCell, following);
            else row.AppendChild(newCell);

            return newCell;
        }

        private static void SetCellValue(Cell cell, string rawValue)
        {
            // Una cella riscritta non deve conservare una formula del valore precedente.
            cell.CellFormula = null;
            cell.RemoveAllChildren<InlineString>();

            if (DateTime.TryParseExact(rawValue, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                // Numero seriale OADate: la rappresentazione nativa delle date in Excel. Il formato
                // di visualizzazione viene dallo stile ereditato, come per le righe già presenti.
                cell.DataType = null;
                cell.CellValue = new CellValue(parsedDate.ToOADate().ToString(CultureInfo.InvariantCulture));
                return;
            }

            if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double numericValue)
                && rawValue.Trim() == numericValue.ToString(CultureInfo.InvariantCulture))
            {
                // Solo se la stringa è esattamente la forma canonica del numero: così un valore come
                // "007" o "1.2.3" resta testo e non viene alterato nella conversione.
                cell.DataType = null;
                cell.CellValue = new CellValue(numericValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            // Stringa inline: non tocca sharedStrings.xml, che resta byte-identico.
            cell.CellValue = null;
            cell.DataType = CellValues.InlineString;
            cell.AppendChild(new InlineString(new Text(rawValue)));
        }

        /// <summary>Nome di colonna (1 → "A", 27 → "AA") da un numero 1-based.</summary>
        internal static string GetColumnName(int columnNumber)
        {
            if (columnNumber < 1) throw new ArgumentOutOfRangeException(nameof(columnNumber));

            string name = string.Empty;
            while (columnNumber > 0)
            {
                int remainder = (columnNumber - 1) % 26;
                name = (char)('A' + remainder) + name;
                columnNumber = (columnNumber - 1) / 26;
            }
            return name;
        }

        /// <summary>Numero di colonna 1-based da un riferimento di cella tipo <c>"BC12"</c>; 0 se non interpretabile.</summary>
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
