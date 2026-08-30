using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using PersonalAutomationTool.Core;

namespace PersonalAutomationTool.Modules.Verifiche
{
    /// <summary>Esito dell'archiviazione di una verifica.</summary>
    /// <param name="Riuscita">Vero se il file è stato aggiornato e rinominato.</param>
    /// <param name="Messaggio">Descrizione dell'esito, mostrabile al tecnico.</param>
    /// <param name="NuovoPercorso">Percorso del file rinominato, se l'operazione è riuscita.</param>
    /// <param name="PercorsoBackup">Percorso della copia di sicurezza nella cartella OLD.</param>
    public sealed record ArchiviazioneEsito(bool Riuscita, string Messaggio, string? NuovoPercorso = null, string? PercorsoBackup = null);

    /// <summary>
    /// Archivia una riga di VERIFICHE: la copia nel foglio storico dell'anno corrente, la rimuove dal
    /// foglio principale e rinomina il file con data, ora e cognome del tecnico.
    ///
    /// <para>
    /// <b>Modifica chirurgica in OpenXML, non riscrittura.</b> Questi workbook contengono fogli
    /// nascosti ("non cancellare"), storici di dieci anni, filtri e stili accumulati nel tempo:
    /// riaprirli e risalvarli con ClosedXML ne riscriverebbe l'intero pacchetto, aggiungendo e
    /// rimuovendo parti (misurato in §6.1-septies di PROJECT_MEMORY.md). Qui si toccano solo gli
    /// elementi <c>&lt;row&gt;</c> interessati. È stato verificato sui tre file reali che i fogli
    /// principali <b>non contengono formule, formattazione condizionale, convalide dati né tabelle</b>,
    /// e che merge e autofiltro coprono solo le righe 1-2: cancellare una riga dati e rinumerare le
    /// successive non invalida quindi alcun intervallo.
    /// </para>
    ///
    /// <para>
    /// <b>Lo stile della riga archiviata è copiato dal foglio di destinazione</b>, non impostato a un
    /// colore fisso. La specifica indicava un grigio <c>#A6A6A6</c>, ma i file reali lo smentiscono:
    /// lo storico di ETR1000 archivia su sfondo <b>giallo</b>, e quelli di ETR500 ed ETR700 usano
    /// colori di tema, non un RGB letterale. Ereditare lo stile dalle righe già presenti riproduce
    /// l'aspetto corretto in tutti e tre i casi e continuerà a farlo se un domani cambierà.
    /// </para>
    /// </summary>
    public static class VerificheArchivioService
    {
        /// <summary>
        /// Colonne copiate per posizione dal foglio principale a quello storico.
        /// <para>
        /// Verificato sui tre file reali: TRENO, LOCO, descrizione dell'avaria e data comunicazione
        /// occupano le colonne A-D <b>in tutti e sei i fogli</b> (principale e storico di ogni
        /// flotta), nonostante le intestazioni non coincidano — nello storico ETR700 la colonna C si
        /// chiama "note da tenere presente" mentre nel principale è "Avaria segnalata da ING/SVI".
        /// Un abbinamento per testo dell'intestazione fallirebbe proprio lì; la posizione no.
        /// </para>
        /// </summary>
        private const int UltimaColonnaPosizionale = 4;

