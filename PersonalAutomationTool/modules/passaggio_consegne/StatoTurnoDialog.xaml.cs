using System.Windows;

namespace PersonalAutomationTool.Modules.PassaggioConsegne
{
    /// <summary>
    /// Dialog modale mostrato alla pressione di "Genera Mail": chiede lo stato del turno con tre
    /// pulsanti distinti invece di una ComboBox o di radio button, perché la scelta deve essere
    /// immediata e senza possibilità di lasciare un valore implicito selezionato per default.
    ///
    /// <para>
    /// Chiudere la finestra o premere "Annulla" lascia <see cref="StatoSelezionato"/> a <c>null</c> e
    /// <c>DialogResult</c> a <c>false</c>: <see cref="WpfStatoTurnoDialogService"/> lo traduce in un
    /// esito nullo, e <c>PassaggioConsegneViewModel.GeneraMailAsync</c> interrompe l'operazione senza
    /// generare né PDF né bozza.
    /// </para>
    /// </summary>
    public partial class StatoTurnoDialog : Window
    {
        public StatoTurno? StatoSelezionato { get; private set; }

        public StatoTurnoDialog()
        {
            InitializeComponent();
        }

        private void NessunaAttivita_Click(object sender, RoutedEventArgs e) => Conferma(StatoTurno.NessunaAttivita);
        private void AttivitaPreviste_Click(object sender, RoutedEventArgs e) => Conferma(StatoTurno.AttivitaPreviste);
        private void AttivitaImminenti_Click(object sender, RoutedEventArgs e) => Conferma(StatoTurno.AttivitaImminentiOInCorso);

        private void Conferma(StatoTurno stato)
        {
            StatoSelezionato = stato;
            DialogResult = true;
            Close();
        }

        private void Annulla_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
