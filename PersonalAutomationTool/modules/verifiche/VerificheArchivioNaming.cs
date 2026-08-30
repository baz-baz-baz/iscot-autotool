using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PersonalAutomationTool.Modules.Verifiche
{
    /// <summary>
    /// Le due regole "di nomenclatura" dell'archiviazione verifiche, estratte come funzioni pure:
    /// come si compone il nuovo nome del file, e quale foglio storico è quello dell'anno corrente.
    ///
    /// <para>
    /// Sono qui, e non dentro il servizio che scrive il workbook, perché siano verificabili senza
    /// toccare un file Excel: è il modo in cui questa applicazione tiene sotto controllo il suo
    /// rischio principale, cioè l'<b>output silenziosamente sbagliato</b> su nomi e percorsi (§6 di
    /// PROJECT_MEMORY.md).
    /// </para>
    /// </summary>
    internal static partial class VerificheArchivioNaming
    {
        /// <summary>
        /// Compone il nuovo nome del file principale:
        /// <c>Verifiche ETR500 240826 21_36 Rossi.xlsx</c> — prefisso di flotta, data, ora
        /// <c>HH_mm</c> (i due punti non sono ammessi nei nomi file Windows), cognome.
        ///
        /// <para>
        /// ⚠️ <b>La data è <c>ddMMyy</c>, non <c>AAMMGG</c>.</b> La specifica la chiamava "AAMMGG",
        /// ma l'esempio che la accompagnava — <c>240826</c> — e i nomi dei file reali forniti dal
        /// committente (<c>Verifiche ETR500 240826 14_31 Franzese.xlsx</c>, del 24 agosto 2026)
        /// dicono l'opposto: giorno, mese, anno a due cifre. È anche la convenzione di **tutta**
        /// l'applicazione, fissata dall'invariante §5.1 di PROJECT_MEMORY.md ("la data è sempre
        /// <c>ddMMyy</c>"). Interpretare l'etichetta alla lettera avrebbe prodotto <c>260824</c>,
        /// cioè un file che i tecnici avrebbero letto come 26 agosto 2024.
        /// </para>
        /// </summary>
        /// <param name="prefisso">Es. <c>"Verifiche ETR500"</c>, dalla configurazione dei percorsi.</param>
        /// <param name="momento">Data e ora dell'archiviazione.</param>
        /// <param name="cognome">Cognome digitato dal tecnico, già validato come non vuoto.</param>
        internal static string ComponiNomeFile(string prefisso, DateTime momento, string cognome)
        {
            string cognomePulito = PulisciPerNomeFile(cognome).Trim();
            string data = momento.ToString("ddMMyy", CultureInfo.InvariantCulture);
            string ora = momento.ToString("HH\\_mm", CultureInfo.InvariantCulture);

            return $"{prefisso.Trim()} {data} {ora} {cognomePulito}.xlsx";
        }

        /// <summary>
        /// Sostituisce con uno spazio i caratteri non ammessi nei nomi di file, e comprime gli spazi
        /// multipli. Il cognome è testo digitato a mano: una barra o due punti bloccherebbero il
        /// salvataggio a fine turno, quando il tecnico ha fretta.
        /// </summary>
        internal static string PulisciPerNomeFile(string? valore)
        {
            if (string.IsNullOrWhiteSpace(valore)) return string.Empty;

            var vietati = Path.GetInvalidFileNameChars();
            string ripulito = new(valore.Select(c => vietati.Contains(c) ? ' ' : c).ToArray());
            return SpaziMultipli().Replace(ripulito, " ").Trim();
        }

        /// <summary>
        /// Individua, fra i nomi dei fogli di un workbook, quello dello storico che copre
        /// <paramref name="anno"/>.
        ///
        /// <para>
        /// <b>Non è un confronto per nome: i tre file reali usano tre convenzioni diverse.</b>
        /// ETR700 ha <c>"STORICO 2026"</c>, ETR1000 ha <c>"STORICO 22-24-25-26"</c> ed ETR500 ha
        /// <c>"STORICO '22-'23-'24-'25-'26"</c> — un foglio unico che copre più anni, con o senza
        /// apostrofo, a due o quattro cifre. Cercare l'anno corrente come stringa fallirebbe su due
        /// file su tre. Qui si estraggono invece <b>tutti</b> gli anni citati nel nome
        /// (<see cref="EstraiAnni"/>) e si verifica che <paramref name="anno"/> sia fra quelli.
        /// </para>
        /// </summary>
        /// <returns>Il nome del foglio, oppure <c>null</c> se nessuno copre quell'anno.</returns>
        internal static string? TrovaFoglioStorico(IEnumerable<string> nomiFogli, int anno)
        {
            var candidati = nomiFogli
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Where(n => n.TrimStart().StartsWith("STORICO", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // A parità di copertura vince il foglio più specifico (meno anni): un ipotetico
            // "STORICO 2026" è preferibile a "STORICO 22-...-26" se entrambi esistessero.
            return candidati
                .Select(n => (Nome: n, Anni: EstraiAnni(n)))
                .Where(x => x.Anni.Contains(anno))
                .OrderBy(x => x.Anni.Count)
                .Select(x => x.Nome)
                .FirstOrDefault();
        }

        /// <summary>
        /// Estrae gli anni citati nel nome di un foglio, normalizzando le due cifre a quattro:
        /// <c>"STORICO '22-'23-'26"</c> → <c>{2022, 2023, 2026}</c>, <c>"STORICO 2019-2020"</c> →
        /// <c>{2019, 2020}</c>.
        ///
        /// <para>
        /// I gruppi di 4 cifre sono presi come anni pieni, quelli di 2 come <c>2000+n</c>. Sequenze
        /// di lunghezza diversa (1, 3, 5+ cifre) sono ignorate: non sono anni e includerle
        /// produrrebbe corrispondenze casuali.
        /// </para>
        /// </summary>
        internal static HashSet<int> EstraiAnni(string nomeFoglio)
        {
            var anni = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(nomeFoglio)) return anni;

            foreach (Match m in SequenzeDiCifre().Matches(nomeFoglio))
            {
                string cifre = m.Value;
                if (cifre.Length == 4 && int.TryParse(cifre, out int annoPieno))
                {
                    anni.Add(annoPieno);
                }
                else if (cifre.Length == 2 && int.TryParse(cifre, out int annoBreve))
                {
                    anni.Add(2000 + annoBreve);
                }
            }

            return anni;
        }

        [GeneratedRegex(@"\d+")]
        private static partial Regex SequenzeDiCifre();

        [GeneratedRegex(@"\s{2,}")]
        private static partial Regex SpaziMultipli();
    }
}