        /// <summary>
        /// Esegue l'archiviazione. Da chiamare su thread di background: fa I/O su cartelle
        /// sincronizzate OneDrive e riscrive un pacchetto OpenXML.
        /// </summary>
        public static ArchiviazioneEsito Archivia(
            VerificheModel riga,
            VerifichePercorsiRisolti percorsi,
            string cognome,
            DateTime momento)
        {
            ArgumentNullException.ThrowIfNull(riga);
            ArgumentNullException.ThrowIfNull(percorsi);

            if (!riga.PuoEssereArchiviata)
            {
                return new ArchiviazioneEsito(false,
                    "Questa riga non può essere archiviata: non è stato possibile risalire alla riga " +
                    "di origine nel file Excel. Riaprire il modulo VERIFICHE e riprovare.");
            }

            string filePrincipale = riga.SourceFilePath;
            if (!File.Exists(filePrincipale))
            {
                return new ArchiviazioneEsito(false,
                    $"Il file delle verifiche non esiste più:\n{filePrincipale}\n\n" +
                    "Potrebbe essere stato rinominato o spostato da un altro tecnico. Aggiornare l'elenco e riprovare.");
            }

            // 1) Copia di sicurezza PRIMA di qualunque modifica: se il passo successivo fallisce a
            //    metà, la versione integra del file è già al sicuro nella cartella OLD.
            string? percorsoBackup;
            try
            {
                percorsoBackup = CreaBackup(filePrincipale, percorsi.CartellaOld);
            }
            catch (Exception ex)
            {
                return new ArchiviazioneEsito(false,
                    $"Impossibile creare la copia di sicurezza nella cartella di archivio:\n{ex.Message}\n\n" +
                    "Nessuna modifica è stata apportata al file.");
            }

            // 2) Modifica del workbook.
            try
            {
                var esito = AggiornaWorkbook(filePrincipale, riga, momento);
                if (!esito.Riuscita) return esito with { PercorsoBackup = percorsoBackup };
            }
            catch (IOException ex)
            {
                return new ArchiviazioneEsito(false, MessaggioFileBloccato(filePrincipale, ex), null, percorsoBackup);
            }
            catch (Exception ex)
            {
                return new ArchiviazioneEsito(false,
                    $"Errore durante l'aggiornamento del file Excel:\n{ex.Message}\n\n" +
                    $"Una copia integra del file, precedente alla modifica, si trova in:\n{percorsoBackup}",
                    null, percorsoBackup);
            }

            // 3) Rinomina con data, ora e cognome.
            try
            {
                string nuovoNome = VerificheArchivioNaming.ComponiNomeFile(percorsi.PrefissoFile, momento, cognome);
                string nuovoPercorso = Path.Combine(Path.GetDirectoryName(filePrincipale)!, nuovoNome);

                if (!string.Equals(nuovoPercorso, filePrincipale, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(nuovoPercorso)) File.Delete(nuovoPercorso);
                    FileOperationRetry.Execute(() => File.Move(filePrincipale, nuovoPercorso));
                }

                return new ArchiviazioneEsito(true,
                    $"Verifica archiviata.\n\nFile aggiornato:\n{Path.GetFileName(nuovoPercorso)}",
                    nuovoPercorso, percorsoBackup);
            }
            catch (Exception ex)
            {
                // Il contenuto è già stato aggiornato: la mancata rinomina non perde dati.
                return new ArchiviazioneEsito(true,
                    $"La verifica è stata archiviata, ma non è stato possibile rinominare il file:\n{ex.Message}",
                    filePrincipale, percorsoBackup);
            }
        }

        // ------------------------------------------------------------------
        // Backup
        // ------------------------------------------------------------------

        private static string? CreaBackup(string filePrincipale, string? cartellaOld)
        {
            if (string.IsNullOrWhiteSpace(cartellaOld)) return null;

            Directory.CreateDirectory(cartellaOld);
            string destinazione = Path.Combine(cartellaOld, Path.GetFileName(filePrincipale));

            // Un file con lo stesso nome esiste già (due archiviazioni nello stesso minuto, o un
            // backup precedente mai rinominato): si aggiunge un suffisso invece di sovrascrivere una
            // copia di sicurezza, che è l'unica rete presente in questa operazione.
            if (File.Exists(destinazione))
            {
                string senzaEstensione = Path.GetFileNameWithoutExtension(destinazione);
                string estensione = Path.GetExtension(destinazione);
                int progressivo = 2;
                do
                {
                    destinazione = Path.Combine(cartellaOld, $"{senzaEstensione} ({progressivo}){estensione}");
                    progressivo++;
                } while (File.Exists(destinazione));
            }

            FileOperationRetry.Execute(() => File.Copy(filePrincipale, destinazione, overwrite: false));
            return destinazione;
        }

