using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace PersonalAutomationTool.Core.Naming
{
    /// <summary>Il tipo di sottocartella: raccolta log oppure dump della memoria diagnostica.</summary>
    public enum LogDumpKind
    {
        Log,
        Dump
    }

    /// <summary>
    /// Rappresenta il nome di una SOTTOCARTELLA sotto una cartella madre di "LOG &amp; DUMP", nel
    /// formato scritto da <c>CartelleView.BtnCrea_Click</c>:
    /// <c>SR{ticket} {LOG|DUMP} {tipo} {loco} {software} {ddMMyy} {utente}</c>
    /// (es. <c>"SR1247654 LOG ETR700 117 04.02HR 300526 Todde"</c>).
    /// <para>
    /// Questo tipo copre SOLO questo formato. Non copre il nome della "cartella madre"
    /// (<c>{tipo} {treno}</c>, senza ticket né data — vedi <c>HomeViewModel</c>,
    /// <c>ExcelViewModel.MatchesTrain</c>) né il formato dei file ZIP spostati in rete
    /// (<c>[Ticket] [LOG|DUMP] [Treno] [Loco] ...</c>, senza prefisso "SR" — vedi
    /// <c>HomeViewModel.OnLogDumpRete</c>). Unificare anche quei due formati è fuori dallo scope
    /// di questa prima estrazione: sono grammatiche diverse, con parser diversi, usate da
    /// chiamanti diversi.
    /// </para>
    /// <para>
    /// <b>Perché centralizzare qui il parsing.</b> Prima di questo tipo, la stessa grammatica
    /// veniva ridecodificata in modo indipendente in almeno otto punti del codice (PdfView,
    /// HomeViewModel, EmailService, TrainViewHelper, ChiusuraTicketDialog, ExcelViewModel, ...),
    /// ciascuno con la propria copia della logica di split e con la propria gestione (spesso
    /// assente) dei casi limite. Uno solo di questi punti — <c>PdfView.ParseLogFolderName</c> — è
    /// stato migrato a usare <see cref="TryParse"/> in questa sessione, come pilota: gli altri
    /// sette restano invariati e vanno migrati uno alla volta in sessioni future, verificando ogni
    /// volta sul campo prima di procedere con il successivo (vedi PROJECT_MEMORY.md §6).
    /// </para>
    /// </summary>
    public sealed partial record LogDumpFolderName
    {
        public required string Ticket { get; init; }
        public required LogDumpKind Kind { get; init; }
        public required string Tipo { get; init; }
        public required string Loco { get; init; }

        /// <summary>
        /// Versione software, se presente fra la loco e la data. Il parser originale
        /// (<c>PdfView.ParseLogFolderName</c>) scartava questo campo: nessun chiamante esistente
        /// lo leggeva dal nome della cartella (la versione software viene normalmente recuperata
        /// dal database <c>flotte</c>, non dal nome del percorso). È stato aggiunto qui perché è
        /// parte della grammatica scritta da <c>CartelleView</c> e un domani potrebbe servire;
        /// la sua presenza non altera in alcun modo Ticket/Tipo/Loco/Data/Utente.
        /// </summary>
        public required string Software { get; init; }

        /// <summary>Data nel formato grezzo a 6 cifre <c>ddMMyy</c>, così come compare nel nome (nessuna validazione di calendario).</summary>
        public required string Data { get; init; }

        public required string Utente { get; init; }

        [GeneratedRegex(@"^SR(?<ticket>\S+)\s(?<kind>LOG|DUMP)\s(?<rest>.*)$")]
        private static partial Regex FolderPrefixRegex();

        [GeneratedRegex(@"\s(?<data>\d{6})\s(?<utente>.*)$")]
        private static partial Regex DateAndUserRegex();

        /// <summary>
        /// Prova a interpretare <paramref name="folderName"/> come nome di sottocartella
        /// LOG/DUMP. Restituisce <see langword="false"/> senza lanciare eccezioni se il nome non
        /// rispetta il formato (incluso il caso, ereditato fedelmente dal parser originale, di una
        /// cartella creata con campo "utente" vuoto: <c>CartelleView</c> applica <c>.Trim()</c>
        /// all'intero nome dopo l'interpolazione, quindi lo spazio separatore prima dell'utente
        /// vuoto viene rimosso e il nome risultante non può più essere riconosciuto — non è un bug
        /// introdotto qui, è un limite preesistente del formato su disco).
        /// </summary>
        /// <param name="folderName">Il nome della sottocartella (non il percorso completo).</param>
        /// <param name="knownTypes">
        /// I valori di "tipo" noti (tipicamente <c>SELECT DISTINCT tipo FROM flotte</c>), che
        /// <b>DEVONO</b> essere ordinati dal più lungo al più corto dal chiamante. È l'unico modo
        /// per distinguere correttamente un tipo composto come <c>"ETR1000 I-F"</c> dal suo
        /// prefisso <c>"ETR1000"</c>: se l'ordine fosse invertito, un nome con tipo "ETR1000 I-F"
        /// verrebbe interpretato come tipo "ETR1000" con loco "I-F". Se un tipo non compare in
        /// questa lista, si ricade sul comportamento del parser originale: il primo token dopo il
        /// prefisso viene comunque usato come tipo.
        /// </param>
        public static bool TryParse(string? folderName, IReadOnlyList<string> knownTypes, out LogDumpFolderName? result)
        {
            result = null;
            if (string.IsNullOrEmpty(folderName))
            {
                return false;
            }

            var prefixMatch = FolderPrefixRegex().Match(folderName);
            if (!prefixMatch.Success)
            {
                return false;
            }

            string ticket = prefixMatch.Groups["ticket"].Value;
            string kindText = prefixMatch.Groups["kind"].Value;
            string rest = prefixMatch.Groups["rest"].Value;

            var dateMatch = DateAndUserRegex().Match(rest);
            if (!dateMatch.Success)
            {
                return false;
            }

            string data = dateMatch.Groups["data"].Value;
            string utente = dateMatch.Groups["utente"].Value;

            // Tutto ciò che precede la data (tipo, loco, eventuale software), senza il separatore
            // finale: dateMatch.Index punta allo spazio che precede la data, quindi lo slice si
            // ferma prima di quello spazio.
            string tipoLocoSoftware = rest[..dateMatch.Index];

            string? tipo = null;
            foreach (var candidate in knownTypes)
            {
                if (tipoLocoSoftware.StartsWith(candidate + " ", StringComparison.Ordinal))
                {
                    tipo = candidate;
                    break;
                }
            }

            if (string.IsNullOrEmpty(tipo))
            {
                // Nessun tipo noto corrisponde: fallback identico all'originale, il primo token
                // diventa il tipo "per difetto".
                var fallbackParts = tipoLocoSoftware.Split(' ');
                tipo = fallbackParts[0];
            }

            string remaining = tipoLocoSoftware[tipo.Length..].Trim();
            var remainingTokens = remaining.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            string loco = remainingTokens.Length > 0 ? remainingTokens[0] : "";
            string software = remainingTokens.Length > 1 ? string.Join(' ', remainingTokens.Skip(1)) : "";

            result = new LogDumpFolderName
            {
                Ticket = ticket,
                Kind = kindText.Equals("DUMP", StringComparison.Ordinal) ? LogDumpKind.Dump : LogDumpKind.Log,
                Tipo = tipo,
                Loco = loco,
                Software = software,
                Data = data,
                Utente = utente
            };
            return true;
        }

        /// <summary>
        /// Ricostruisce il nome nella stessa forma prodotta da <c>CartelleView.BtnCrea_Click</c>:
        /// interpolazione dei campi seguita da <c>.Trim()</c> sull'intera stringa. Come nel
        /// codice originale, un campo intermedio vuoto (tipicamente <see cref="Software"/>)
        /// produce uno spazio doppio nel nome risultante anziché essere normalizzato: cambiare
        /// questo comportamento vorrebbe dire produrre nomi diversi da quelli che l'applicazione
        /// ha già scritto su disco in passato.
        /// </summary>
        public string Format()
        {
            string kindText = Kind == LogDumpKind.Dump ? "DUMP" : "LOG";
            return $"SR{Ticket} {kindText} {Tipo} {Loco} {Software} {Data} {Utente}".Trim();
        }

        public override string ToString() => Format();
    }
}
