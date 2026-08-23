using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PersonalAutomationTool.Core.Naming;

namespace PersonalAutomationTool.Modules.Excel
{
    /// <summary>Ticket e locomotore estratti dal nome di una sottocartella LOG/DUMP.</summary>
    public sealed record TicketLoco(string Ticket, string Loco);

    /// <summary>
    /// Logica di estrazione ticket/loco dai nomi di sottocartella per il modulo EXCEL, estratta da
    /// <c>ExcelViewModel.AutoFillReportFieldsAsync</c> (Sprint 3, §6.1-quater — stesso principio
    /// dell'intervento 2.1 dello Sprint 2 per <c>PdfRenamePlanner</c>: separare la decisione dal
    /// guscio WPF, così da poterla testare senza avviare un'applicazione). Nessuna dipendenza da WPF.
    ///
    /// <para>
    /// <b>Il problema che questa classe risolve.</b> Il codice originale costruiva il pattern di
    /// estrazione del locomotore interpolando direttamente <c>SelectedTrain</c>, cioè l'<b>etichetta
    /// della ComboBox</b>, come token letterale:
    /// <code>new Regex($@"{SelectedTrain}\s*[-_]?\s*(\d{{3,4}})")</code>
    /// Due delle quattro etichette non compaiono mai come tali sui nomi di cartella reali, quindi il
    /// pattern non poteva trovare corrispondenza e l'estrazione ricadeva in silenzio sui fallback
    /// generici (<c>\b\d{7,8}\b</c> per il ticket, <c>\b\d{2,4}\b</c> per la loco), che prendono il
    /// primo numero della lunghezza giusta ovunque si trovi — anche dentro la versione software o la
    /// data:
    /// <list type="bullet">
    /// <item><c>"ETR1000 / 1000FH"</c> — nessuna cartella contiene la stringa con lo slash.</item>
    /// <item><c>"ETR1000 I-F"</c> — su disco la forma è <c>ETR1000IF</c>, attaccata e senza trattino
    /// (verificato sui nomi reali forniti dal committente). Anche questa etichetta cadeva quindi sul
    /// fallback: il difetto era più ampio di quanto documentato nella scoperta #1 dello Sprint 2,
    /// che citava solo la prima.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>La correzione.</b> L'estrazione non parte più dall'etichetta UI: usa
    /// <see cref="LogDumpFolderName.TryParse"/> — lo stesso parser condiviso già adottato da
    /// <c>PdfView</c> (intervento 1.1) — che localizza i campi per <i>posizione</i> nella grammatica
    /// <c>SR{ticket} {LOG|DUMP} {tipo} {loco} {software} {ddMMyy} {utente}</c> e non ha quindi
    /// bisogno di sapere quale voce di ComboBox è selezionata. L'etichetta resta usata soltanto per
    /// il percorso di riserva (<see cref="BuildLocoRegex"/>), per i nomi che non rispettano la
    /// grammatica, ma tradotta prima nei token che compaiono davvero su disco.
    /// </para>
    /// </summary>
    public static class ExcelFolderParser
    {
        /// <summary>
        /// Traduce un'etichetta della ComboBox di EXCEL nei token che compaiono realmente nei nomi
        /// di cartella, ordinati dal più lungo al più corto (necessario perché l'alternanza di un
        /// regex sceglie la prima alternativa che corrisponde, non la più lunga).
        ///
        /// <para>
        /// <b><c>ETR1000</c>, <c>ETR1000FH</c> ed <c>ETR1000IF</c> sono tre treni distinti</b> (il
        /// resto dell'applicazione li tratta già come tali: tre viste EMAIL separate, tre chiavi in
        /// <c>destinatari.json</c>, tre cartelle Hitachi in <c>VerificheViewModel</c>, tipi distinti
        /// in <c>flotte</c>). Nel <b>solo modulo EXCEL</b>, ETR1000 e la variante FH <b>condividono
        /// il Report Interventi</b> — stessa voce di ComboBox, stessa cartella Hitachi, stesse
        /// opzioni del form — mentre la I-F ne ha uno proprio, con un numero di colonne diverso
        /// (<c>maxCol</c> 24 contro 27, §5.4) e una cartella Hitachi propria. Da qui il
        /// raggruppamento nei token: riflette la condivisione del <i>report</i>, non un'identità fra
        /// i treni. La I-F resta esclusa dalla prima etichetta, in coerenza con
        /// <c>ExcelViewModel.MatchesTrain</c> e con l'invariante §5.3: confonderle farebbe scrivere
        /// righe nel report sbagliato.
        /// </para>
        ///
        /// <para>
        /// <b>Dove la distinzione conta comunque:</b> condividere il report non significa essere lo
        /// stesso rotabile. Il campo "ROTABILE" va compilato con il treno reale della cartella —
        /// vedi <see cref="ResolveActualTrainType"/> e <see cref="SelectRotabileOption"/>.
        /// </para>
        /// </summary>
        public static IReadOnlyList<string> GetDiskTokens(string? uiLabel)
        {
            if (string.IsNullOrWhiteSpace(uiLabel)) return [];

            string[] tokens = uiLabel switch
            {
                // Le forme Italia-Francia sono escluse: appartengono all'etichetta "ETR1000 I-F".
                // "ETR1001FH" è il valore reale della colonna `tipo` in `flotte` (non "ETR1000FH":
                // verificato sul database, vedi §6.1 dello Sprint 1).
                "ETR1000 / 1000FH" => ["ETR1001FH", "ETR1000FH", "ETR1001", "ETR1000", "1000FH"],

                // "ETR1000IF" (attaccato) è la forma osservata sui nomi di cartella reali;
                // "ETR1000 I-F" è la forma usata dalla colonna `tipo` di `flotte`. Entrambe servono.
                "ETR1000 I-F" => ["ETR1000 I-F", "ETR1000IF", "ETR1000 IF", "1000IF"],

                // MatchesTrain accetta anche "ETR500" per questa flotta: stessa equivalenza qui.
                "E404P" => ["E404P", "ETR500"],

                _ => [uiLabel]
            };

            return [.. tokens.OrderByDescending(t => t.Length)];
        }

