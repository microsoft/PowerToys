// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

using ColorPicker.Helpers;
using ColorPicker.ViewModelContracts;
using ColorPicker.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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

        // WinUI can duplicate value-type items in SelectedItems when selection is set before
        // the ListView loads. Snapshot distinct ARGB values so commands match the visible selection.
        internal static List<Color> GetDistinctSelectedColors(IList<object> items)
            => items.OfType<Color>().Distinct().ToList();

        internal static bool ShouldStartPicking(bool isTextInputFocused, bool isAdjustFlyoutOpen)
            => !isTextInputFocused && !isAdjustFlyoutOpen;

        private void HistoryContextFlyout_Opening(object sender, object e)
        {
            var selectedColors = GetDistinctSelectedColors(HistoryColors.SelectedItems);
            bool hasSelection = selectedColors.Count > 0;

            // MenuFlyout is detached from the visual tree in WinUI, so neither its inherited
            // DataContext nor an ElementName binding to HistoryColors is reliable. Refresh the
            // commands and their parameters every time the menu opens.
            if (DataContext is IColorEditorViewModel viewModel)
            {
                RemoveMenuItem.CommandParameter = selectedColors;
                ExportByColorMenuItem.CommandParameter = selectedColors;
                ExportByFormatMenuItem.CommandParameter = selectedColors;

                RemoveMenuItem.Command = viewModel.RemoveColorsCommand;
                ExportByColorMenuItem.Command = viewModel.ExportColorsGroupedByColorCommand;
                ExportByFormatMenuItem.Command = viewModel.ExportColorsGroupedByFormatCommand;
            }

            RemoveMenuItem.IsEnabled = hasSelection;
            ExportMenuItem.IsEnabled = hasSelection;
        }

        private void DeleteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;

            // Read the selection at key-press time; the context menu's CommandParameter can be stale.
            var selectedColors = GetDistinctSelectedColors(HistoryColors.SelectedItems);
            if (DataContext is IColorEditorViewModel viewModel && viewModel.RemoveColorsCommand.CanExecute(selectedColors))
            {
                viewModel.RemoveColorsCommand.Execute(selectedColors);

                // Removing the focused item moves focus out of the list; bring it back so Delete can repeat.
                HistoryColors.UpdateLayout();
                HistoryColors.Focus(FocusState.Keyboard);
            }
        }

        private void PickAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            bool isTextInputFocused = FocusManager.GetFocusedElement(XamlRoot) is TextBox;
            if (!ShouldStartPicking(isTextInputFocused, EditorState.BlockEscapeKeyClosingColorPickerEditor))
            {
                // Leave Handled false so a focused TextBox still receives the typed character.
                return;
            }

            args.Handled = true;
            if (DataContext is IColorEditorViewModel viewModel && viewModel.OpenColorPickerCommand.CanExecute(null))
            {
                viewModel.OpenColorPickerCommand.Execute(null);
            }
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
