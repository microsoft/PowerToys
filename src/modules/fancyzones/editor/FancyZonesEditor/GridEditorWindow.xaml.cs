// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    /// <summary>
    /// Interaction logic for Window2.xaml
    /// </summary>
    public partial class GridEditorWindow : EditorWindow
    {
        public GridEditorWindow()
        {
            InitializeComponent();
            _stashedModel = (GridLayoutModel)(EditorOverlay.Current.DataContext as GridLayoutModel).Clone();
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
            GridLayoutModel model = EditorOverlay.Current.DataContext as GridLayoutModel;
            _stashedModel.RestoreTo(model);
        }

        private GridLayoutModel _stashedModel;
    }
}
