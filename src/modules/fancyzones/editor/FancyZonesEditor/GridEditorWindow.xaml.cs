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
        public GridEditorWindow(GridLayoutModel model)
            : base(model)
        {
            InitializeComponent();

            KeyUp += GridEditorWindow_KeyUp;
            KeyDown += ((App)Application.Current).App_KeyDown;
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
        }

        private void GridEditorWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCancel(sender, null);
            }

            ((App)Application.Current).App_KeyUp(sender, e);
        }

        // This is required to fix a WPF rendering bug when using custom chrome
        private void EditorWindow_ContentRendered(object sender, System.EventArgs e)
        {
            InvalidateVisual();
        }
    }
}
