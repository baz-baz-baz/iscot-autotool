using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Fotografia immutabile di un <see cref="RapportinoTurno"/>, presa sul thread UI e passata così
    /// com'è a <see cref="PassaggioConsegnePdfExporter"/>.
    ///
    /// <para>
    /// <b>È il pezzo che tiene il PDF fuori dalla UI, ed è deliberato.</b> La prima versione del modulo
    /// produceva il PDF fotografando la finestra con <c>RenderTargetBitmap</c>, e da quella scelta
    /// discendevano tre problemi documentati in PROJECT_MEMORY.md: una bitmap da decine di MB nella
    /// Large Object Heap (criticità E, §6.4), l'impossibilità di virtualizzare le griglie senza
    /// troncare righe dal PDF (§6.1-bis, scoperta #3), e il flag <c>IsExporting</c> che nascondeva le
    /// checkbox mutando la UI durante l'esportazione — origine dello sfarfallio inseguito per quattro
    /// correzioni in §6.1-undecies.
    /// </para>
    ///
    /// <para>
    /// Con lo snapshot nessuno dei tre può ripresentarsi: il PDF è disegnato in grafica vettoriale a
    /// partire da <b>stringhe già pronte</b>, la UI non viene toccata né letta durante il disegno, e
    /// il lavoro può girare su thread pool senza corse con l'utente che continua a digitare. La regola
    /// "Si"/"No"/vuoto (<see cref="SiNoCell.PerPdf"/>) è applicata <b>qui</b>, al momento della
    /// cattura, così l'esportatore non deve conoscere alcuna logica di dominio.
    /// </para>
    /// </summary>
    public sealed record RapportinoSnapshot(
        string TipoTreno,
        string Sottotitolo,
        string Nome,
        string Cognome,
        string Data,
        string OraInizio,
        string OraFine,
        IReadOnlyList<MovimentoSnapshot> Movimenti,
        IReadOnlyList<InterventoSnapshot> Interventi,
        IReadOnlyList<InterventoNonSvoltoSnapshot> InterventiNonSvolti)
    {
        /// <summary>
        /// Cattura lo stato corrente del rapportino. Da chiamare sul thread UI: legge
        /// <c>ObservableCollection</c> legate all'interfaccia.
        /// </summary>
        public static RapportinoSnapshot Cattura(RapportinoTurno r) => new(
            TipoTreno: r.TipoTreno,
            Sottotitolo: r.Sottotitolo,
            Nome: r.Nome ?? string.Empty,
            Cognome: r.Cognome ?? string.Empty,
            Data: r.Data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            OraInizio: r.OraInizio ?? string.Empty,
            OraFine: r.OraFine ?? string.Empty,
            Movimenti: r.Movimenti.Select(m => new MovimentoSnapshot(
                m.Numero, m.Treno, m.Loco, m.DataIngresso, m.OraIngresso, m.DataUscita, m.OraUscita)).ToList(),
            Interventi: r.Interventi.Select(i => new InterventoSnapshot(
                i.TrenoLoco,
                i.Descrizione,
                SiNoCell.PerPdf(i.CompilazioneOdl, i.IsCompilata),
                SiNoCell.PerPdf(i.ChiusuraTicket, i.IsCompilata),
                SiNoCell.PerPdf(i.CompReport, i.IsCompilata),
                SiNoCell.PerPdf(i.EmailIngegneria, i.IsCompilata),
                SiNoCell.PerPdf(i.AggiornareVerifiche, i.IsCompilata))).ToList(),
            InterventiNonSvolti: r.InterventiNonSvolti.Select(n => new InterventoNonSvoltoSnapshot(
                n.Numero,
                n.TrenoLoco,
                n.Motivazione,
                n.OraRichiesta,
                n.Referente,
                SiNoCell.PerPdf(n.InviataEmailIngegneria, n.IsCompilata),
                SiNoCell.PerPdf(n.PassaggioConsegna, n.IsCompilata))).ToList());
    }

    /// <summary>Riga della tabella movimenti, già in forma di testo.</summary>
    public sealed record MovimentoSnapshot(
        int Numero,
        string Treno,
        string Loco,
        string DataIngresso,
        string OraIngresso,
        string DataUscita,
        string OraUscita);

    /// <summary>
    /// Riga della tabella dettaglio interventi. I 5 campi booleani sono già stringhe
    /// <c>"Si"</c>/<c>"No"</c>/vuoto: nel PDF non esiste alcun controllo grafico di checkbox.
    /// </summary>
    public sealed record InterventoSnapshot(
        string TrenoLoco,
        string Descrizione,
        string CompilazioneOdl,
        string ChiusuraTicket,
        string CompReport,
        string EmailIngegneria,
        string AggiornareVerifiche);

    /// <summary>Riga della tabella interventi non svolti, con le 2 colonne booleane già in testo.</summary>
    public sealed record InterventoNonSvoltoSnapshot(
        int Numero,
        string TrenoLoco,
        string Motivazione,
        string OraRichiesta,
        string Referente,
        string InviataEmail,
        string PassaggioConsegna);
}
