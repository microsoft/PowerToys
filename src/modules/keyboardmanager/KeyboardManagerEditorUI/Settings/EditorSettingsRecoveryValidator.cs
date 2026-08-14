// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    internal static class EditorSettingsRecoveryValidator
    {
        internal static bool IsValid(EditorSettings settings)
        {
            if (settings.ShortcutSettingsDictionary is null ||
                settings.ShortcutSettingsDictionary.Count == 0 ||
                settings.ShortcutsByOperationType is null ||
                settings.ProfileDictionary is null)
            {
                return false;
            }

            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                ShortcutSettings shortcutSettings = entry.Value;
                if (shortcutSettings is null ||
                    shortcutSettings.Shortcut is null ||
                    shortcutSettings.Profiles is null ||
                    string.IsNullOrEmpty(entry.Key) ||
                    !string.Equals(entry.Key, shortcutSettings.Id, StringComparison.Ordinal) ||
                    !settings.ShortcutsByOperationType.TryGetValue(shortcutSettings.Shortcut.OperationType, out List<string>? operationIds) ||
                    operationIds is null ||
                    operationIds.Count(id => string.Equals(id, entry.Key, StringComparison.Ordinal)) != 1)
                {
                    return false;
                }

                foreach (string profileName in shortcutSettings.Profiles)
                {
                    if (!settings.ProfileDictionary.TryGetValue(profileName, out List<string>? profileIds) ||
                        profileIds is null ||
                        profileIds.Count(id => string.Equals(id, entry.Key, StringComparison.Ordinal)) != 1)
                    {
                        return false;
                    }
                }
            }

            foreach (KeyValuePair<ShortcutOperationType, List<string>> operation in settings.ShortcutsByOperationType)
            {
                if (operation.Value is null || operation.Value.Count != operation.Value.Distinct(StringComparer.Ordinal).Count())
                {
                    return false;
                }

                foreach (string id in operation.Value)
                {
                    if (!settings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                        shortcutSettings is null ||
                        shortcutSettings.Shortcut.OperationType != operation.Key)
                    {
                        return false;
                    }
                }
            }

            foreach (KeyValuePair<string, List<string>> profile in settings.ProfileDictionary)
            {
                if (profile.Value is null || profile.Value.Count != profile.Value.Distinct(StringComparer.Ordinal).Count())
                {
                    return false;
                }

                foreach (string id in profile.Value)
                {
                    if (!settings.ShortcutSettingsDictionary.TryGetValue(id, out ShortcutSettings? shortcutSettings) ||
                        shortcutSettings is null ||
                        !shortcutSettings.Profiles.Contains(profile.Key, StringComparer.Ordinal))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
