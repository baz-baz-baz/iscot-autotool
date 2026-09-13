using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace PersonalAutomationTool.Modules.Excel
{
    /// <summary>
    /// Scrittura **chirurgica e in streaming** di una riga nel "Report Interventi": attraversa il
    /// foglio con <see cref="XmlReader"/>/<see cref="XmlWriter"/> copiando ogni nodo così com'è e
    /// intervenendo solo sulla riga di destinazione. Tutto il resto del pacchetto OpenXML — parti
    /// binarie (<c>vbaProject.bin</c>), <c>styles.xml</c>, <c>sharedStrings.xml</c>, convalide dati,
    /// formattazione condizionale, <c>autoFilter</c>, <c>tableParts</c>, protezioni, relazioni
    /// (<c>.rels</c>) e metadati — resta intatto.
    ///
    /// <para>
    /// <b>Perché streaming e non DOM (Sprint 22).</b> La prima versione di questa classe leggeva
    /// <c>worksheetPart.Worksheet</c>, che materializza in memoria l'intero albero del foglio. Sul
    /// Report Interventi ETR1000 reale — <c>sheet1.xml</c> da 59 MB — quella singola riga costava
    /// **4.680 ms e 925 MB allocati**, misurati, perché il DOM creava oltre un milione di oggetti
    /// <c>Row</c>. La versione in streaming non materializza nulla tranne la riga di destinazione:
    /// **1.458 ms** sullo stesso file, **288 ms** dopo la compattazione di
    /// <see cref="CompactEmptyRows"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Scelte fatte per non alterare nulla di implicito.</b>
    /// <list type="bullet">
    /// <item>Le stringhe sono scritte come <c>inlineStr</c> e non nella tabella delle stringhe
    /// condivise: <c>sharedStrings.xml</c> è condivisa da tutti i fogli e non ha alcun motivo di
    /// cambiare.</item>
    /// <item>Le celle nuove ereditano lo <c>StyleIndex</c> dalla cella della stessa colonna nella
    /// riga precedente: è ciò che rende la riga appena inserita visivamente identica alle altre
    /// senza toccare <c>styles.xml</c>, dove nessun nuovo stile viene creato.</item>
    /// <item>Le date sono scritte come numero seriale OADate, la rappresentazione nativa di Excel.</item>
    /// <item>Righe e celle restano in ordine crescente, come richiede lo schema OpenXML.</item>
    /// <item><b>Nessuna formula viene ricalcolata</b> e <c>workbook.xml</c> non viene toccato (niente
    /// <c>fullCalcOnLoad</c>): l'applicazione scrive valori in celle prive di formule, e l'unica
    /// colonna calcolata del report reale (la numerazione progressiva in A, formula condivisa) è
    /// già precompilata nelle righe vuote con il proprio valore in cache. Vedi PROJECT_MEMORY.md
    /// §6.1-vicies-quater.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class ReportInterventiWriter
    {
        private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace S = Ns;

        /// <summary>
        /// Scrive i valori indicati nella riga <paramref name="rowNumber"/> del primo foglio.
        /// </summary>
        /// <param name="filePath">Percorso del workbook (.xlsx o .xlsm) da modificare sul posto.</param>
        /// <param name="rowNumber">Riga di destinazione, 1-based.</param>
        /// <param name="valuesByColumn">
        /// Valori da scrivere, indicizzati per numero di colonna 1-based. Una colonna assente dal
        /// dizionario <b>non viene toccata</b>, e nemmeno una colonna con valore vuoto: è il
        /// comportamento del percorso Interop, che salta le celle senza valore per non sovrascrivere
        /// formule o formattazione preesistenti.
        /// </param>
        public static void WriteRow(string filePath, int rowNumber, IReadOnlyDictionary<int, string?> valuesByColumn)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            ArgumentNullException.ThrowIfNull(valuesByColumn);
            if (rowNumber < 1) throw new ArgumentOutOfRangeException(nameof(rowNumber));

            var values = valuesByColumn
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value!);

            TransformSheet(filePath, (reader, writer) => WriteRowCore(reader, writer, rowNumber, values));
        }

        /// <summary>
        /// L'ultima riga che contiene un valore in almeno una delle colonne chiave, senza aprire il
        /// foglio in memoria. Sostituisce la scansione con ClosedXML che precedeva la scrittura: su
        /// un file reale quella scansione costava **5.694 ms e 1.436 MB** solo per ricavare un
        /// numero di riga, perché <c>XLWorkbook</c> carica l'intero workbook.
        /// </summary>
        /// <param name="filePath">Percorso del workbook.</param>
        /// <param name="keyColumns">Colonne 1-based che qualificano una riga come "compilata".</param>
        /// <returns>L'indice 1-based dell'ultima riga compilata, oppure 0 se non ne esistono.</returns>
        public static int FindLastFilledRow(string filePath, IReadOnlyCollection<int> keyColumns)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            ArgumentNullException.ThrowIfNull(keyColumns);
            if (keyColumns.Count == 0) return 0;

            var wanted = new HashSet<int>(keyColumns);

            using var document = SpreadsheetDocument.Open(filePath, isEditable: false);
            var worksheetPart = GetFirstWorksheetPart(document.WorkbookPart
                    ?? throw new InvalidOperationException("Workbook privo di WorkbookPart: pacchetto non valido."))
                ?? throw new InvalidOperationException("Nessun foglio di lavoro trovato nel workbook.");

            using var stream = worksheetPart.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { CloseInput = false });

            int lastFilled = 0;
            int currentRow = 0;
            bool currentRowCounts = false;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element) continue;

                if (reader.LocalName == "row" && reader.NamespaceURI == Ns)
                {
                    currentRow = ParseRowIndex(reader.GetAttribute("r"));
                    currentRowCounts = false;
                    continue;
                }

                if (reader.LocalName != "c" || reader.NamespaceURI != Ns) continue;
                if (currentRowCounts || currentRow <= lastFilled) continue;

                // Una cella senza contenuto è self-closing: non qualifica la riga come compilata.
                if (reader.IsEmptyElement) continue;
                if (!wanted.Contains(GetColumnNumber(reader.GetAttribute("r")))) continue;

                currentRowCounts = true;
                lastFilled = currentRow;
            }

            return lastFilled;
        }

        /// <summary>
        /// Rimuove dal foglio le righe **completamente vuote**: elementi <c>&lt;row/&gt;</c> privi di
        /// celle e di attributi che portino informazione. <c>&lt;dimension&gt;</c> viene lasciato
        /// com'è: è un suggerimento che Excel ricalcola all'apertura, e riscriverlo richiederebbe una
        /// seconda passata sul foglio per conoscere l'estensione finale prima di emetterlo.
        ///
        /// <para>
        /// <b>Perché serve.</b> Il Report Interventi reale contiene un elemento <c>&lt;row/&gt;</c>
        /// per **ognuna** delle 1.048.576 righe del foglio: 1.028.205 di questi sono segnaposto vuoti
        /// che occupano 40 dei 59 MB del foglio senza portare alcun dato. Ogni lettura o scrittura —
        /// compresa l'apertura del file da parte di Excel — paga quel peso. Rimuoverli porta la parte
        /// da 59 a 19 MB e la scrittura da 1.458 a 288 ms (misurati).
        /// </para>
        ///
        /// <para>
        /// <b>Perché tocca anche <c>zeroHeight</c>, e perché è indispensabile.</b> Il foglio dichiara
        /// <c>&lt;sheetFormatPr zeroHeight="1"&gt;</c>: con quell'attributo, le righe **prive** di un
        /// proprio elemento <c>&lt;row&gt;</c> sono nascoste. Oggi non si nota, perché un elemento
        /// esiste per ogni riga. Rimuovendoli senza altro, tutto ciò che sta sotto l'ultima riga
        /// superstite diventerebbe invisibile in Excel. Portare <c>zeroHeight</c> a <c>"0"</c> fa sì
        /// che le righe non elencate usino <c>defaultRowHeight</c> — cioè restino visibili e alte
        /// esattamente come adesso. La trasformazione è quindi neutra a video, ma **modifica la
        /// struttura del foglio**: è deliberatamente un'operazione esplicita e non un effetto
        /// collaterale della scrittura.
        /// </para>
        /// </summary>
        /// <returns>Il numero di righe vuote rimosse.</returns>
        public static int CompactEmptyRows(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            int removed = 0;
            TransformSheet(filePath, (reader, writer) => removed = CompactCore(reader, writer));
            return removed;
        }

        // ---------------------------------------------------------------------------------------
        // Motore di streaming
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Apre il pacchetto, individua il foglio e ne riscrive la parte facendola passare per
        /// <paramref name="transform"/>. L'output transita da un file temporaneo e non da un
        /// <c>MemoryStream</c>: su un foglio da 59 MB quest'ultimo sarebbe un'allocazione sul Large
        /// Object Heap a ogni salvataggio, la categoria di problema già affrontata in §6.1-sexies.
        /// </summary>
        private static void TransformSheet(string filePath, Action<XmlReader, XmlWriter> transform)
        {
            using var document = SpreadsheetDocument.Open(filePath, isEditable: true);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Workbook privo di WorkbookPart: pacchetto non valido.");

            var worksheetPart = GetFirstWorksheetPart(workbookPart)
                ?? throw new InvalidOperationException("Nessun foglio di lavoro trovato nel workbook.");

            string tempPath = Path.Combine(Path.GetTempPath(), "pat_sheet_" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                using (var source = worksheetPart.GetStream(FileMode.Open, FileAccess.Read))
                using (var reader = XmlReader.Create(source, new XmlReaderSettings { CloseInput = false }))
                using (var destination = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = XmlWriter.Create(destination, new XmlWriterSettings
                {
                    CloseOutput = false,
                    Indent = false,
                    Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                }))
                {
                    writer.WriteStartDocument(standalone: true);
                    reader.MoveToContent();
                    transform(reader, writer);
                }

                using var updated = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                worksheetPart.FeedData(updated);
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* pulizia best-effort */ }
            }
        }

        private static void WriteRowCore(XmlReader reader, XmlWriter writer, int rowNumber, Dictionary<int, string> values)
        {
            bool inSheetData = false;
            bool written = false;

            // Stili della riga precedente a quella di destinazione, per colonna: servono solo se la
            // riga va creata da zero. Vengono raccolti durante la copia, senza una seconda lettura.
            var previousStyles = new Dictionary<int, string>();
            var currentStyles = new Dictionary<int, string>();
            bool collecting = false;

            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns && reader.LocalName == "sheetData")
                {
                    bool empty = reader.IsEmptyElement;
                    writer.WriteStartElement(reader.Prefix, "sheetData", Ns);
                    writer.WriteAttributes(reader, defattr: false);

                    if (empty)
                    {
                        // <sheetData/>: va espanso per poter contenere la riga nuova.
                        WriteNewRow(writer, rowNumber, values, previousStyles);
                        written = true;
                        writer.WriteFullEndElement();
                    }
                    else
                    {
                        inSheetData = true;
                    }

                    reader.Read();
                    continue;
                }

                if (inSheetData && reader.NodeType == XmlNodeType.EndElement && reader.NamespaceURI == Ns && reader.LocalName == "sheetData")
                {
                    if (!written)
                    {
                        // Nessuna riga successiva: la destinazione va in fondo.
                        if (collecting) CopyStyles(currentStyles, previousStyles);
                        WriteNewRow(writer, rowNumber, values, previousStyles);
                        written = true;
                    }

                    inSheetData = false;
                    writer.WriteFullEndElement();
                    reader.Read();
                    continue;
                }

                if (inSheetData && reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns && reader.LocalName == "row")
                {
                    int index = ParseRowIndex(reader.GetAttribute("r"));

                    if (index == rowNumber)
                    {
                        // La riga esiste: la si materializza (una sola riga, non il foglio) per
                        // fondervi i valori conservando celle, stili e formule già presenti.
                        var existing = (XElement)XNode.ReadFrom(reader);
                        MergeValues(existing, values, previousStyles);
                        existing.WriteTo(writer);
                        written = true;
                        continue; // ReadFrom ha già avanzato il reader oltre la riga
                    }

                    if (!written && index > rowNumber)
                    {
                        if (collecting) CopyStyles(currentStyles, previousStyles);
                        WriteNewRow(writer, rowNumber, values, previousStyles);
                        written = true;
                    }

                    if (!written)
                    {
                        // Riga che precede la destinazione: se ne raccolgono gli stili, sovrascrivendo
                        // quelli della riga precedente. Alla fine resta l'ultima riga utile.
                        if (collecting) CopyStyles(currentStyles, previousStyles);
                        currentStyles.Clear();
                        collecting = true;
                    }

                    CopyShallow(reader, writer);
                    reader.Read();
                    continue;
                }

                if (collecting && !written && reader.NodeType == XmlNodeType.Element
                    && reader.NamespaceURI == Ns && reader.LocalName == "c")
                {
                    string? style = reader.GetAttribute("s");
                    if (style != null)
                    {
                        int column = GetColumnNumber(reader.GetAttribute("r"));
                        if (column > 0) currentStyles[column] = style;
                    }
                }

                CopyShallow(reader, writer);
                reader.Read();
            }
        }

        private static int CompactCore(XmlReader reader, XmlWriter writer)
        {
            int removed = 0;
            bool inSheetData = false;

            while (!reader.EOF)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == Ns)
                {
                    switch (reader.LocalName)
                    {
                        case "sheetFormatPr":
                        {
                            // Senza questo, le righe non più elencate risulterebbero nascoste.
                            bool empty = reader.IsEmptyElement;
                            writer.WriteStartElement(reader.Prefix, "sheetFormatPr", Ns);
                            if (reader.MoveToFirstAttribute())
                            {
                                do
                                {
                                    if (reader.LocalName == "zeroHeight" && string.IsNullOrEmpty(reader.Prefix)) continue;
                                    writer.WriteAttributeString(reader.Prefix, reader.LocalName, reader.NamespaceURI, reader.Value);
                                }
                                while (reader.MoveToNextAttribute());
                                reader.MoveToElement();
                            }
                            writer.WriteAttributeString("zeroHeight", "0");
                            if (empty) writer.WriteEndElement();
                            reader.Read();
                            continue;
                        }

                        case "sheetData":
                            inSheetData = !reader.IsEmptyElement;
                            break;

                        case "row" when inSheetData:
                            if (IsDiscardableRow(reader))
                            {
                                removed++;
                                reader.Read(); // elemento vuoto: un solo nodo da consumare
                                continue;
                            }
                            break;
                    }
                }

                if (reader.NodeType == XmlNodeType.EndElement && reader.NamespaceURI == Ns && reader.LocalName == "sheetData")
                {
                    inSheetData = false;
                }

                CopyShallow(reader, writer);
                reader.Read();
            }

            return removed;
        }

        /// <summary>
        /// Vero se la riga è un segnaposto privo di informazione: nessuna cella (elemento vuoto) e
        /// nessun attributo oltre a quelli puramente posizionali/di rendering predefinito.
        /// <c>ht</c>, <c>customHeight</c>, <c>hidden</c>, <c>s</c>, <c>customFormat</c>, <c>outlineLevel</c>
        /// e <c>collapsed</c> portano invece informazione: una riga che li dichiara non va rimossa.
        /// </summary>
        private static bool IsDiscardableRow(XmlReader reader)
        {
            if (!reader.IsEmptyElement) return false;

            if (!reader.MoveToFirstAttribute()) return true;

            bool discardable = true;
            do
            {
                if (reader.LocalName is not ("r" or "spans" or "dyDescent"))
                {
                    discardable = false;
                    break;
                }
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
            return discardable;
        }

        // ---------------------------------------------------------------------------------------
        // Costruzione e aggiornamento della riga
        // ---------------------------------------------------------------------------------------

        private static void CopyStyles(Dictionary<int, string> source, Dictionary<int, string> destination)
        {
            destination.Clear();
            foreach (var (column, style) in source) destination[column] = style;
        }

        private static void WriteNewRow(XmlWriter writer, int rowNumber, Dictionary<int, string> values, Dictionary<int, string> previousStyles)
        {
            var row = new XElement(S + "row", new XAttribute("r", rowNumber));

            foreach (var column in values.Keys.OrderBy(c => c))
            {
                var cell = new XElement(S + "c", new XAttribute("r", GetColumnName(column) + rowNumber.ToString(CultureInfo.InvariantCulture)));
                if (previousStyles.TryGetValue(column, out var style)) cell.SetAttributeValue("s", style);

                SetCellValue(cell, values[column]);
                row.Add(cell);
            }

            row.WriteTo(writer);
        }

        /// <summary>
        /// Fonde i valori nella riga già esistente: le celle presenti conservano il proprio stile e
        /// le colonne non richieste restano intatte, comprese le loro formule.
        /// </summary>
        private static void MergeValues(XElement row, Dictionary<int, string> values, Dictionary<int, string> previousStyles)
        {
            int rowNumber = ParseRowIndex(row.Attribute("r")?.Value);

            foreach (var column in values.Keys.OrderBy(c => c))
            {
                string reference = GetColumnName(column) + rowNumber.ToString(CultureInfo.InvariantCulture);

                var cell = row.Elements(S + "c")
                    .FirstOrDefault(c => string.Equals(c.Attribute("r")?.Value, reference, StringComparison.OrdinalIgnoreCase));

                if (cell == null)
                {
                    cell = new XElement(S + "c", new XAttribute("r", reference));
                    if (previousStyles.TryGetValue(column, out var style)) cell.SetAttributeValue("s", style);

                    var following = row.Elements(S + "c")
                        .FirstOrDefault(c => GetColumnNumber(c.Attribute("r")?.Value) > column);

                    if (following != null) following.AddBeforeSelf(cell);
                    else row.Add(cell);
                }

                SetCellValue(cell, values[column]);
            }
        }

        private static void SetCellValue(XElement cell, string rawValue)
        {
            // Una cella riscritta non conserva formula né contenuto precedente.
            cell.Elements().Remove();
            cell.Attribute("t")?.Remove();

            if (DateTime.TryParseExact(rawValue, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                // Numero seriale OADate: la rappresentazione nativa delle date in Excel. Il formato
                // di visualizzazione viene dallo stile ereditato, come per le righe già presenti.
                cell.Add(new XElement(S + "v", parsedDate.ToOADate().ToString(CultureInfo.InvariantCulture)));
                return;
            }

            if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double numericValue)
                && rawValue.Trim() == numericValue.ToString(CultureInfo.InvariantCulture))
            {
                // Solo se la stringa è esattamente la forma canonica del numero: così un valore come
                // "007" o "1.2.3" resta testo e non viene alterato nella conversione.
                cell.Add(new XElement(S + "v", numericValue.ToString(CultureInfo.InvariantCulture)));
                return;
            }

            // Stringa inline: non tocca sharedStrings.xml, che resta byte-identico.
            cell.SetAttributeValue("t", "inlineStr");
            var text = new XElement(S + "t", rawValue);

            // xml:space="preserve" quando lo spazio iniziale o finale è significativo — esattamente
            // come fa Excel stesso in sharedStrings.xml per i valori del catalogo "Descrizione LRU"
            // (es. " CPUE ALM N B61C.0100003"). Senza questo attributo un processore XML conforme
            // alla specifica può normalizzare quello spazio, facendo divergere silenziosamente il
            // valore scritto da quello richiesto dalla convalida dati.
            if (rawValue.Length > 0 && (char.IsWhiteSpace(rawValue[0]) || char.IsWhiteSpace(rawValue[^1])))
            {
                text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            }

            cell.Add(new XElement(S + "is", text));
        }

        // ---------------------------------------------------------------------------------------
        // Utilità
        // ---------------------------------------------------------------------------------------

        /// <summary>
        /// Copia il nodo corrente **senza discendere**: è la primitiva su cui si regge tutto il
        /// percorso di streaming. <see cref="XmlWriter.WriteNode(XmlReader, bool)"/> non è
        /// utilizzabile qui perché copierebbe l'intero sottoalbero avanzando il reader, togliendo
        /// la possibilità di intervenire sulle singole righe.
        /// </summary>
        private static void CopyShallow(XmlReader reader, XmlWriter writer)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                    writer.WriteAttributes(reader, defattr: false);
                    if (reader.IsEmptyElement) writer.WriteEndElement();
                    break;
                case XmlNodeType.Text:
                    writer.WriteString(reader.Value);
                    break;
                case XmlNodeType.Whitespace:
                case XmlNodeType.SignificantWhitespace:
                    writer.WriteWhitespace(reader.Value);
                    break;
                case XmlNodeType.CDATA:
                    writer.WriteCData(reader.Value);
                    break;
                case XmlNodeType.EntityReference:
                    writer.WriteEntityRef(reader.Name);
                    break;
                case XmlNodeType.ProcessingInstruction:
                    writer.WriteProcessingInstruction(reader.Name, reader.Value);
                    break;
                case XmlNodeType.Comment:
                    writer.WriteComment(reader.Value);
                    break;
                case XmlNodeType.EndElement:
                    // WriteFullEndElement e non WriteEndElement: un elemento aperto e chiuso senza
                    // figli deve restare <a></a> e non diventare <a/>, altrimenti la parte cambia
                    // in punti che non abbiamo motivo di toccare.
                    writer.WriteFullEndElement();
                    break;
            }
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
        ///
        /// <para>
        /// <b>E non è nemmeno l'ordine delle parti.</b> <c>WorksheetParts</c> le restituisce
        /// nell'ordine delle relazioni, che non è quello delle schede: sul report reale il primo
        /// elemento è <c>sheet2.xml</c> ("istruzioni", 12 KB) e non il foglio degli interventi.
        /// Scrivere lì dentro significherebbe scrivere nel foglio sbagliato.
        /// </para>
        /// </summary>
        private static WorksheetPart? GetFirstWorksheetPart(WorkbookPart workbookPart)
        {
            try
            {
                using var stream = workbookPart.GetStream(FileMode.Open, FileAccess.Read);
                var document = XDocument.Load(stream);

                XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

                string? relationshipId = document.Root?
                    .Element(S + "sheets")?
                    .Elements(S + "sheet")
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

        private static int ParseRowIndex(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : 0;

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
