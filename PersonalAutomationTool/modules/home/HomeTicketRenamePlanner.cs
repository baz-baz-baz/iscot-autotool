using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PersonalAutomationTool.Core.Naming;

namespace PersonalAutomationTool.Modules.Home
{
    /// <summary>
    /// Logica di decisione di "Aggiorna ticket" (<see cref="HomeViewModel.OnAggiornaTicket"/>),
    /// estratta per essere testabile senza WPF — stesso principio già applicato a
    /// <c>PdfRenamePlanner</c> (intervento 2.1 della roadmap, "separare decidere da eseguire").
    ///
    /// <para>
    /// <b>Il bug che questa classe corregge.</b> Il codice precedente raggruppava le sottocartelle in
    /// base al <b>ticket attualmente scritto nel nome</b> (il primo token, es. <c>"SRX"</c>): se le
    /// quattro cartelle di un treno a doppia motrice condividevano lo stesso ticket segnaposto — il
    /// caso normale prima di una prima assegnazione, es. <c>SRX DUMP E404P 634 …</c> e
    /// <c>SRX DUMP E404P 635 …</c> — quel raggruppamento produceva un solo gruppo, e il secondo campo
    /// ticket veniva ignorato: tutte e quattro le cartelle ricevevano <c>Ticket 1</c>. Il campo giusto
    /// per distinguere le due motrici non è il ticket corrente (che può essere identico per
    /// entrambe), ma la <b>loco</b>, che <see cref="LogDumpFolderName.TryParse"/> estrae dalla
    /// grammatica ufficiale del nome (<c>SR{ticket} {LOG|DUMP} {tipo} {loco} {software} {ddMMyy}
    /// {utente}</c>, invariante §5.1 di PROJECT_MEMORY.md).
    /// </para>
    /// </summary>
    public static class HomeTicketRenamePlanner
    {
        /// <summary>
        /// Calcola il piano di rinomina per le sottocartelle dirette di <paramref name="parentFolderPath"/>.
        /// Usata identica sia per popolare l'anteprima sia per l'esecuzione (stesso principio di
        /// <c>PdfRenamePlanner</c>/<c>OnAggiornaData</c>): non c'è un secondo percorso di codice che
        /// possa disallinearsi.
        /// </summary>
        /// <param name="parentFolderPath">La cartella madre (es. "E404P 34" sotto LOG &amp; DUMP).</param>
        /// <param name="ticket1">
        /// Ticket già normalizzato con prefisso "SR" (vedi <see cref="HomeViewModel.NormalizeTicketPrefix"/>),
        /// o stringa vuota/null se il campo non è compilato.
        /// </param>
        /// <param name="ticket2">Come <paramref name="ticket1"/>, per la seconda motrice.</param>
        /// <param name="knownTypes">
        /// I "tipo" noti (<c>FlotteCache.GetDistinctTipiOrderByLengthDesc</c>), ordinati dal più lungo
        /// al più corto — richiesto da <see cref="LogDumpFolderName.TryParse"/>.
        /// </param>
        public static IReadOnlyList<(string OldPath, string NewPath)> CreatePlan(
            string parentFolderPath, string? ticket1, string? ticket2, IReadOnlyList<string> knownTypes)
        {
            var operations = new List<(string OldPath, string NewPath)>();

            bool ticket1Presente = !string.IsNullOrWhiteSpace(ticket1);
            bool ticket2Presente = !string.IsNullOrWhiteSpace(ticket2);
            if (!ticket1Presente && !ticket2Presente) return operations;

            if (!Directory.Exists(parentFolderPath)) return operations;

            // Solo le sottocartelle che rispettano la grammatica ufficiale: le altre non possono
            // essere assegnate con certezza a una loco e vengono lasciate come sono, non rinominate
            // "alla cieca" come faceva la vecchia euristica basata sul ticket corrente.
            var righe = new List<(string Path, string Name, string Loco)>();
            foreach (string dir in Directory.GetDirectories(parentFolderPath))
            {
                string name = new DirectoryInfo(dir).Name;
                if (LogDumpFolderName.TryParse(name, knownTypes, out LogDumpFolderName? info) && info != null)
                {
                    righe.Add((dir, name, info.Loco));
                }
            }

            if (righe.Count == 0) return operations;

            // "Loco 1"/"Loco 2" nell'ordine di censimento: numerico quando entrambe le loco lo sono
            // (il caso comune, es. "634"/"635"), alfabetico altrimenti.
            var distinctLocos = righe
                .Select(r => r.Loco)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(l => l, LocoComparer.Instance)
                .ToList();

            bool dueMotriciConDueTicket = ticket1Presente && ticket2Presente && distinctLocos.Count > 1;

            foreach (var (path, name, loco) in righe)
            {
                string? nuovoTicket;
                if (dueMotriciConDueTicket)
                {
                    int indice = distinctLocos.FindIndex(l => string.Equals(l, loco, StringComparison.OrdinalIgnoreCase));
                    nuovoTicket = indice == 0 ? ticket1 : ticket2;
                }
                else
                {
                    // Cassa singola (un solo valore di loco), oppure un solo campo ticket compilato:
                    // quello compilato si applica a tutte le cartelle coinvolte.
                    nuovoTicket = ticket1Presente ? ticket1 : ticket2;
                }

                if (string.IsNullOrEmpty(nuovoTicket)) continue;

                // Sostituisce solo il primo token (il ticket) del nome esistente, lasciando LOG/DUMP,
                // tipo, loco, software, data e utente esattamente come sono: nessuna ricostruzione via
                // LogDumpFolderName.Format(), che normalizzerebbe la spaziatura anche dove non serve.
                var parts = name.Split(' ');
                parts[0] = nuovoTicket;
                string newName = string.Join(' ', parts);
                string newPath = Path.Combine(parentFolderPath, newName);

                if (!string.Equals(path, newPath, StringComparison.Ordinal))
                {
                    operations.Add((path, newPath));
                }
            }

            return operations;
        }

        private sealed class LocoComparer : IComparer<string>
        {
            public static readonly LocoComparer Instance = new();

            public int Compare(string? x, string? y)
            {
                if (int.TryParse(x, out int xi) && int.TryParse(y, out int yi))
                {
                    return xi.CompareTo(yi);
                }
                return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
