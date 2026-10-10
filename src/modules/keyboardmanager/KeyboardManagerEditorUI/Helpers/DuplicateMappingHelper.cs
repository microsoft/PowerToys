// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;

namespace KeyboardManagerEditorUI.Helpers
{
    /// <summary>
    /// Duplicate detection shared by the editor dialog and the save transaction. Kept free of UI and
    /// native dependencies (shortcut comparison is injected) so it can be unit tested.
    /// </summary>
    internal static class DuplicateMappingHelper
    {
        /// <summary>
        /// Global mappings (null/empty target app) and each app's mappings live in separate engine tables,
        /// and app-specific mappings take precedence over global ones. A trigger therefore only conflicts
        /// with another mapping in the same scope. The engine lowercases app names, so compare ignoring case.
        /// </summary>
        internal static bool IsSameAppScope(string? first, string? second) =>
            string.Equals(first ?? string.Empty, second ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        internal static bool IsDuplicateMapping(
            IEnumerable<KeyValuePair<string, ShortcutSettings>> entries,
            string shortcutKeysString,
            bool isEditMode,
            string? appName,
            string? editingId,
            Func<string, string, bool> areShortcutsEqual)
        {
            // Only rows that are active belong to the current profile's engine configuration;
            // inactive ones are retained metadata for other profiles and must not block an edit.
            int matches = entries
                .Where(kvp => kvp.Value.IsActive)
                .Where(kvp => editingId == null || kvp.Key != editingId)
                .Count(kvp => areShortcutsEqual(kvp.Value.Shortcut.OriginalKeys, shortcutKeysString) &&
                              IsSameAppScope(kvp.Value.Shortcut.TargetApp, appName));

            // With the edited row's identity we exclude exactly that row above, so any remaining match is
            // a genuine duplicate against a *different* row. Without it, fall back to the old tolerance
            // (edit mode may still match its own not-yet-excluded row once).
            int upperLimit = editingId != null ? 0 : (isEditMode ? 1 : 0);
            return matches > upperLimit;
        }

        internal static bool HasDuplicateEditorMapping(
            IEnumerable<KeyValuePair<string, ShortcutSettings>> entries,
            ShortcutKeyMapping replacementMapping,
            string? replacingId,
            Func<string, string, bool> areShortcutsEqual) =>
            entries.Any(entry =>
                entry.Value.IsActive &&
                !entry.Key.Equals(replacingId, StringComparison.OrdinalIgnoreCase) &&
                areShortcutsEqual(entry.Value.Shortcut.OriginalKeys, replacementMapping.OriginalKeys) &&

                // An Always and an Alone remap of the same key are distinct (separate engine tables),
                // so only treat it as a duplicate when the condition matches too.
                entry.Value.Shortcut.Condition == replacementMapping.Condition &&
                IsSameAppScope(entry.Value.Shortcut.TargetApp, replacementMapping.TargetApp));
    }
}
