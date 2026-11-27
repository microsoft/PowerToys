// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>What the editor does about the active profile.</summary>
    internal enum ProfileReloadAction
    {
        /// <summary>Nothing: the editor already shows the right profile, or cannot tell yet.</summary>
        None,

        /// <summary>Load and show the profile that is active on disk. Nothing is written.</summary>
        ShowActive,

        /// <summary>Make the selected profile the active one, then load and show it.</summary>
        SwitchTo,
    }

    /// <summary>
    /// Decides whether the editor has to load another profile. The profile the editor has LOADED
    /// (its mapping service and rows) is what counts, never what the picker happens to show: the
    /// picker can already show a profile whose rows are not loaded (the user picked the profile the
    /// engine had just switched to, or an earlier load failed), and comparing with the picker would
    /// then leave picker and rows different for good. Kept free of UI so it can be unit tested.
    /// </summary>
    internal static class ProfileReloadDecision
    {
        /// <summary>
        /// settings.json changed on disk. <paramref name="activeOnDisk"/> is null when it could not
        /// be read right now; the caller then tries again later instead of acting on a guess.
        /// </summary>
        internal static ProfileReloadAction OnDiskChange(string? loadedProfile, string? activeOnDisk)
        {
            if (activeOnDisk == null || Same(loadedProfile, activeOnDisk))
            {
                return ProfileReloadAction.None;
            }

            return ProfileReloadAction.ShowActive;
        }

        /// <summary>The user selected <paramref name="selectedProfile"/> in the picker.</summary>
        internal static ProfileReloadAction OnPickerSelection(string? loadedProfile, string selectedProfile, string? activeOnDisk)
        {
            if (Same(loadedProfile, selectedProfile))
            {
                return ProfileReloadAction.None;
            }

            // Already active on disk (the engine switched to it a moment ago): only the editor is behind.
            return activeOnDisk != null && Same(selectedProfile, activeOnDisk)
                ? ProfileReloadAction.ShowActive
                : ProfileReloadAction.SwitchTo;
        }

        private static bool Same(string? left, string? right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