        // ------------------------------------------------------------------
        // Modifica del workbook
        // ------------------------------------------------------------------

        /// <summary>
        /// Aggiorna il workbook in <b>due passaggi separati</b>: prima una validazione in sola
        /// lettura, poi — solo se tutto torna — l'apertura in scrittura.
        ///
        /// <para>
        /// <b>Non è una raffinatezza: è ciò che rende vera la frase "nessuna modifica è stata
        /// apportata".</b> Aprire un <c>SpreadsheetDocument</c> con <c>isEditable: true</c> tocca il
        /// file anche quando poi non si scrive nulla — verificato da un test che confrontava la data
        /// di ultima modifica dopo un rifiuto, e che falliva. Un tecnico che riceve un messaggio di
        /// errore deve poter contare sul fatto che il file sia rimasto quello di prima.
        /// </para>
        /// </summary>
        private static ArchiviazioneEsito AggiornaWorkbook(string percorso, VerificheModel riga, DateTime momento)
        {
            var (esitoValidazione, nomeStorico) = ValidaInSolaLettura(percorso, riga, momento);
            if (!esitoValidazione.Riuscita) return esitoValidazione;

            return ApplicaModifica(percorso, riga, momento, nomeStorico!);
        }

        /// <summary>
        /// Controlla, senza aprire il file in scrittura, che esista il foglio storico dell'anno e che
        /// la riga da archiviare sia ancora quella vista a schermo.
        /// </summary>
        private static (ArchiviazioneEsito Esito, string? NomeStorico) ValidaInSolaLettura(
            string percorso, VerificheModel riga, DateTime momento)
        {
            // FileShare.Delete oltre a ReadWrite, come in VerificheExcelReader: i file stanno in
            // cartelle sincronizzate, dove OneDrive può sostituirli mentre li leggiamo.
            using var stream = new FileStream(percorso, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var document = SpreadsheetDocument.Open(stream, isEditable: false);

            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Il file non contiene un workbook valido.");

            var fogli = workbookPart.Workbook.Sheets?.Elements<Sheet>().ToList() ?? [];
            if (fogli.Count == 0)
                return (new ArchiviazioneEsito(false, "Il file non contiene alcun foglio."), null);

            string? nomeStorico = VerificheArchivioNaming.TrovaFoglioStorico(
                fogli.Select(f => f.Name?.Value ?? string.Empty), momento.Year);

            if (nomeStorico == null)
            {
                return (new ArchiviazioneEsito(false,
                    $"Nel file non è stato trovato un foglio storico per l'anno {momento.Year}.\n\n" +
                    $"Fogli presenti: {string.Join(", ", fogli.Select(f => f.Name?.Value))}.\n\n" +
                    "Creare il foglio storico dell'anno corrente in Excel e riprovare. " +
                    "Nessuna modifica è stata apportata."), null);
            }

            var partPrincipale = (WorksheetPart)workbookPart.GetPartById(fogli[0].Id!.Value!);
            var datiPrincipale = partPrincipale.Worksheet.GetFirstChild<SheetData>();
            var stringhe = workbookPart.SharedStringTablePart?.SharedStringTable;

            var rigaOrigine = datiPrincipale?.Elements<Row>()
                .FirstOrDefault(r => r.RowIndex?.Value == (uint)riga.SourceRowNumber);

            if (rigaOrigine == null)
            {
                return (new ArchiviazioneEsito(false,
                    $"La riga {riga.SourceRowNumber} non esiste più nel foglio principale.\n\n" +
                    "Il file è stato modificato da un'altra postazione. Aggiornare l'elenco e riprovare. " +
                    "Nessuna modifica è stata apportata."), null);
            }

            string trenoNelFile = LeggiTesto(rigaOrigine, 1, stringhe);
            string locoNelFile = LeggiTesto(rigaOrigine, 2, stringhe);

            // Si confrontano i valori GREZZI del foglio, non quelli mostrati a video: per la flotta
            // 1000 il ViewModel sostituisce "ETR1000" con il numero di treno risolto dal database
            // tramite la loco, e confrontare quello con il contenuto della cella faceva fallire ogni
            // archiviazione ETR1000 con un falso "la riga non corrisponde piu" (segnalato dal committente).
            if (!Corrisponde(trenoNelFile, riga.SourceTreno) || !Corrisponde(locoNelFile, riga.SourceLoco))
            {
                return (new ArchiviazioneEsito(false,
                    $"La riga {riga.SourceRowNumber} non corrisponde più alla verifica selezionata.\n\n" +
                    $"Atteso: treno '{riga.SourceTreno}', loco '{riga.SourceLoco}'.\n" +
                    $"Trovato: treno '{trenoNelFile}', loco '{locoNelFile}'.\n\n" +
                    "Il file è stato modificato da un'altra postazione. Aggiornare l'elenco e riprovare. " +
                    "Nessuna modifica è stata apportata."), null);
            }

            return (new ArchiviazioneEsito(true, "Validazione superata."), nomeStorico);
        }

        private static ArchiviazioneEsito ApplicaModifica(
            string percorso, VerificheModel riga, DateTime momento, string nomeStorico)
        {
            using var document = SpreadsheetDocument.Open(percorso, isEditable: true);
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidOperationException("Il file non contiene un workbook valido.");

            var fogli = workbookPart.Workbook.Sheets!.Elements<Sheet>().ToList();

            // Foglio principale = il primo, la stessa convenzione con cui VerificheExcelReader legge
            // i dati mostrati a video. I nomi non aiuterebbero: "VERIFICHE ETR500" su una flotta,
            // "verifiche" sulle altre due.
            var partPrincipale = (WorksheetPart)workbookPart.GetPartById(fogli[0].Id!.Value!);
            var datiPrincipale = partPrincipale.Worksheet.GetFirstChild<SheetData>()!;

            var sheetStorico = fogli.First(f => f.Name?.Value == nomeStorico);
            var partStorico = (WorksheetPart)workbookPart.GetPartById(sheetStorico.Id!.Value!);
            var datiStorico = partStorico.Worksheet.GetFirstChild<SheetData>()
                ?? throw new InvalidOperationException($"Il foglio '{nomeStorico}' non contiene dati.");

            var stringhe = workbookPart.SharedStringTablePart?.SharedStringTable;

            // Esistenza e corrispondenza della riga sono già state verificate in sola lettura.
            var rigaOrigine = datiPrincipale.Elements<Row>()
                .First(r => r.RowIndex?.Value == (uint)riga.SourceRowNumber);

            // --- Archiviazione nel foglio storico ---
            int colonnaDataChiusura = TrovaColonnaDataChiusura(datiStorico, stringhe);
            AppendiRigaStorico(datiStorico, rigaOrigine, colonnaDataChiusura, momento, stringhe);

            // --- Rimozione dal foglio principale, con shift verso l'alto ---
            RimuoviRigaEShifta(datiPrincipale, (uint)riga.SourceRowNumber);

            partStorico.Worksheet.Save();
            partPrincipale.Worksheet.Save();
            workbookPart.Workbook.Save();

            return new ArchiviazioneEsito(true, $"Riga archiviata nel foglio '{nomeStorico}'.");
        }

        /// <summary>
        /// Confronto esatto (a meno di spazi ai bordi e maiuscole) fra il valore attualmente nella
        /// cella e quello letto quando la riga è comparsa a video.
        ///
        /// <para>
        /// Era tollerante — accettava anche una corrispondenza per suffisso — finché il confronto
        /// avveniva contro i valori normalizzati per la UI. Ora che si confrontano i valori grezzi del
        /// foglio con sé stessi quella tolleranza è solo un rischio: <c>"1"</c> risulterebbe uguale a
        /// <c>"31"</c>, e la guardia lascerebbe passare proprio la riga sbagliata che deve fermare.
        /// </para>
        /// </summary>
        private static bool Corrisponde(string nelFile, string? atteso) =>
            (nelFile ?? string.Empty).Trim()
                .Equals((atteso ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Colonna in cui va scritta la data di chiusura nel foglio storico.
        ///
        /// <para>
        /// Cercata per intestazione, perché il nome cambia da flotta a flotta: "Data verifica" su
        /// ETR700, "Data chiusura Verifica" su ETR1000. Lo storico di ETR500 <b>non ha intestazioni</b>
        /// — le sue righe partono direttamente da riga 1 — e lì si ricade sulla colonna successiva a
        /// quella della data comunicazione, cioè la E, che è quella effettivamente usata nel file.
        /// </para>
        /// </summary>
        private static int TrovaColonnaDataChiusura(SheetData datiStorico, SharedStringTable? stringhe)
        {
            foreach (var row in datiStorico.Elements<Row>().Take(10))
            {
                foreach (var cell in row.Elements<Cell>())
                {
                    string testo = LeggiTesto(cell, stringhe).Trim();
                    if (testo.Length == 0) continue;

                    bool eData = testo.Contains("DATA", StringComparison.OrdinalIgnoreCase);
                    bool eChiusura = testo.Contains("VERIFICA", StringComparison.OrdinalIgnoreCase) ||
                                     testo.Contains("CHIUSURA", StringComparison.OrdinalIgnoreCase);

                    if (eData && eChiusura) return NumeroColonna(cell.CellReference?.Value);
                }
            }

            // Nessuna intestazione: subito dopo le colonne copiate per posizione.
            return UltimaColonnaPosizionale + 1;
        }

        private static void AppendiRigaStorico(
            SheetData datiStorico, Row rigaOrigine, int colonnaDataChiusura, DateTime momento, SharedStringTable? stringhe)
        {
            // "Prima riga libera" = dopo l'ultima riga che contiene dati, non il primo buco. Nello
            // storico ETR700 le righe 3-14 sono vuote ma già formattate e i record veri partono dalla
            // 15: riempire i buchi metterebbe l'archiviazione di oggi sopra quelle del 2026.
            uint ultimaConDati = 0;
            foreach (var r in datiStorico.Elements<Row>())
            {
                if (r.RowIndex?.Value is not uint idx) continue;
                if (r.Elements<Cell>().Any(c => LeggiTesto(c, stringhe).Trim().Length > 0))
                {
                    if (idx > ultimaConDati) ultimaConDati = idx;
                }
            }

            uint nuovoIndice = ultimaConDati + 1;
            var rigaModello = datiStorico.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == ultimaConDati);
            var rigaEsistente = datiStorico.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == nuovoIndice);

            Row destinazione = rigaEsistente ?? CreaRigaOrdinata(datiStorico, nuovoIndice, rigaModello);

            // 1) Prima si veste l'INTERA riga come l'ultima archiviata, poi si scrivono i valori.
            //
            // L'ordine conta e l'ampiezza pure. Una prima versione dava uno stile solo alle colonne
            // che scriveva (A-D più la data): le colonne intermedie — "Data richiesta", "Tecnico",
            // "NOTE" — non venivano nemmeno create, e nel foglio la riga appariva bianca e senza
            // bordi da E in poi, mentre quella sopra era grigia e bordata (segnalato dal committente
            // con uno screenshot dello storico ETR1000). Qui si scorrono **tutte** le celle della
            // riga modello, comprese quelle vuote, così sfondo, bordi, font e allineamento
            // proseguono senza interruzioni per l'intera larghezza della tabella.
            var rigaStile = rigaModello ?? rigaEsistente;
            if (rigaStile != null)
            {
                if (rigaStile.Height != null) { destinazione.Height = rigaStile.Height; destinazione.CustomHeight = true; }
                if (rigaStile.StyleIndex != null) { destinazione.StyleIndex = rigaStile.StyleIndex; destinazione.CustomFormat = true; }

                foreach (var cellaModello in rigaStile.Elements<Cell>())
                {
                    int colonna = NumeroColonna(cellaModello.CellReference?.Value);
                    if (colonna <= 0 || cellaModello.StyleIndex == null) continue;

                    OttieniOCreaCella(destinazione, colonna).StyleIndex = cellaModello.StyleIndex.Value;
                }
            }

            // 2) Colonne A-D: copia posizionale del solo valore. Lo stile è già quello del foglio di
            //    destinazione, impostato sopra: CopiaValore tocca soltanto tipo e contenuto.
            for (int colonna = 1; colonna <= UltimaColonnaPosizionale; colonna++)
            {
                var sorgente = TrovaCella(rigaOrigine, colonna);
                if (sorgente == null) continue;

                var cella = OttieniOCreaCella(destinazione, colonna);
                CopiaValore(sorgente, cella);
                ApplicaStileSeMancante(cella, rigaStile, colonna);
            }

            // 3) Data di chiusura = oggi, come numero: il formato data arriva dallo stile ereditato
            //    dalle righe già archiviate ("dd/mm/yy" su ETR1000, "[$-410]d-mmm-yy" su ETR500).
            //    Prenderlo invece dalla riga vuota preesistente faceva comparire il seriale grezzo
            //    "46258" al posto di "24/08/2026".
            var cellaData = OttieniOCreaCella(destinazione, colonnaDataChiusura);
            cellaData.CellValue = new CellValue(ASerialeExcel(momento).ToString(CultureInfo.InvariantCulture));
            cellaData.DataType = null;
            ApplicaStileSeMancante(cellaData, rigaStile, colonnaDataChiusura);

            // NOTA: la colonna "Tecnico" non viene compilata, per scelta esplicita del committente.
            // Il cognome resta solo nel nome del file.
        }

        /// <summary>
        /// Rete di sicurezza per le colonne che la riga modello non possiede: applica lo stile della
        /// cella corrispondente solo se la destinazione non ne ha già uno.
        /// </summary>
        private static void ApplicaStileSeMancante(Cell cella, Row? rigaStile, int colonna)
        {
            if (cella.StyleIndex != null || rigaStile == null) return;

            var riferimento = TrovaCella(rigaStile, colonna);
            if (riferimento?.StyleIndex != null) cella.StyleIndex = riferimento.StyleIndex.Value;
        }

        private static Row CreaRigaOrdinata(SheetData dati, uint indice, Row? modello)
        {
            var riga = new Row { RowIndex = indice };
            if (modello?.Height != null) { riga.Height = modello.Height; riga.CustomHeight = true; }
            if (modello?.StyleIndex != null) { riga.StyleIndex = modello.StyleIndex; riga.CustomFormat = true; }

            var successiva = dati.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value > indice);
            if (successiva != null) dati.InsertBefore(riga, successiva);
            else dati.AppendChild(riga);

            return riga;
        }

