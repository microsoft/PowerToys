// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Input;
using FancyZonesEditor.Models;

namespace FancyZonesEditor
{
    public partial class CanvasEditorWindow : EditorWindow
    {
        private CanvasLayoutModel _model;
        private CanvasLayoutModel _stashedModel;

        public CanvasEditorWindow()
        {
            InitializeComponent();

            KeyUp += CanvasEditorWindow_KeyUp;
            KeyDown += CanvasEditorWindow_KeyDown;

            _model = App.Overlay.CurrentDataContext as CanvasLayoutModel;
            _stashedModel = (CanvasLayoutModel)_model.Clone();
        }

        public LayoutModel Model
        {
            get
            {
                return _model;
            }
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            _model.AddZone();
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            base.OnCancel(sender, e);
            _stashedModel.RestoreTo(_model);
        }

        private void CanvasEditorWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCancel(sender, null);
            }
        }

        private void CanvasEditorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                e.Handled = true;
                App.Overlay.FocusEditor();
            }
        }

        // This is required to fix a WPF rendering bug when using custom chrome
        private void EditorWindow_ContentRendered(object sender, System.EventArgs e)
        {
            InvalidateVisual();
        }
    }
}
