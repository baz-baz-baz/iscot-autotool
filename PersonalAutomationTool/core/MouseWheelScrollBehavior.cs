using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PersonalAutomationTool.Core
{
    /// <summary>
    /// Colma la lacuna di WPF sullo scorrimento annidato: un <see cref="ScrollViewer"/> interno
    /// "ingoia" l'evento della rotellina anche quando è già arrivato a fondo corsa, impedendo al
    /// contenitore esterno di scorrere. Questo comportamento globale inoltra lo scatto al
    /// ScrollViewer padre quando quello interno non può più muoversi.
    /// </summary>
    public static class MouseWheelScrollBehavior
    {
        /// <summary>Divisore applicato al delta per l'inoltro al contenitore esterno (≈40 px per scatto).</summary>
        private const double ScrollStepDivisor = 3.0;

        public static void InitializeGlobalMouseWheelHandler()
        {
            // handledEventsToo: false — se un handler più specifico ha già gestito lo scatto
            // (per esempio lo scorrimento orizzontale di ChiusuraTicketDialog) noi non dobbiamo
            // intervenire. In precedenza era true e questo handler girava anche sugli eventi
            // già gestiti, sovrapponendosi a quella logica.
            EventManager.RegisterClassHandler(
                typeof(UIElement),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel),
                handledEventsToo: false);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta == 0 || e.Handled)
                return;

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return;

            // PreviewMouseWheel è un evento in tunneling: la route va dalla finestra fino
            // all'elemento sotto il mouse, e questo class handler viene invocato per OGNI
            // elemento attraversato. Eseguendo la logica a ogni invocazione, un singolo scatto
            // di rotellina poteva far scorrere lo stesso ScrollViewer una volta per livello di
            // profondità della UI: lo scorrimento risultava moltiplicato e la sua velocità
            // dipendeva da quanto era annidato l'elemento puntato.
            //
            // Agiamo quindi una volta sola, sull'ultimo elemento della route: quello davvero
            // sotto il puntatore. Così facendo giriamo anche DOPO gli handler di istanza degli
            // antenati, che mantengono la precedenza.
            if (!ReferenceEquals(sender, GetDeepestUIElement(e)))
                return;

            if (sender is not DependencyObject target)
                return;

            // Il ScrollViewer che contiene il punto cliccato si trova risalendo l'albero visuale.
            // La versione precedente lo cercava fra i DISCENDENTI dell'elemento corrente
            // (visita completa del sottoalbero, ripetuta per ogni livello della route): oltre a
            // costare moltissimo, partendo dalla finestra restituiva il primo ScrollViewer in
            // ordine di visita, che non è necessariamente quello sotto il mouse.
            ScrollViewer? inner = target as ScrollViewer ?? FindVisualParent<ScrollViewer>(target);
            if (inner == null)
                return;

            if (CanScrollVertically(inner, e.Delta))
            {
                // Il contenitore interno ha ancora margine: lasciamo fare a WPF, che scorrerà
                // da solo quando l'evento in bubbling raggiungerà il ScrollViewer.
                return;
            }

            ScrollViewer? outer = FindVisualParent<ScrollViewer>(inner);
            if (outer == null)
                return;

            e.Handled = true;
            outer.ScrollToVerticalOffset(outer.VerticalOffset - (e.Delta / ScrollStepDivisor));
        }

        /// <summary>
        /// True se <paramref name="scrollViewer"/> può ancora scorrere verticalmente nella
        /// direzione indicata dal delta della rotellina.
        /// </summary>
        private static bool CanScrollVertically(ScrollViewer scrollViewer, int delta)
        {
            if (scrollViewer.ScrollableHeight <= 0)
                return false;

            return delta < 0
                ? scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight
                : scrollViewer.VerticalOffset > 0;
        }

        /// <summary>
        /// Elemento più interno della route, cioè quello effettivamente sotto il puntatore.
        /// Se l'hit-test ha restituito un ContentElement (per esempio un <c>Run</c> dentro un
        /// TextBlock) questo non comparirà mai come <c>sender</c> di un class handler registrato
        /// su <see cref="UIElement"/>: si risale quindi al primo UIElement che lo contiene.
        /// </summary>
        private static DependencyObject? GetDeepestUIElement(MouseWheelEventArgs e)
        {
            if (e.OriginalSource is UIElement uiElement)
                return uiElement;

            DependencyObject? node = e.OriginalSource as DependencyObject;
            while (node != null && node is not UIElement)
            {
                node = node is FrameworkContentElement contentElement
                    ? contentElement.Parent
                    : LogicalTreeHelper.GetParent(node);
            }

            return node;
        }

        /// <summary>
        /// Primo discendente di tipo <typeparamref name="T"/> in visita pre-order.
        /// <para>
        /// Non è più usata dalla gestione della rotellina (che ora risale l'albero invece di
        /// scenderlo) ed è mantenuta solo come utility pubblica. La ricorsione è stata sostituita
        /// da uno stack esplicito preservando l'identico ordine di visita, quindi l'identico risultato.
        /// </para>
        /// </summary>
        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int rootCount = VisualTreeHelper.GetChildrenCount(parent);
            if (rootCount == 0)
                return null;

            // Inserendo i figli in ordine inverso, il pop successivo restituisce sempre
            // il figlio con indice più basso.
            var pending = new Stack<DependencyObject>();
            for (int i = rootCount - 1; i >= 0; i--)
                pending.Push(VisualTreeHelper.GetChild(parent, i));

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current is T match)
                    return match;

                int childCount = VisualTreeHelper.GetChildrenCount(current);
                for (int i = childCount - 1; i >= 0; i--)
                    pending.Push(VisualTreeHelper.GetChild(current, i));
            }

            return null;
        }

        public static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                var parent = VisualTreeHelper.GetParent(child);
                if (parent is T typedParent)
                    return typedParent;
                child = parent;
            }
            return null;
        }
    }
}