        /// <summary>
        /// Rimuove la riga indicata e sposta in alto di una posizione tutte le successive,
        /// aggiornando sia <c>r</c> della riga sia i riferimenti di cella (<c>A15</c> → <c>A14</c>).
        /// Senza l'aggiornamento dei riferimenti Excel considera il file corrotto.
        /// </summary>
        private static void RimuoviRigaEShifta(SheetData dati, uint indiceDaRimuovere)
        {
            var daRimuovere = dati.Elements<Row>().FirstOrDefault(r => r.RowIndex?.Value == indiceDaRimuovere);
            daRimuovere?.Remove();

            foreach (var riga in dati.Elements<Row>().Where(r => r.RowIndex?.Value > indiceDaRimuovere).ToList())
            {
                uint nuovo = riga.RowIndex!.Value - 1;
                riga.RowIndex = nuovo;

                foreach (var cella in riga.Elements<Cell>())
                {
                    string? riferimento = cella.CellReference?.Value;
                    if (string.IsNullOrEmpty(riferimento)) continue;

                    string lettere = new(riferimento.TakeWhile(char.IsLetter).ToArray());
                    cella.CellReference = lettere + nuovo.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        // ------------------------------------------------------------------
        // Utilità su celle
        // ------------------------------------------------------------------

        private static void CopiaValore(Cell sorgente, Cell destinazione)
        {
            destinazione.DataType = sorgente.DataType?.Value;
            destinazione.CellValue = sorgente.CellValue != null ? new CellValue(sorgente.CellValue.Text) : null;
            if (sorgente.InlineString != null) destinazione.InlineString = (InlineString)sorgente.InlineString.CloneNode(true);
        }

        private static Cell? TrovaCella(Row riga, int colonna) =>
            riga.Elements<Cell>().FirstOrDefault(c => NumeroColonna(c.CellReference?.Value) == colonna);

        private static Cell OttieniOCreaCella(Row riga, int colonna)
        {
            var esistente = TrovaCella(riga, colonna);
            if (esistente != null) return esistente;

            string riferimento = LettereColonna(colonna) + riga.RowIndex!.Value.ToString(CultureInfo.InvariantCulture);
            var nuova = new Cell { CellReference = riferimento };

            var successiva = riga.Elements<Cell>().FirstOrDefault(c => NumeroColonna(c.CellReference?.Value) > colonna);
            if (successiva != null) riga.InsertBefore(nuova, successiva);
            else riga.AppendChild(nuova);

            return nuova;
        }

        private static string LeggiTesto(Row riga, int colonna, SharedStringTable? stringhe)
        {
            var cella = TrovaCella(riga, colonna);
            return cella == null ? string.Empty : LeggiTesto(cella, stringhe);
        }

        private static string LeggiTesto(Cell cella, SharedStringTable? stringhe)
        {
            if (cella.DataType?.Value == CellValues.SharedString)
            {
                if (stringhe == null || cella.CellValue == null) return string.Empty;
                if (!int.TryParse(cella.CellValue.Text, out int indice)) return string.Empty;
                var item = stringhe.Elements<SharedStringItem>().ElementAtOrDefault(indice);
                return item?.InnerText ?? string.Empty;
            }

            if (cella.DataType?.Value == CellValues.InlineString) return cella.InlineString?.InnerText ?? string.Empty;
            return cella.CellValue?.Text ?? string.Empty;
        }

        /// <summary>Data → seriale Excel. L'epoca è il 30/12/1899 per il noto bug dell'anno bisestile 1900.</summary>
        internal static int ASerialeExcel(DateTime data) => (int)(data.Date - new DateTime(1899, 12, 30)).TotalDays;

        internal static int NumeroColonna(string? riferimentoCella)
        {
            if (string.IsNullOrEmpty(riferimentoCella)) return 0;

            int numero = 0;
            foreach (char c in riferimentoCella)
            {
                if (!char.IsLetter(c)) break;
                numero = (numero * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
            }
            return numero;
        }

        internal static string LettereColonna(int colonna)
        {
            string risultato = string.Empty;
            while (colonna > 0)
            {
                int resto = (colonna - 1) % 26;
                risultato = (char)('A' + resto) + risultato;
                colonna = (colonna - 1) / 26;
            }
            return risultato;
        }

        private static string MessaggioFileBloccato(string percorso, IOException ex) =>
            $"Il file delle verifiche è bloccato da un altro processo e non può essere aggiornato:\n" +
            $"{Path.GetFileName(percorso)}\n\n" +
            "Cause più probabili: il file è aperto in Excel su questa o su un'altra postazione, " +
            "oppure OneDrive lo sta sincronizzando in questo momento.\n\n" +
            $"Chiudere il file e riprovare.\n\nDettaglio: {ex.Message}";
    }
}
