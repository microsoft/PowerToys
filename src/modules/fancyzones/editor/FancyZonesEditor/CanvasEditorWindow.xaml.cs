// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows;
using System.Windows.Input;
using FancyZonesEditor.Models;
using ManagedCommon;

namespace FancyZonesEditor
{
    public partial class CanvasEditorWindow : EditorWindow
    {
        public CanvasEditorWindow(CanvasLayoutModel layout)
            : base(layout)
        {
            InitializeComponent();

            KeyUp += CanvasEditorWindow_KeyUp;
            KeyDown += CanvasEditorWindow_KeyDown;
        }

        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Add zone");
            if (EditingLayout is CanvasLayoutModel canvas)
            {
                canvas.AddZone();
            }
        }

        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Cancel changes");
            base.OnCancel(sender, e);
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
