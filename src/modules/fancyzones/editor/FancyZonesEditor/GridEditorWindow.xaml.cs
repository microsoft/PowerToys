// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public partial class GridEditorWindow : EditorWindow
    {
        public GridEditorWindow()
        {
            InitializeComponent();

            KeyUp += GridEditorWindow_KeyUp;
            KeyDown += ((App)Application.Current).App_KeyDown;

            _stashedModel = (GridLayoutModel)(App.Overlay.CurrentDataContext as GridLayoutModel).Clone();
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
            GridLayoutModel model = App.Overlay.CurrentDataContext as GridLayoutModel;
            _stashedModel.RestoreTo(model);
        }

        private void GridEditorWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCancel(sender, null);
            }

            ((App)Application.Current).App_KeyUp(sender, e);
        }

        private GridLayoutModel _stashedModel;

        // This is required to fix a WPF rendering bug when using custom chrome
        private void EditorWindow_ContentRendered(object sender, System.EventArgs e)
        {
            InvalidateVisual();
        }
    }
}
