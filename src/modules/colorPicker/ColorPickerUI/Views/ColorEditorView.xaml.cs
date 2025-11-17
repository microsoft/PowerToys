// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using ColorPicker.Helpers;
using ColorPicker.ViewModels;

namespace ColorPicker.Views
{
    /// <summary>
    /// Interaction logic for ColorEditorView.xaml
    /// </summary>
    public partial class ColorEditorView : UserControl
    {
        public ColorEditorView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            EnableHistoryColorsScrollIntoView();
        }

        /// <summary>
        /// Updating SelectedColorIndex will not refresh ListView viewport.
        /// We listen for SelectedColorIndex property change and in case of a value <= 0 (new color is added) a call to ScrollIntoView is made so ListView will scroll up.
        /// </summary>
        private void EnableHistoryColorsScrollIntoView()
        {
            ColorEditorViewModel colorEditorViewModel = (ColorEditorViewModel)this.DataContext;
            ((INotifyPropertyChanged)colorEditorViewModel).PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(colorEditorViewModel.SelectedColorIndex) && colorEditorViewModel.SelectedColorIndex <= 0)
                {
                    HistoryColors.ScrollIntoView(colorEditorViewModel.SelectedColor);
                }
            };
        }

        /// <summary>
        /// Handles the mouse wheel scroll event on the HistoryColors ListView.
        /// Scrolls the ListView horizontally based on the direction of the mouse wheel scroll.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse wheel event data.</param>
        private void HistoryColors_OnMouseWheelScroll(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(HistoryColors);

            if (scrollViewer != null)
            {
                if (e.Delta > 0)
                {
                    scrollViewer.LineLeft();
                }
                else
                {
                    scrollViewer.LineRight();
                }

                e.Handled = true;
            }
        }

        /// <summary>
        /// Finds a visual child of a specified type within a given dependency object.
        /// </summary>
        /// <typeparam name="T">The type of the child element to find.</typeparam>
        /// <param name="obj">The parent dependency object.</param>
        /// <returns>The first child element of the specified type, or null if no such element is found.</returns>
        private static T FindVisualChild<T>(DependencyObject obj)
            where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T tChild)
                {
                    return tChild;
                }
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                    {
                        return childOfChild;
                    }
                }
            }

            return null;
        }

        /*
        private void HistoryColors_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Note: it does not handle clicking on the same color.
            // More appropriate event would be SelectionChanged but we cannot distinguish between user action and program action inside of it.
            SessionEventHelper.Event.EditorHistoryColorPicked = true;
        }
        */
    }
}
