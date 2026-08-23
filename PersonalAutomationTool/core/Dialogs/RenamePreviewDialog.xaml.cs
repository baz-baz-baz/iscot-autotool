using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PersonalAutomationTool.Core.Dialogs
{
    /// <summary>Una riga del <see cref="RenamePreviewDialog"/>: solo nomi, non percorsi completi.</summary>
    public sealed record RenamePreviewItem(string NomeAttuale, string NuovoNome);

    /// <summary>
    /// Dialog modale essenziale (intervento 4.1, Sprint 3) richiamato prima di eseguire una rinomina
    /// in blocco, in PDF e in HOME (vedi <c>PdfView.BtnRinomina_Click</c> e
    /// <c>HomeViewModel.OnAggiornaTicket</c>/<c>OnAggiornaData</c>). Mostra solo vecchio/nuovo nome,
    /// in sola lettura: la decisione di *cosa* rinominare resta ai rispettivi planner
    /// (<c>PdfRenamePlanner</c> per PDF, logica inline per HOME) — questo dialog non decide nulla,
    /// mostra soltanto il piano già calcolato e lascia all'utente conferma o annullamento.
    /// </summary>
    public partial class RenamePreviewDialog : Window
    {
        public RenamePreviewDialog(IReadOnlyList<RenamePreviewItem> items, string? subtitle = null)
        {
            InitializeComponent();
            ItemsGrid.ItemsSource = items;
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                SubtitleText.Text = subtitle;
            }
        }

        private void BtnConferma_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnAnnulla_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Costruisce gli item dai percorsi completi (solo il nome file/cartella finale viene
        /// mostrato) e mostra il dialog. Restituisce <c>true</c> solo se l'utente ha premuto
        /// "Conferma". <paramref name="owner"/> può essere <c>null</c> (es. chiamato da un
        /// ViewModel senza riferimento diretto a una Window): in quel caso il dialog si centra
        /// sullo schermo invece che sulla finestra padre, per non lanciare l'eccezione WPF che
        /// <c>WindowStartupLocation.CenterOwner</c> solleverebbe senza un Owner.
        /// </summary>
        public static bool Confirm(Window? owner, IReadOnlyList<(string OldPath, string NewPath)> operations, string? subtitle = null)
        {
            var items = operations
                .Select(o => new RenamePreviewItem(Path.GetFileName(o.OldPath), Path.GetFileName(o.NewPath)))
                .ToList();

            var dialog = new RenamePreviewDialog(items, subtitle);
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            return dialog.ShowDialog() == true;
        }
    }
}
