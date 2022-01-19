// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using System.Windows.Controls;
using ColorPicker.Helpers;

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
            ((INotifyCollectionChanged)HistoryColors.ItemsSource).CollectionChanged += new NotifyCollectionChangedEventHandler(HandleItemAdded);
        }

        private void HistoryColors_ItemClick(object sender, ModernWpf.Controls.ItemClickEventArgs e)
        {
            // Note: it does not handle clicking on the same color.
            // More appropriate event would be SelectionChanged but we can not distinguish between user action and program action inside of it.
            SessionEventHelper.Event.EditorHistoryColorPicked = true;
        }

        private void HandleItemAdded(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
            {
                ScrollHistoryToTop();
            }
        }

        private void ScrollHistoryToTop()
        {
            HistoryColors.ScrollIntoView(HistoryColors.Items[0]);
        }
    }
}
