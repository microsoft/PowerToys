// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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

        /*
        private void HistoryColors_ItemClick(object sender, ItemClickEventArgs e)
        {
            // Note: it does not handle clicking on the same color.
            // More appropriate event would be SelectionChanged but we can not distinguish between user action and program action inside of it.
            SessionEventHelper.Event.EditorHistoryColorPicked = true;
        }
        */
    }
}
