// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

using ColorPicker.Helpers;
using ColorPicker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace ColorPicker.Views
{
    /// <summary>
    /// Interaction logic for ColorEditorView.xaml
    /// </summary>
    public sealed partial class ColorEditorView : UserControl
    {
        public ColorEditorView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void HistoryContextFlyout_Opening(object sender, object e)
        {
            bool hasSelection = HistoryColors.SelectedItems.Count > 0;
            RemoveMenuItem.IsEnabled = hasSelection;
            ExportMenuItem.IsEnabled = hasSelection;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnableHistoryColorsScrollIntoView();
        }

        /// <summary>
        /// Updating SelectedColorIndex will not refresh the ListView viewport. We listen for the
        /// SelectedColorIndex property change and, when a new color is added (value &lt;= 0), call
        /// ScrollIntoView so the ListView scrolls back to the start.
        /// </summary>
        private void EnableHistoryColorsScrollIntoView()
        {
            if (DataContext is not ColorEditorViewModel colorEditorViewModel)
            {
                return;
            }

            ((INotifyPropertyChanged)colorEditorViewModel).PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(colorEditorViewModel.SelectedColorIndex) && colorEditorViewModel.SelectedColorIndex <= 0)
                {
                    HistoryColors.ScrollIntoView(colorEditorViewModel.SelectedColor);
                }
            };
        }

        /// <summary>
        /// Scrolls the history ListView horizontally on mouse wheel. WinUI has no MouseWheel; use
        /// PointerWheelChanged and the wheel delta from the pointer point.
        /// </summary>
        private void HistoryColors_OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(HistoryColors);
            if (scrollViewer != null)
            {
                var delta = e.GetCurrentPoint(HistoryColors).Properties.MouseWheelDelta;

                // Positive delta scrolls left (towards the most-recent color), matching the WPF behavior.
                scrollViewer.ChangeView(scrollViewer.HorizontalOffset - delta, null, null);
                e.Handled = true;
            }
        }

        private static T FindVisualChild<T>(DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T tChild)
                {
                    return tChild;
                }

                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }

            return null;
        }
    }
}