        /// <summary>
        /// Costruisce il pattern di riserva "token del treno seguito dal numero di locomotore".
        /// Ogni token è passato per <see cref="Regex.Escape"/>: l'etichetta di default
        /// <c>"ETR1000 / 1000FH"</c> veniva prima interpolata grezza in un pattern, il che la
        /// rendeva dipendente dal fatto che non contenesse metacaratteri.
        /// </summary>
        /// <param name="uiLabel">L'etichetta selezionata nella ComboBox.</param>
        /// <param name="minDigits">
        /// Cifre minime del numero di locomotore: 3 nel ciclo sulle sottocartelle, 2 nella ricerca
        /// del campo "SN" — i due chiamanti originali usavano soglie diverse e la differenza è
        /// preservata invece di essere uniformata.
        /// </param>
        /// <returns><see langword="null"/> se non ci sono token utilizzabili, così il chiamante salta direttamente al fallback generico.</returns>
        public static Regex? BuildLocoRegex(string? uiLabel, int minDigits)
        {
            var tokens = GetDiskTokens(uiLabel);
            if (tokens.Count == 0) return null;

            string alternation = string.Join("|", tokens.Select(Regex.Escape));
            return new Regex($@"(?:{alternation})\s*[-_]?\s*(\d{{{minDigits},4}})", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Estrae ticket e locomotore da un nome di sottocartella tramite
        /// <see cref="LogDumpFolderName.TryParse"/>, indipendentemente dall'etichetta UI selezionata.
        /// Restituisce <see langword="null"/> se il nome non rispetta la grammatica LOG/DUMP oppure
        /// se i campi estratti non hanno la forma attesa: in quel caso il chiamante prosegue con la
        /// logica a regex preesistente, che resta invariata.
        ///
        /// <para>
        /// <b>Le due guardie sulla forma non sono ridondanti.</b> Il parser condiviso è più
        /// permissivo dei regex che sostituisce (accetta un ticket <c>\S+</c> e una loco che è
        /// semplicemente il token successivo al tipo), mentre l'estrazione originale accettava solo
        /// un ticket di sole cifre e una loco di 2-4 cifre. Senza queste guardie, un nome anomalo
        /// produrrebbe ora un valore che prima veniva scartato — cioè un cambiamento di
        /// comportamento silenzioso su dati imprevisti, esattamente ciò che questo intervento vuole
        /// eliminare. Con le guardie, il risultato è identico a prima ovunque il vecchio codice
        /// funzionasse, e corretto dove prima cadeva sul fallback debole.
        /// </para>
        /// </summary>
        /// <param name="subFolderName">Nome della sottocartella (non il percorso completo).</param>
        /// <param name="knownTypes">
        /// I <c>tipo</c> noti da <c>flotte</c>, ordinati per lunghezza decrescente
        /// (<c>FlotteCache.GetDistinctTipiOrderByLengthDesc</c>). Un tipo assente dall'elenco — come
        /// <c>ETR1000IF</c>, che sul disco compare attaccato mentre in <c>flotte</c> è registrato
        /// come <c>"ETR1000 I-F"</c> — viene comunque gestito: <see cref="LogDumpFolderName.TryParse"/>
        /// ricade sul primo token, che in quel caso è esattamente il tipo cercato.
        /// </param>
        public static TicketLoco? TryExtractTicketAndLoco(string? subFolderName, IReadOnlyList<string> knownTypes)
        {
            if (!LogDumpFolderName.TryParse(subFolderName, knownTypes, out var parsed) || parsed == null)
            {
                return null;
            }

            if (!IsAllDigits(parsed.Ticket)) return null;
            if (!IsAllDigits(parsed.Loco) || parsed.Loco.Length is < 2 or > 4) return null;

            return new TicketLoco(parsed.Ticket, parsed.Loco);
        }

        private static bool IsAllDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
            {
                if (!char.IsAsciiDigit(c)) return false;
            }
            return true;
        }

        /// <summary>
        /// Il <c>tipo</c> realmente scritto nei nomi delle sottocartelle LOG/DUMP, ricavato con
        /// <see cref="LogDumpFolderName.TryParse"/>. Restituisce <see langword="null"/> se nessuna
        /// sottocartella è analizzabile.
        ///
        /// <para>
        /// Serve perché <b>l'etichetta della ComboBox non identifica il treno</b>: la voce
        /// <c>"ETR1000 / 1000FH"</c> copre due flotte distinte (<c>ETR1000</c> ed <c>ETR1001FH</c>)
        /// che in EXCEL condividono lo stesso Report Interventi — condividere il report non significa
        /// però essere lo stesso rotabile, e il campo "ROTABILE" del report deve riportare il treno
        /// reale. Vedi <see cref="SelectRotabileOption"/>.
        /// </para>
        ///
        /// <para>
        /// In presenza di sottocartelle di tipi diversi nella stessa cartella madre (caso anomalo:
        /// una cartella madre corrisponde a un solo treno) vince il tipo più frequente, e a parità di
        /// frequenza il primo incontrato — deterministico, mai un'eccezione.
        /// </para>
        /// </summary>
        public static string? ResolveActualTrainType(IEnumerable<string> subFolderNames, IReadOnlyList<string> knownTypes)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (var name in subFolderNames)
            {
                if (!LogDumpFolderName.TryParse(name, knownTypes, out var parsed) || parsed == null) continue;
                if (string.IsNullOrWhiteSpace(parsed.Tipo)) continue;

                if (counts.TryGetValue(parsed.Tipo, out int n)) counts[parsed.Tipo] = n + 1;
                else { counts[parsed.Tipo] = 1; order.Add(parsed.Tipo); }
            }

            if (order.Count == 0) return null;
            return order.OrderByDescending(t => counts[t]).First();
        }

