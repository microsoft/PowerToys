// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Common.UI.Controls.Window;

namespace ColorPicker
{
    /// <summary>
    /// The picking-overlay tooltip window: hosts <see cref="Views.MainView"/> inside the
    /// shared <see cref="TransparentWindow"/> (transparent <c>TransparentTintBackdrop</c>,
    /// frameless, tool-window so it stays out of the taskbar/Alt-Tab, and a no-activate
    /// <c>Show()</c> so it never steals foreground from the user's work).
    /// </summary>
    /// <remarks>
    /// This replaces the deleted WPF root MainWindow (AllowsTransparency + WindowStyle=None +
    /// SizeToContent + Topmost). Cursor-follow positioning, content auto-sizing, and the
    /// MainViewModel / AppStateHandler wiring are added as the overlay's input layer and state
    /// machine are ported; until then this window is not yet shown from the App launch path.
    /// </remarks>
    public sealed partial class ColorPickerOverlayWindow : TransparentWindow
    {
        public ColorPickerOverlayWindow()
        {
            InitializeComponent();
        }
    }
}
