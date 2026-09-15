using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using PersonalAutomationTool.Core;

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
        /// Scrive i valori indicati nella riga <paramref name="rowNumber"/> del foglio
        /// <paramref name="sheetName"/>, risolto **per nome esatto** (case-insensitive, con trim) e
        /// mai per posizione — vedi <see cref="GetWorksheetPartByName"/>.
        /// </summary>
        /// <param name="filePath">Percorso del workbook (.xlsx o .xlsm) da modificare sul posto.</param>
        /// <param name="sheetName">
        /// Nome esatto del foglio interventi, così come appare nella scheda in Excel (es. "Interventi
        /// ETR500"). Se il workbook non contiene un foglio con questo nome, l'operazione fallisce con
        /// <see cref="InvalidOperationException"/> invece di scrivere su un foglio diverso.
        /// </param>
        /// <param name="rowNumber">Riga di destinazione, 1-based.</param>
        /// <param name="valuesByColumn">
        /// Valori da scrivere, indicizzati per numero di colonna 1-based. Una colonna assente dal
        /// dizionario <b>non viene toccata</b>, e nemmeno una colonna con valore vuoto: è il
        /// comportamento del percorso Interop, che salta le celle senza valore per non sovrascrivere
        /// formule o formattazione preesistenti.
        /// </param>
        public static ReportRowWriteResult WriteRow(string filePath, string sheetName, int rowNumber, IReadOnlyDictionary<int, string?> valuesByColumn)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            ArgumentException.ThrowIfNullOrEmpty(sheetName);
            ArgumentNullException.ThrowIfNull(valuesByColumn);
            if (rowNumber < 1) throw new ArgumentOutOfRangeException(nameof(rowNumber));

            var values = valuesByColumn
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value!);

            // Contenuto della riga di destinazione PRIMA di toccarla: se non è vuota lo si scopre qui,
            // e resta agli atti nel log diagnostico invece di andare perso in una sovrascrittura muta.
            var before = ReadRowValues(filePath, sheetName, rowNumber, values.Keys);

            TransformSheet(filePath, sheetName,
                (reader, writer) => WriteRowCore(reader, writer, rowNumber, values),
                tableRowToInclude: rowNumber);

            // Riletto dal file ATTIVO, non dalla copia di lavoro: è la differenza fra "ho eseguito la
            // scrittura" e "il report contiene davvero questi valori".
            var after = ReadRowValues(filePath, sheetName, rowNumber, values.Keys);

            var mismatched = values.Keys
                .Where(column => !string.Equals(
                    after.GetValueOrDefault(column, string.Empty),
                    ToStoredValue(values[column]),
                    StringComparison.Ordinal))
                .OrderBy(column => column)
                .ToList();

            if (mismatched.Count > 0)
            {
                // Il difetto segnalato dal committente era esattamente questo: l'applicazione annunciava
                // "salvato alla riga N" mentre nel foglio non compariva nulla. Con questa verifica quel
                // caso diventa un errore visibile, non un falso successo.
                throw new InvalidOperationException(
                    $"Verifica dopo la scrittura fallita sul foglio '{sheetName}', riga {rowNumber}: " +
                    string.Join("; ", mismatched.Select(column =>
                        $"colonna {GetColumnName(column)} attesa '{ToStoredValue(values[column])}', letta '{after.GetValueOrDefault(column, string.Empty)}'")));
            }

            return new ReportRowWriteResult(sheetName, rowNumber, before, after);
        }

        /// <summary>
        /// L'ultima riga che contiene un valore in almeno una delle colonne chiave, senza aprire il
        /// foglio in memoria. Sostituisce la scansione con ClosedXML che precedeva la scrittura: su
        /// un file reale quella scansione costava **5.694 ms e 1.436 MB** solo per ricavare un
        /// numero di riga, perché <c>XLWorkbook</c> carica l'intero workbook.
        /// </summary>
        /// <param name="filePath">Percorso del workbook.</param>
        /// <param name="sheetName">Nome esatto del foglio interventi (vedi <see cref="WriteRow"/>).</param>
        /// <param name="keyColumns">Colonne 1-based che qualificano una riga come "compilata".</param>
        /// <returns>L'indice 1-based dell'ultima riga compilata, oppure 0 se non ne esistono.</returns>
        public static int FindLastFilledRow(string filePath, string sheetName, IReadOnlyCollection<int> keyColumns)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            ArgumentException.ThrowIfNullOrEmpty(sheetName);
            ArgumentNullException.ThrowIfNull(keyColumns);
            if (keyColumns.Count == 0) return 0;

            var wanted = new HashSet<int>(keyColumns);

            using var document = SpreadsheetDocument.Open(filePath, isEditable: false);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Workbook privo di WorkbookPart: pacchetto non valido.");
            var worksheetPart = GetWorksheetPartByName(workbookPart, sheetName);
            var blankSharedStrings = LoadBlankSharedStringIndexes(workbookPart);

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

                // Una cella self-closing (<c r="B12" s="3"/>) non ha contenuto per costruzione.
                if (reader.IsEmptyElement) continue;
                if (!wanted.Contains(GetColumnNumber(reader.GetAttribute("r")))) continue;

                // **Non basta che la cella esista**: va guardato il valore. Vedi HasRealValue.
                string? cellType = reader.GetAttribute("t");
                bool real;
                using (var cell = reader.ReadSubtree())
                {
                    cell.Read(); // posiziona il sotto-lettore su <c>
                    real = HasRealValue(cell, cellType, blankSharedStrings);
                }
                if (!real) continue;

                currentRowCounts = true;
                lastFilled = currentRow;
            }

            return lastFilled;
        }

        /// <summary>
        /// Vero se la cella in corso di lettura porta un valore **reale**: non basta che l'elemento
        /// <c>&lt;c&gt;</c> esista, e non basta nemmeno che non sia self-closing.
        ///
        /// <para>
        /// <b>Perché il controllo strutturale non basta (bug ETR1000 I-F, §6.1-tricies-septies).</b> Le
        /// righe già formattate in coda all'area dati — bordi, sfondo, convalide pronte per il prossimo
        /// intervento — sono indistinguibili dalle righe compilate se ci si limita a contare i nodi
        /// <c>&lt;row&gt;</c> o a guardare se la cella è self-closing. Excel scrive quelle celle come
        /// <c>&lt;c r="B12" s="3"/&gt;</c>, ma <b>non è garantito</b>: la stessa cella vuota può arrivare
        /// come <c>&lt;c r="B12" s="3"&gt;&lt;/c&gt;</c>, come <c>&lt;v&gt;&lt;/v&gt;</c>, o come una
        /// stringa fatta di soli spazi — tre forme che il controllo precedente contava come "riga
        /// occupata", facendo scivolare in avanti la riga di inserimento su ogni riga preformattata.
        /// </para>
        ///
        /// <para>
        /// Una stringa condivisa viene risolta tramite <paramref name="blankSharedStrings"/>: il
        /// <c>&lt;v&gt;</c> di una cella <c>t="s"</c> è un <i>indice</i>, non un testo, e l'indice di una
        /// stringa vuota è un numero come gli altri — contarlo come valore è l'errore più facile da
        /// commettere qui.
        /// </para>
        /// </summary>
        /// <param name="cell">Sotto-lettore posizionato sull'elemento <c>&lt;c&gt;</c>.</param>
        /// <param name="cellType">Attributo <c>t</c> della cella (<c>s</c>, <c>inlineStr</c>, <c>str</c>, <c>b</c>, <c>e</c>, oppure assente per i numeri).</param>
        /// <param name="blankSharedStrings">Indici delle stringhe condivise vuote o di soli spazi.</param>
        private static bool HasRealValue(XmlReader cell, string? cellType, HashSet<int> blankSharedStrings)
        {
            var (text, sharedIndex) = ReadCellContent(cell, cellType);

            // Una stringa condivisa "piena" di nulla vale quanto una cella vuota.
            if (sharedIndex is int index) return !blankSharedStrings.Contains(index);

            return !string.IsNullOrWhiteSpace(text);
        }

        /// <summary>
        /// Contenuto grezzo della cella su cui è posizionato <paramref name="cell"/>: il testo, oppure
        /// l'indice della stringa condivisa se la cella è di tipo <c>s</c> (in quel caso il testo va
        /// risolto a parte, perché vive in <c>sharedStrings.xml</c>).
        /// </summary>
        private static (string Text, int? SharedIndex) ReadCellContent(XmlReader cell, string? cellType)
        {
            string? sharedIndex = null;
            var text = new StringBuilder();

            while (cell.Read())
            {
                if (cell.NodeType != XmlNodeType.Element || cell.NamespaceURI != Ns) continue;

                if (cell.LocalName == "v")
                {
                    string value = cell.ReadElementContentAsString();
                    if (string.Equals(cellType, "s", StringComparison.Ordinal)) sharedIndex = value;
                    else text.Append(value);
                }
                else if (cell.LocalName == "t")
                {
                    // Dentro <is> (stringa inline), anche quando è spezzata in più <r><t>…</t></r>.
                    text.Append(cell.ReadElementContentAsString());
                }
            }

            if (string.Equals(cellType, "s", StringComparison.Ordinal) &&
                int.TryParse(sharedIndex, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
                return (string.Empty, index);
            }

            return (text.ToString(), null);
        }

        /// <summary>
        /// Rilegge dal file le colonne indicate della riga <paramref name="rowNumber"/>, risolvendo le
        /// stringhe condivise. Si ferma appena superata la riga cercata: su un report con un milione di
        /// righe la verifica costa quanto arrivare alla riga di destinazione, non quanto leggere il foglio.
        /// </summary>
        private static Dictionary<int, string> ReadRowValues(string filePath, string sheetName, int rowNumber, IEnumerable<int> columns)
        {
            var wanted = new HashSet<int>(columns);
            var result = new Dictionary<int, string>();
            if (wanted.Count == 0) return result;

            using var document = SpreadsheetDocument.Open(filePath, isEditable: false);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Workbook privo di WorkbookPart: pacchetto non valido.");

            var worksheetPart = GetWorksheetPartByName(workbookPart, sheetName);
            var sharedByColumn = new Dictionary<int, int>();

            using (var stream = worksheetPart.GetStream(FileMode.Open, FileAccess.Read))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings { CloseInput = false }))
            {
                int currentRow = 0;

                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element) continue;

                    if (reader.LocalName == "row" && reader.NamespaceURI == Ns)
                    {
                        currentRow = ParseRowIndex(reader.GetAttribute("r"));
                        if (currentRow > rowNumber) break; // le righe sono in ordine crescente
                        continue;
                    }

                    if (currentRow != rowNumber) continue;
                    if (reader.LocalName != "c" || reader.NamespaceURI != Ns) continue;

                    int column = GetColumnNumber(reader.GetAttribute("r"));
                    if (!wanted.Contains(column)) continue;

                    if (reader.IsEmptyElement)
                    {
                        result[column] = string.Empty;
                        continue;
                    }

                    string? cellType = reader.GetAttribute("t");
                    using var cell = reader.ReadSubtree();
                    cell.Read();

                    var (text, sharedIndex) = ReadCellContent(cell, cellType);
                    if (sharedIndex is int index) sharedByColumn[column] = index;
                    else result[column] = text;
                }
            }

            if (sharedByColumn.Count > 0) ResolveSharedStrings(workbookPart, sharedByColumn, result);

            foreach (int column in wanted) result.TryAdd(column, string.Empty);
            return result;
        }

        /// <summary>
        /// Risolve i soli indici di stringa condivisa effettivamente incontrati nella riga letta,
        /// scorrendo <c>sharedStrings.xml</c> una volta sola e senza materializzare l'intera tabella.
        /// </summary>
        private static void ResolveSharedStrings(WorkbookPart workbookPart, Dictionary<int, int> sharedByColumn, Dictionary<int, string> result)
        {
            var texts = new Dictionary<int, string>();

            if (workbookPart.SharedStringTablePart is { } part)
            {
                var wantedIndexes = new HashSet<int>(sharedByColumn.Values);

                using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
                using var reader = XmlReader.Create(stream, new XmlReaderSettings { CloseInput = false });

                int index = -1;
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si" || reader.NamespaceURI != Ns) continue;

                    index++;
                    if (!wantedIndexes.Contains(index)) continue;

                    var text = new StringBuilder();
                    using (var item = reader.ReadSubtree())
                    {
                        item.Read();
                        while (item.Read())
                        {
                            if (item.NodeType == XmlNodeType.Element && item.LocalName == "t" && item.NamespaceURI == Ns)
                            {
                                text.Append(item.ReadElementContentAsString());
                            }
                        }
                    }

                    texts[index] = text.ToString();
                    if (texts.Count == wantedIndexes.Count) break;
                }
            }

            foreach (var (column, index) in sharedByColumn)
            {
                result[column] = texts.TryGetValue(index, out string? text) ? text : string.Empty;
            }
        }

        /// <summary>
        /// Gli indici delle stringhe condivise il cui testo è vuoto o composto di soli spazi, letti in
        /// streaming da <c>sharedStrings.xml</c>. Si conservano **solo gli indici vuoti** — tipicamente
        /// una manciata su migliaia di voci — invece dell'intera tabella: è ciò che permette il
        /// controllo rigoroso senza rinunciare alla frugalità di memoria che è l'invariante di questa
        /// classe (§6.1-vicies-quater).
        /// </summary>
        private static HashSet<int> LoadBlankSharedStringIndexes(WorkbookPart workbookPart)
        {
            var blank = new HashSet<int>();
            if (workbookPart.SharedStringTablePart is not { } part) return blank;

            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { CloseInput = false });

            int index = -1;
            bool hasText = false;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si" && reader.NamespaceURI == Ns)
                {
                    if (index >= 0 && !hasText) blank.Add(index);
                    index++;
                    hasText = false;
                    continue;
                }

                if (index < 0) continue;

                if (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace
                    && !string.IsNullOrWhiteSpace(reader.Value))
                {
                    hasText = true;
                }
            }

            if (index >= 0 && !hasText) blank.Add(index);
            return blank;
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
        /// <param name="filePath">Percorso del workbook.</param>
        /// <param name="sheetName">Nome esatto del foglio interventi (vedi <see cref="WriteRow"/>).</param>
        /// <returns>Il numero di righe vuote rimosse.</returns>
        public static int CompactEmptyRows(string filePath, string sheetName)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            ArgumentException.ThrowIfNullOrEmpty(sheetName);

            int removed = 0;
            TransformSheet(filePath, sheetName, (reader, writer) => removed = CompactCore(reader, writer));
            return removed;
        }

        // ---------------------------------------------------------------------------------------
        // Motore di streaming
        // ---------------------------------------------------------------------------------------

        /// <summary>Tentativi della copia finale sul file attivo: copre l'aggancio del client OneDrive/SharePoint.</summary>
        private const int SafeSaveMaxAttempts = 5;

        /// <summary>Attesa prima della prima riprova della copia finale; raddoppia a ogni tentativo (500, 1000, 2000, 4000 ms).</summary>
        private const int SafeSaveInitialDelayMs = 500;

        /// <summary>
        /// Solo per i test: accorcia l'attesa fra le riprove. In esercizio quei millisecondi servono ad
        /// aspettare il client OneDrive/SharePoint che rilascia il file; in una suite che provoca
        /// <b>volutamente</b> un blocco sarebbero solo secondi di attesa a ogni esecuzione.
        /// Stesso trattamento di <c>CrashReporter.PercorsoLogSostitutivo</c>.
        /// </summary>
        internal static int SafeSaveDelayMsSostitutivo { get; set; } = SafeSaveInitialDelayMs;

        /// <summary>
        /// Applica <paramref name="transform"/> al foglio <paramref name="sheetName"/> con un
        /// **salvataggio transazionale**: il file attivo non viene mai aperto in scrittura, né spostato,
        /// né cancellato. Si lavora su una copia in <see cref="AppPaths.TempFolder"/> e solo una versione
        /// completa e validata prende il posto dell'originale.
        ///
        /// <para>
        /// <b>Il difetto che questo sostituisce (bug ETR500, §6.1-tricies-septies).</b> La versione
        /// precedente apriva il file attivo con <c>SpreadsheetDocument.Open(filePath, isEditable: true)</c>
        /// e lo riscriveva sul posto con <c>FeedData</c>. Su una cartella sincronizzata è la premessa
        /// perfetta per perdere il file: il pacchetto <c>.xlsx</c> è un archivio ZIP, riscriverlo sul posto
        /// significa troncarlo e ricostruirlo, e qualunque interruzione in quella finestra — un
        /// <c>ERROR_SHARING_VIOLATION</c> del demone di sincronizzazione, un'eccezione, la chiusura
        /// dell'applicazione — lascia sulla cartella SharePoint un archivio incompleto, che il client di
        /// sync propaga o rifiuta e che Excel non riapre più. Dal punto di vista del tecnico: "il report
        /// è sparito".
        /// </para>
        ///
        /// <para>
        /// <b>La sequenza, nell'ordine in cui conta.</b>
        /// <list type="number">
        /// <item>Copia del file attivo nella cartella di lavoro (l'originale resta dov'è, intatto).</item>
        /// <item>Trasformazione applicata <b>alla copia</b>.</item>
        /// <item>Validazione della copia: esiste, non è vuota, si riapre come pacchetto OpenXML e
        /// contiene ancora il foglio di destinazione.</item>
        /// <item>Solo ora la copia sostituisce l'originale, con <c>File.Copy(overwrite: true)</c> e
        /// riprova a backoff esponenziale sulle violazioni di condivisione.</item>
        /// <item>Il temporaneo viene rimosso <b>solo</b> a sostituzione confermata; se qualcosa fallisce
        /// resta su disco e il suo percorso finisce nel messaggio d'errore, perché contiene comunque il
        /// lavoro appena scritto.</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="tableRowToInclude">
        /// Se valorizzato, la riga appena scritta viene inclusa nell'intervallo delle eventuali tabelle
        /// strutturate del foglio (vedi <see cref="ExpandTablesToRow"/>).
        /// </param>
        private static void TransformSheet(string filePath, string sheetName, Action<XmlReader, XmlWriter> transform, int? tableRowToInclude = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Report Interventi non trovato: '{filePath}'.", filePath);
            }

            // Il nome porta quello del report: se un salvataggio fallisce, il file di lavoro resta su
            // disco ed è l'unica copia che contiene la scrittura appena eseguita — dev'essere
            // riconducibile a colpo d'occhio al report da cui proviene, non un GUID anonimo.
            string workingCopy = AppPaths.TempFile(
                $"{WorkingCopyPrefix(filePath)}_{Guid.NewGuid():N}{Path.GetExtension(filePath)}");

            // L'originale viene solo LETTO: su violazione di condivisione si riprova, non si forza.
            FileOperationRetry.Execute(
                () => File.Copy(filePath, workingCopy, overwrite: true),
                SafeSaveMaxAttempts, SafeSaveDelayMsSostitutivo);

            bool replaced = false;
            try
            {
                using (var document = SpreadsheetDocument.Open(workingCopy, isEditable: true))
                {
                    var workbookPart = document.WorkbookPart
                        ?? throw new InvalidOperationException("Workbook privo di WorkbookPart: pacchetto non valido.");

                    var worksheetPart = GetWorksheetPartByName(workbookPart, sheetName);

                    string sheetXmlPath = Path.Combine(Path.GetTempPath(), "pat_sheet_" + Guid.NewGuid().ToString("N") + ".xml");
                    try
                    {
                        // Lo scambio passa da un file e non da un MemoryStream: su un foglio da 59 MB
                        // quest'ultimo sarebbe un'allocazione sul Large Object Heap a ogni salvataggio,
                        // la categoria di problema già affrontata in §6.1-sexies.
                        using (var source = worksheetPart.GetStream(FileMode.Open, FileAccess.Read))
                        using (var reader = XmlReader.Create(source, new XmlReaderSettings { CloseInput = false }))
                        using (var destination = new FileStream(sheetXmlPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var writer = XmlWriter.Create(destination, new XmlWriterSettings
                        {
                            CloseOutput = false,
                            Indent = false,
                            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                        }))
                        {
                            writer.WriteStartDocument(standalone: true);
                            reader.MoveToContent();
                            transform(reader, writer);
                        }

                        using var updated = new FileStream(sheetXmlPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        worksheetPart.FeedData(updated);
                    }
                    finally
                    {
                        try { if (File.Exists(sheetXmlPath)) File.Delete(sheetXmlPath); } catch { /* pulizia best-effort */ }
                    }

                    if (tableRowToInclude is int tableRow) ExpandTablesToRow(worksheetPart, tableRow);
                }

                ValidateWorkbook(workingCopy, sheetName);

                // Sostituzione del file attivo. È il primo e unico momento in cui l'originale viene
                // toccato, ed è una sovrascrittura: mai un Delete seguito da una Copy, che lascerebbe
                // una finestra in cui il file non esiste da nessuna parte.
                FileOperationRetry.Execute(
                    () => File.Copy(workingCopy, filePath, overwrite: true),
                    SafeSaveMaxAttempts, SafeSaveDelayMsSostitutivo);

                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    throw new IOException(
                        $"La sostituzione del report non è andata a buon fine: '{filePath}' risulta assente o vuoto. " +
                        $"La versione aggiornata è disponibile in '{workingCopy}'.");
                }

                replaced = true;
            }
            finally
            {
                // Solo a sostituzione confermata: se è fallita, il temporaneo è l'unica copia che
                // contiene la scrittura appena eseguita e va conservata.
                if (replaced)
                {
                    try { File.Delete(workingCopy); } catch { /* pulizia best-effort */ }
                }
            }
        }

        /// <summary>
        /// Prefisso del file di lavoro per un dato report: <c>report_temp_&lt;nome del report&gt;</c>,
        /// ripulito dai caratteri non ammessi e troncato, perché un nome di report reale è lungo e
        /// contiene spazi e punti. Esposto ai test, che lo usano per ritrovare il file di lavoro del
        /// proprio report senza inciampare in quelli delle suite che girano in parallelo.
        /// </summary>
        internal static string WorkingCopyPrefix(string filePath)
        {
            var name = new StringBuilder("report_temp_");
            foreach (char character in Path.GetFileNameWithoutExtension(filePath))
            {
                name.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0 ? '_' : character);
            }

            const int lunghezzaMassima = 80;
            return name.Length > lunghezzaMassima ? name.ToString(0, lunghezzaMassima) : name.ToString();
        }

        /// <summary>
        /// Rilegge il file appena prodotto come pacchetto OpenXML e verifica che contenga ancora il
        /// foglio di destinazione con il suo <c>&lt;sheetData&gt;</c>. È la barriera fra "ho scritto
        /// qualcosa" e "posso sostituire il report aziendale": un pacchetto troncato o senza il foglio
        /// non deve mai arrivare sulla cartella sincronizzata.
        /// </summary>
        private static void ValidateWorkbook(string filePath, string sheetName)
        {
            if (!File.Exists(filePath))
            {
                throw new IOException($"Il file di lavoro '{filePath}' non è stato prodotto.");
            }

            if (new FileInfo(filePath).Length == 0)
            {
                throw new IOException($"Il file di lavoro '{filePath}' è vuoto: scrittura non valida.");
            }

            using var document = SpreadsheetDocument.Open(filePath, isEditable: false);
            var workbookPart = document.WorkbookPart
                ?? throw new IOException($"Il file di lavoro '{filePath}' non è un pacchetto OpenXML valido.");

            var worksheetPart = GetWorksheetPartByName(workbookPart, sheetName);

            using var stream = worksheetPart.GetStream(FileMode.Open, FileAccess.Read);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { CloseInput = false });

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "sheetData" && reader.NamespaceURI == Ns)
                {
                    return;
                }
            }

            throw new IOException($"Il foglio '{sheetName}' del file di lavoro '{filePath}' è privo di <sheetData>.");
        }

        /// <summary>
        /// Estende l'intervallo delle tabelle strutturate (ListObject) del foglio fino a includere la
        /// riga <paramref name="rowNumber"/> appena scritta. Senza questo, una riga aggiunta appena sotto
        /// una tabella resta fuori dal suo <c>ref</c>: Excel la mostra come riga libera, le formule
        /// strutturate e i filtri della tabella la ignorano, e in alcuni casi il file viene segnalato come
        /// da riparare.
        ///
        /// <para>
        /// <b>Cosa viene aggiornato e cosa no.</b> Si aggiornano <c>ref</c> della tabella e <c>ref</c>
        /// del suo <c>&lt;autoFilter&gt;</c>, che sono intervalli e quindi dipendono dal numero di righe.
        /// <b>Non</b> si tocca <c>&lt;tableColumns count&gt;</c>: quel contatore conta le <i>colonne</i>,
        /// non le righe — incrementarlo a ogni riga aggiunta lo porterebbe a divergere dal numero di
        /// <c>&lt;tableColumn&gt;</c> effettivi, ed è esattamente ciò che fa dichiarare il file
        /// danneggiato a Excel.
        /// </para>
        ///
        /// <para>
        /// Nessuno dei quattro Report Interventi aziendali usa oggi una tabella strutturata (verificato
        /// sui file reali: zero <c>TableDefinitionPart</c>). Questo percorso esiste perché la struttura
        /// dei report cambia nel tempo senza preavviso, e una riga scritta fuori tabella è un difetto
        /// silenzioso: meglio gestirlo ora che scoprirlo su un report reale.
        /// </para>
        /// </summary>
        private static void ExpandTablesToRow(WorksheetPart worksheetPart, int rowNumber)
        {
            foreach (var tablePart in worksheetPart.TableDefinitionParts)
            {
                // XML grezzo e non <c>tablePart.Table</c>: il solo accesso a quella proprietà carica il
                // DOM e l'SDK **riscrive la parte alla chiusura del documento**, anche senza modifiche.
                // Sarebbe lo stesso effetto collaterale già scoperto su workbook.xml (§6.1-vicies-quater),
                // e qui l'ha intercettato di nuovo la suite di integrità: una tabella non toccata deve
                // restare byte per byte identica.
                XDocument document;
                using (var stream = tablePart.GetStream(FileMode.Open, FileAccess.Read))
                {
                    document = XDocument.Load(stream);
                }

                var root = document.Root;
                if (root?.Attribute("ref")?.Value is not { } reference) continue;
                if (!TryParseRowSpan(reference, out int firstRow, out int lastRow)) continue;
                if (rowNumber <= lastRow || rowNumber < firstRow) continue;

                root.SetAttributeValue("ref", ReplaceLastRow(reference, rowNumber));

                var autoFilter = root.Element(S + "autoFilter");
                if (autoFilter?.Attribute("ref")?.Value is { } filterReference &&
                    TryParseRowSpan(filterReference, out _, out int filterLastRow) &&
                    rowNumber > filterLastRow)
                {
                    autoFilter.SetAttributeValue("ref", ReplaceLastRow(filterReference, rowNumber));
                }

                // Una definizione di tabella sta in pochi KB: qui il MemoryStream è appropriato, a
                // differenza del foglio (decine di MB, §6.1-sexies).
                using var buffer = new MemoryStream();
                using (var writer = XmlWriter.Create(buffer, new XmlWriterSettings
                {
                    CloseOutput = false,
                    Indent = false,
                    Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                }))
                {
                    document.Save(writer);
                }

                buffer.Position = 0;
                tablePart.FeedData(buffer);
            }
        }

        /// <summary>Prima e ultima riga di un riferimento tipo <c>"A1:N100"</c>.</summary>
        private static bool TryParseRowSpan(string reference, out int firstRow, out int lastRow)
        {
            firstRow = 0;
            lastRow = 0;

            string[] ends = reference.Split(':');
            if (ends.Length != 2) return false;

            firstRow = ParseRowIndex(new string(ends[0].SkipWhile(char.IsLetter).ToArray()));
            lastRow = ParseRowIndex(new string(ends[1].SkipWhile(char.IsLetter).ToArray()));
            return firstRow > 0 && lastRow > 0;
        }

        /// <summary>Sostituisce la riga finale di un riferimento: <c>"A1:N100"</c> + 101 → <c>"A1:N101"</c>.</summary>
        private static string ReplaceLastRow(string reference, int lastRow)
        {
            string[] ends = reference.Split(':');
            string columns = new(ends[1].TakeWhile(char.IsLetter).ToArray());
            return $"{ends[0]}:{columns}{lastRow.ToString(CultureInfo.InvariantCulture)}";
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

            if (TryGetNumericStoredValue(rawValue, out string numericStored))
            {
                cell.Add(new XElement(S + "v", numericStored));
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

        /// <summary>
        /// La forma **realmente memorizzata** nella cella per <paramref name="rawValue"/>: seriale
        /// OADate per una data <c>dd/MM/yyyy</c>, forma canonica per un numero, la stringa stessa
        /// altrimenti. È il termine di paragone della verifica dopo la scrittura: confrontare con il
        /// valore digitato dal tecnico darebbe falsi allarmi su ogni data e su ogni numero.
        /// </summary>
        internal static string ToStoredValue(string rawValue) =>
            TryGetNumericStoredValue(rawValue, out string stored) ? stored : rawValue;

        /// <summary>
        /// Vero se il valore va scritto come numero (data seriale o numero canonico), con la forma da
        /// mettere in <c>&lt;v&gt;</c>. Falso per tutto il resto, che resta testo.
        /// </summary>
        private static bool TryGetNumericStoredValue(string rawValue, out string stored)
        {
            if (DateTime.TryParseExact(rawValue, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                // Numero seriale OADate: la rappresentazione nativa delle date in Excel. Il formato
                // di visualizzazione viene dallo stile ereditato, come per le righe già presenti.
                stored = parsedDate.ToOADate().ToString(CultureInfo.InvariantCulture);
                return true;
            }

            if (double.TryParse(rawValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double numericValue)
                && rawValue.Trim() == numericValue.ToString(CultureInfo.InvariantCulture))
            {
                // Solo se la stringa è esattamente la forma canonica del numero: così un valore come
                // "007" o "1.2.3" resta testo e non viene alterato nella conversione.
                stored = numericValue.ToString(CultureInfo.InvariantCulture);
                return true;
            }

            stored = rawValue;
            return false;
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
        /// Il foglio il cui nome, in <c>workbook.xml</c>, corrisponde a <paramref name="sheetName"/>
        /// — confronto case-insensitive e con trim, **mai per posizione**.
        ///
        /// <para>
        /// <b>Perché non per posizione.</b> Il bug che questo metodo corregge: "Scrivi report" su ETR500
        /// finiva nel foglio "istruzioni" invece che in "Interventi ETR500". Causa reale, verificata sul
        /// file aziendale — non un'ipotesi: la prima scheda del workbook (ordine di <c>&lt;sheets&gt;</c>)
        /// è <c>"Grafico1"</c>, un <b>foglio grafico</b> (<c>ChartsheetPart</c>), non un
        /// <c>WorksheetPart</c>. La vecchia logica, non trovando lì un foglio di lavoro, ripiegava su
        /// <c>WorksheetParts.FirstOrDefault()</c> — l'ordine delle relazioni nel pacchetto, non quello
        /// delle schede — che su quel file restituiva "istruzioni" invece del foglio interventi. Lo
        /// stesso schema (foglio grafico o "Foglio1" prima del foglio interventi in
        /// <c>&lt;sheets&gt;</c>) si ripete su ETR1000 I-F. **Nessuna posizione è quindi affidabile**:
        /// l'unico ancoraggio corretto è il nome del foglio, esplicito in ogni chiamata.
        /// </para>
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
        /// <exception cref="InvalidOperationException">
        /// Nessun foglio con questo nome nel workbook, oppure il nome corrisponde a una parte che non
        /// è un foglio di lavoro (es. un foglio grafico). Deliberatamente esplicita: ricadere in
        /// silenzio su un foglio diverso è esattamente il difetto che questo metodo elimina.
        /// </exception>
        private static WorksheetPart GetWorksheetPartByName(WorkbookPart workbookPart, string sheetName)
        {
            using var stream = workbookPart.GetStream(FileMode.Open, FileAccess.Read);
            var document = XDocument.Load(stream);

            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            string trimmedName = sheetName.Trim();

            string? relationshipId = document.Root?
                .Element(S + "sheets")?
                .Elements(S + "sheet")
                .FirstOrDefault(sheet => string.Equals(
                    sheet.Attribute("name")?.Value?.Trim(), trimmedName, StringComparison.OrdinalIgnoreCase))
                ?.Attribute(rel + "id")?.Value;

            if (string.IsNullOrEmpty(relationshipId))
            {
                throw new InvalidOperationException($"Foglio interventi non trovato nel file Excel: '{sheetName}'.");
            }

            if (workbookPart.GetPartById(relationshipId) is not WorksheetPart part)
            {
                throw new InvalidOperationException($"Il foglio '{sheetName}' non è un foglio di lavoro valido nel file Excel.");
            }

            return part;
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

    /// <summary>
    /// Esito <b>verificato</b> di una scrittura di riga: cosa conteneva la riga di destinazione prima,
    /// e cosa risulta riletto dal file attivo dopo. Serve a rendere diagnosticabile dal campo il difetto
    /// più insidioso di questo modulo — l'applicazione che annuncia "salvato alla riga N" mentre il
    /// foglio non è cambiato (§6.1-tricies-septies).
    /// </summary>
    /// <param name="SheetName">Foglio su cui la scrittura è avvenuta davvero.</param>
    /// <param name="RowNumber">Riga di destinazione, 1-based.</param>
    /// <param name="ValuesBefore">Contenuto delle colonne scritte prima della scrittura (vuoto se la riga era libera).</param>
    /// <param name="ValuesAfter">Contenuto delle stesse colonne riletto dal file attivo dopo la sostituzione.</param>
    public sealed record ReportRowWriteResult(
        string SheetName,
        int RowNumber,
        IReadOnlyDictionary<int, string> ValuesBefore,
        IReadOnlyDictionary<int, string> ValuesAfter)
    {
        /// <summary>Vero se la riga di destinazione conteneva già qualcosa nelle colonne scritte.</summary>
        public bool RowWasOccupied => ValuesBefore.Any(entry => !string.IsNullOrWhiteSpace(entry.Value));

        /// <summary>Riga singola per il log diagnostico, con i valori prima e dopo per colonna.</summary>
        public string ToLogEntry()
        {
            static string Format(IReadOnlyDictionary<int, string> values) =>
                values.Count == 0
                    ? "(nessuna colonna)"
                    : string.Join(", ", values.OrderBy(entry => entry.Key)
                        .Select(entry => $"{ReportInterventiWriter.GetColumnName(entry.Key)}='{entry.Value}'"));

            return $"foglio '{SheetName}', riga {RowNumber}, riga già occupata: {(RowWasOccupied ? "SÌ" : "no")}" +
                   $"{Environment.NewLine}  prima: {Format(ValuesBefore)}" +
                   $"{Environment.NewLine}  dopo : {Format(ValuesAfter)}";
        }
    }
}
