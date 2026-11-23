// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>Outcome of saving the add/edit remapping dialog.</summary>
    internal enum MappingSaveResult
    {
        Saved,
        Failed,

        /// <summary>
        /// Another profile became active after the dialog opened. Nothing was written; the dialog
        /// tells the user which profile it was opened for.
        /// </summary>
        ProfileChanged,
    }

    /// <summary>
    /// The add/edit remapping dialog saves only into the profile it was opened for. The active
    /// profile can change while it is open (another editor, the cycle hotkey, device auto-switch),
    /// and the editor then rebuilds itself for the new profile. Kept free of UI and native
    /// dependencies so the rule can be unit tested.
    /// </summary>
    internal static class MappingDialogTarget
    {
        /// <summary>
        /// Gets whether the dialog's changes may still be saved: both the profile the editor is
        /// showing and the profile the engine has active must still be the one captured when the
        /// dialog opened. The second check covers the moment after the engine switched and before
        /// the editor followed (its file watcher is debounced).
        /// </summary>
        internal static bool CanSave(string? targetAtOpen, string? editorProfile, string? liveProfile) =>
            !string.IsNullOrWhiteSpace(targetAtOpen) &&
            string.Equals(targetAtOpen, editorProfile, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(targetAtOpen, liveProfile, StringComparison.OrdinalIgnoreCase);
    }
}
