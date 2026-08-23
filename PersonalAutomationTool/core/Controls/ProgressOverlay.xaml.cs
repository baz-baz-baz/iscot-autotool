using System.Windows;
using System.Windows.Controls;

namespace PersonalAutomationTool.Core.Controls
{
    /// <summary>
    /// Overlay di caricamento riutilizzabile (intervento 4.2, Sprint 3). Espone <see cref="IsBusy"/>
    /// e <see cref="Message"/> come DependencyProperty: un ViewModel MVVM le lega via binding
    /// (<c>IsBusy="{Binding IsLoading}"</c>), un code-behind puro (es. <c>PdfView</c>) le assegna
    /// direttamente. <see cref="Report"/> è solo una scorciatoia per il formato di conteggio testuale
    /// concordato ("Elaborazione 3 di 12...").
    /// </summary>
    public partial class ProgressOverlay : UserControl
    {
        public static readonly DependencyProperty IsBusyProperty =
            DependencyProperty.Register(nameof(IsBusy), typeof(bool), typeof(ProgressOverlay), new PropertyMetadata(false));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(ProgressOverlay), new PropertyMetadata(string.Empty));

        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public ProgressOverlay()
        {
            InitializeComponent();
        }

        /// <summary>Imposta <see cref="Message"/> nel formato concordato: "{verbo} {corrente} di {totale}...".</summary>
        public void Report(int current, int total, string verb = "Elaborazione") =>
            Message = $"{verb} {current} di {total}...";
    }
}
