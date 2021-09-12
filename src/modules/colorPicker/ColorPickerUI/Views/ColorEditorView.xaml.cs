// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Controls;
using ColorPicker.Helpers;

namespace ColorPicker.Views
{
    /// <summary>
    /// Interaction logic for ColorEditorView.xaml
    /// </summary>
    public partial class ColorEditorView : UserControl
    {
        public ColorEditorView() =>
            InitializeComponent();

        private void HistoryColors_ItemClick(object sender, ModernWpf.Controls.ItemClickEventArgs e)
        {
            // Note: it does not handle clicking on the same color.
            // More appropriate event would be SelectionChanged but we can not distinguish between user action and program action inside of it.
            SessionEventHelper.Event.EditorHistoryColorPicked = true;
        }
    }
}
