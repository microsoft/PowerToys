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

        /// <summary>
        /// Event handler for the "Add Zone" button click event.
        /// Adds a new zone to the canvas layout.
        /// </summary>
        private void OnAddZone(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Add zone");
            if (EditingLayout is CanvasLayoutModel canvas)
            {
                canvas.AddZone();
            }
        }

        /// <summary>
        /// Event handler for the "Cancel" button click event.
        /// Cancels the changes made in the editor and closes the window.
        /// </summary>
        protected new void OnCancel(object sender, RoutedEventArgs e)
        {
            Logger.LogInfo("Cancel changes");
            base.OnCancel(sender, e);
        }

        /// <summary>
        /// Event handler for the KeyUp event.
        /// Closes the editor window when the Escape key is pressed.
        /// </summary>
        private void CanvasEditorWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnCancel(sender, null);
            }
        }

        /// <summary>
        /// Event handler for the KeyDown event.
        /// Focuses the editor when the Ctrl+Tab key combination is pressed.
        /// </summary>
        private void CanvasEditorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)))
            {
                e.Handled = true;
                App.Overlay.FocusEditor();
            }
        }

        /// <summary>
        /// Event handler for the ContentRendered event.
        /// Fixes a WPF rendering bug when using custom chrome by invalidating the visual.
        /// </summary>
        private void EditorWindow_ContentRendered(object sender, System.EventArgs e)
        {
            InvalidateVisual();
        }
    }
}
