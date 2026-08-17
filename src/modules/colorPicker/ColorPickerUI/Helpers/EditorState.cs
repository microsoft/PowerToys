// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ColorPicker.Helpers
{
    /// <summary>
    /// Cross-cutting color-editor UI state shared between the color picker control (which sets
    /// the flag while the adjust-color flyout is open) and the global Esc handling in
    /// AppStateHandler (which reads it). Kept in its own static holder so the control does not
    /// depend on the window-coupled AppStateHandler.
    /// </summary>
    internal static class EditorState
    {
        /// <summary>
        /// Gets or sets a value indicating whether the Escape key should be blocked from
        /// closing the color picker editor (true while the adjust-color flyout is open).
        /// </summary>
        public static bool BlockEscapeKeyClosingColorPickerEditor { get; set; }
    }
}
