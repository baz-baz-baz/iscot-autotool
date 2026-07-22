using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PersonalAutomationTool.Core
{
    public static class MouseWheelScrollBehavior
    {
        public static void InitializeGlobalMouseWheelHandler()
        {
            EventManager.RegisterClassHandler(
                typeof(UIElement),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnPreviewMouseWheel),
                handledEventsToo: true);
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                return;

            if (sender is not DependencyObject depObj)
                return;

            ScrollViewer? scv = depObj as ScrollViewer ?? FindVisualChild<ScrollViewer>(depObj);

            if (scv != null)
            {
                bool isScrollingDown = e.Delta < 0;
                bool isScrollingUp = e.Delta > 0;

                bool cannotScrollDown = isScrollingDown && (scv.VerticalOffset >= scv.ScrollableHeight || scv.ScrollableHeight == 0);
                bool cannotScrollUp = isScrollingUp && (scv.VerticalOffset <= 0 || scv.ScrollableHeight == 0);

                if (cannotScrollDown || cannotScrollUp)
                {
                    ScrollViewer? parentScv = FindVisualParent<ScrollViewer>(scv);
                    if (parentScv != null)
                    {
                        e.Handled = true;
                        parentScv.ScrollToVerticalOffset(parentScv.VerticalOffset - (e.Delta / 3.0));
                    }
                }
            }
            else if (depObj is FrameworkElement fe)
            {
                ScrollViewer? parentScv = FindVisualParent<ScrollViewer>(fe);
                if (parentScv != null)
                {
                    e.Handled = true;
                    parentScv.ScrollToVerticalOffset(parentScv.VerticalOffset - (e.Delta / 3.0));
                }
            }
        }

        public static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T tChild)
                    return tChild;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
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
