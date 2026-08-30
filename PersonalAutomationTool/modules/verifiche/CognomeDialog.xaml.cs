using System.Windows;
using System.Windows.Input;

namespace PersonalAutomationTool.Modules.Verifiche
{
    /// <summary>
    /// Dialog modale che chiede il cognome del tecnico prima di archiviare una verifica.
    ///
    /// <para>
    /// Deliberatamente minimo, come richiesto: etichetta "Cognome?", una casella di testo e i due
    /// pulsanti. Annullare — o confermare con il campo vuoto — non produce alcuna modifica: la
    /// validazione sta qui e non nel ViewModel, così l'operazione di archiviazione non parte
    /// nemmeno.
    /// </para>
    /// </summary>
    public partial class CognomeDialog : Window
    {
        /// <summary>Cognome inserito, già ripulito dagli spazi ai bordi. Valido solo se il dialog è stato confermato.</summary>
        public string Cognome { get; private set; } = string.Empty;

        /// <param name="riepilogo">
        /// Descrizione della riga che si sta archiviando (treno e loco), mostrata sotto l'etichetta:
        /// il pulsante sta in una griglia di molte righe e una conferma senza contesto è un invito a
        /// sbagliare riga.
        /// </param>
        public CognomeDialog(string? riepilogo = null)
        {
            InitializeComponent();

            if (string.IsNullOrWhiteSpace(riepilogo))
            {
                TxtRiepilogo.Visibility = Visibility.Collapsed;
            }
            else
            {
                TxtRiepilogo.Text = riepilogo;
            }

            Loaded += (_, _) => TxtCognome.Focus();
        }

        private void Conferma_Click(object sender, RoutedEventArgs e) => Conferma();

        private void Annulla_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtCognome_KeyDown(object sender, KeyEventArgs e)
        {
            // Invio conferma anche senza spostarsi sul pulsante: a fine turno si digita e si invia.
            if (e.Key == Key.Enter) Conferma();
        }

        private void Conferma()
        {
            string valore = TxtCognome.Text?.Trim() ?? string.Empty;
            if (valore.Length == 0)
            {
                TxtErrore.Visibility = Visibility.Visible;
                TxtCognome.Focus();
                return;
            }

            Cognome = valore;
            DialogResult = true;
            Close();
        }
    }
}
