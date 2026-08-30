using System.Windows;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Stato del turno scelto nel pop-up che precede la generazione della mail: determina sia il
    /// colore dell'etichetta di flotta sia il testo centrale del corpo HTML (vedi
    /// <see cref="OutlookRapportinoMailService.BuildHtmlBody"/>).
    /// </summary>
    public enum StatoTurno
    {
        /// <summary>"Non ci sono attività da svolgere" — verde, dicitura fissa "Nessuna attività in sospeso."</summary>
        NessunaAttivita,

        /// <summary>"Ci sono attività previste" — ambra, corpo lasciato vuoto per la digitazione manuale.</summary>
        AttivitaPreviste,

        /// <summary>"Ci sono attività imminenti o in corso" — rosso, corpo lasciato vuoto per la digitazione manuale.</summary>
        AttivitaImminentiOInCorso
    }

    /// <summary>
    /// Produce il PDF del rapportino. Esiste come interfaccia — invece di chiamare direttamente
    /// <see cref="PassaggioConsegnePdfExporter"/> — perché il ViewModel possa essere verificato senza
    /// scrivere file su disco.
    /// </summary>
    public interface IRapportinoPdfExporter
    {
        /// <summary>Genera il PDF e ne restituisce il percorso.</summary>
        string Esporta(RapportinoSnapshot rapportino);
    }

    /// <summary>
    /// Apre la bozza email del passaggio di consegne. L'implementazione reale parla con Outlook via
    /// COM; nei test se ne usa una finta, così il flusso "Genera Mail" è verificabile per intero senza
    /// che si apra davvero un messaggio.
    /// </summary>
    public interface IRapportinoMailService
    {
        /// <summary>
        /// Crea in Outlook una bozza con oggetto, destinatari e PDF allegato, e la mostra all'utente
        /// <b>senza inviarla</b>: l'invio resta sempre un'azione manuale (invariante §5.5).
        /// </summary>
        void ApriBozza(RapportinoSnapshot rapportino, string percorsoPdf, string destinatariKey, string oggetto, StatoTurno stato);
    }

    /// <summary>
    /// Chiede all'utente lo stato del turno tramite il pop-up modale a tre pulsanti, prima che la mail
    /// venga generata. Astratta per lo stesso motivo delle altre due: verificare il flusso "Genera
    /// Mail" da xUnit senza far comparire alcuna finestra.
    /// </summary>
    public interface IStatoTurnoDialogService
    {
        /// <summary>
        /// Mostra il dialog e restituisce lo stato scelto, oppure <c>null</c> se l'utente lo ha chiuso
        /// o ha premuto "Annulla": in quel caso la generazione della mail deve interrompersi.
        /// </summary>
        StatoTurno? ChiediStato();
    }

    /// <summary>
    /// Messaggi verso l'utente. Astratta perché il ViewModel non apra <c>MessageBox</c> durante i
    /// test: una finestra modale in una suite xUnit la bloccherebbe a tempo indeterminato.
    /// </summary>
    public interface INotificaUtente
    {
        void Errore(string messaggio, string titolo);
        void Informazione(string messaggio, string titolo);
        bool Conferma(string messaggio, string titolo);
    }

    /// <summary>Implementazione di produzione: delega all'esportatore statico.</summary>
    public sealed class RapportinoPdfExporter : IRapportinoPdfExporter
    {
        public string Esporta(RapportinoSnapshot rapportino) =>
            PassaggioConsegnePdfExporter.ExportToPdf(rapportino);
    }

    /// <summary>Implementazione di produzione: apre <see cref="StatoTurnoDialog"/> come finestra modale.</summary>
    public sealed class WpfStatoTurnoDialogService : IStatoTurnoDialogService
    {
        public StatoTurno? ChiediStato()
        {
            var dialog = new StatoTurnoDialog
            {
                Owner = Application.Current?.MainWindow
            };

            bool? confermato = dialog.ShowDialog();
            return confermato == true ? dialog.StatoSelezionato : null;
        }
    }

    /// <summary>Implementazione di produzione: <c>MessageBox</c> di WPF, come nel resto dell'app.</summary>
    public sealed class MessageBoxNotificaUtente : INotificaUtente
    {
        public void Errore(string messaggio, string titolo) =>
            MessageBox.Show(messaggio, titolo, MessageBoxButton.OK, MessageBoxImage.Error);

        public void Informazione(string messaggio, string titolo) =>
            MessageBox.Show(messaggio, titolo, MessageBoxButton.OK, MessageBoxImage.Information);

        public bool Conferma(string messaggio, string titolo) =>
            MessageBox.Show(messaggio, titolo, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