        /// <summary>Vero se il tipo indica la variante FH (<c>ETR1001FH</c> in <c>flotte</c>, <c>1000FH</c>/<c>ETR1000FH</c> nei nomi di cartella).</summary>
        public static bool IsFhType(string? tipo) =>
            !string.IsNullOrEmpty(tipo) && tipo.Contains("FH", StringComparison.OrdinalIgnoreCase);

        /// <summary>Vero se il tipo indica la variante Italia-Francia, in una qualsiasi delle forme in cui compare nell'applicazione.</summary>
        public static bool IsItaliaFranciaType(string? tipo)
        {
            if (string.IsNullOrEmpty(tipo)) return false;
            return tipo.Contains("1000IF", StringComparison.OrdinalIgnoreCase)
                || tipo.Contains("I-F", StringComparison.OrdinalIgnoreCase)
                || tipo.Contains("ITA", StringComparison.OrdinalIgnoreCase)
                || tipo.Contains("Italia", StringComparison.OrdinalIgnoreCase)
                || tipo.Contains("Francia", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sceglie l'opzione del menu a tendina "ROTABILE" corrispondente al <b>treno reale</b>
        /// <paramref name="actualType"/> anziché all'etichetta della ComboBox.
        ///
        /// <para>
        /// <b>Perché serve.</b> Il codice originale sceglieva l'opzione in base a
        /// <c>SelectedTrain</c>: sotto la voce <c>"ETR1000 / 1000FH"</c> cercava sempre un'opzione
        /// contenente "ETR1000", quindi una cartella <c>ETR1001FH</c> otteneva il rotabile
        /// <c>ETR1000</c> — un valore sbagliato scritto nel Report Interventi ufficiale, in silenzio.
        /// Simmetricamente, un'opzione "ETR 1000 FH" poteva essere scelta per una cartella ETR1000
        /// pura, perché contiene comunque la sottostringa "ETR1000".
        /// </para>
        ///
        /// <para>
        /// <b>Non regredisce mai.</b> Restituisce <see langword="null"/> quando il foglio non offre
        /// un'opzione distinta per la variante (per esempio un report che elenca solo "ETR 1000"):
        /// in quel caso il chiamante prosegue con la selezione preesistente, quindi il
        /// comportamento resta identico a oggi ovunque il foglio non permetta di fare meglio.
        /// </para>
        /// </summary>
        /// <param name="options">Le opzioni della convalida dati della colonna ROTABILE.</param>
        /// <param name="actualType">Il tipo reale, da <see cref="ResolveActualTrainType"/>.</param>
        public static string? SelectRotabileOption(IEnumerable<string>? options, string? actualType)
        {
            if (options == null || string.IsNullOrEmpty(actualType)) return null;

            var list = options.Where(o => !string.IsNullOrWhiteSpace(o)).ToList();
            if (list.Count == 0) return null;

            bool wantFh = IsFhType(actualType);
            bool wantIf = IsItaliaFranciaType(actualType);

            // Una variante si riconosce dai propri marcatori; il tipo "base" è quello che non ne ha
            // nessuno. Confrontare così evita che "ETR1000" catturi per sottostringa "ETR 1000 FH".
            static bool OptionIsFh(string o) => o.Contains("FH", StringComparison.OrdinalIgnoreCase);
            static bool OptionIsIf(string o) =>
                o.Contains("IF", StringComparison.OrdinalIgnoreCase)
                || o.Contains("I-F", StringComparison.OrdinalIgnoreCase)
                || o.Contains("ITA", StringComparison.OrdinalIgnoreCase)
                || o.Contains("Italia", StringComparison.OrdinalIgnoreCase)
                || o.Contains("Francia", StringComparison.OrdinalIgnoreCase);

            static bool MentionsEtr1000(string o) =>
                o.Contains("ETR1000", StringComparison.OrdinalIgnoreCase)
                || o.Contains("ETR 1000", StringComparison.OrdinalIgnoreCase)
                || o.Contains("ETR1001", StringComparison.OrdinalIgnoreCase)
                || o.Contains("ETR 1001", StringComparison.OrdinalIgnoreCase);

            if (wantFh)
            {
                return list.FirstOrDefault(o => MentionsEtr1000(o) && OptionIsFh(o) && !OptionIsIf(o));
            }
            if (wantIf)
            {
                return list.FirstOrDefault(o => MentionsEtr1000(o) && OptionIsIf(o));
            }

            // Tipo base: pretende un'opzione priva dei marcatori delle due varianti.
            return list.FirstOrDefault(o => MentionsEtr1000(o) && !OptionIsFh(o) && !OptionIsIf(o));
        }
    }
}
