// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    internal static class TextReplacementMappingReconciler
    {
        // The native table is keyed by trigger text. Treat it as authoritative for active rows,
        // while retaining native-missing editor rows as inactive recovery data.
        internal static bool Reconcile(EditorSettings editorSettings, IReadOnlyList<TextReplacement> nativeMappings)
        {
            bool changed = false;

            foreach (TextReplacement nativeMapping in nativeMappings)
            {
                List<ShortcutSettings> matches = editorSettings.ShortcutSettingsDictionary.Values
                    .Where(settings => IsTextReplacementWithTrigger(settings, nativeMapping.Trigger))
                    .ToList();

                if (matches.Count == 0)
                {
                    AddTextReplacement(editorSettings, CreateTextReplacementMapping(nativeMapping));
                    changed = true;
                    continue;
                }

                ShortcutSettings canonical = SelectCanonical(editorSettings, matches, nativeMapping);
                ShortcutKeyMapping authoritativeMapping = CreateTextReplacementMapping(nativeMapping);
                if (!canonical.Shortcut.Equals(authoritativeMapping))
                {
                    canonical.Shortcut = authoritativeMapping;
                    changed = true;
                }

                foreach (ShortcutSettings duplicate in matches.Where(settings => !ReferenceEquals(settings, canonical)))
                {
                    MergeProfilesAndRemoveDuplicate(editorSettings, canonical, duplicate);
                    changed = true;
                }

                changed = EnsureCanonicalReferences(editorSettings, canonical) || changed;
            }

            // Repair duplicates left by an older editor even if their native mapping was deleted.
            List<IGrouping<string, ShortcutSettings>> duplicateGroups = editorSettings.ShortcutSettingsDictionary.Values
                .Where(IsTextReplacement)
                .GroupBy(settings => settings.Shortcut.TriggerText, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .ToList();
            foreach (IGrouping<string, ShortcutSettings> group in duplicateGroups)
            {
                ShortcutSettings canonical = SelectCanonical(editorSettings, group, nativeMapping: null);
                foreach (ShortcutSettings duplicate in group.Where(settings => !ReferenceEquals(settings, canonical)).ToList())
                {
                    MergeProfilesAndRemoveDuplicate(editorSettings, canonical, duplicate);
                    changed = true;
                }

                changed = EnsureCanonicalReferences(editorSettings, canonical) || changed;
            }

            var nativeTriggers = nativeMappings.Select(mapping => mapping.Trigger).ToHashSet(StringComparer.Ordinal);
            foreach (ShortcutSettings cachedMapping in editorSettings.ShortcutSettingsDictionary.Values.Where(IsTextReplacement))
            {
                bool isActive = nativeTriggers.Contains(cachedMapping.Shortcut.TriggerText);
                if (cachedMapping.IsActive != isActive)
                {
                    cachedMapping.IsActive = isActive;
                    changed = true;
                }
            }

            return changed;
        }

        private static ShortcutKeyMapping CreateTextReplacementMapping(TextReplacement mapping)
        {
            return new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapText,
                TriggerText = mapping.Trigger,
                TriggerKey = mapping.TriggerKey,
                TargetKeys = mapping.TargetText,
                TargetText = mapping.TargetText,
            };
        }

        private static bool IsTextReplacement(ShortcutSettings settings)
        {
            return settings.Shortcut.OperationType == ShortcutOperationType.RemapText &&
                   !string.IsNullOrEmpty(settings.Shortcut.TriggerText);
        }

        private static bool IsTextReplacementWithTrigger(ShortcutSettings settings, string trigger)
        {
            return IsTextReplacement(settings) &&
                   string.Equals(settings.Shortcut.TriggerText, trigger, StringComparison.Ordinal);
        }

        private static ShortcutSettings SelectCanonical(
            EditorSettings settings,
            IEnumerable<ShortcutSettings> candidates,
            TextReplacement? nativeMapping)
        {
            settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out List<string>? operationIds);
            return candidates
                .OrderBy(candidate => IsExactNativeMatch(candidate, nativeMapping) ? 0 : 1)
                .ThenBy(candidate => candidate.IsActive ? 0 : 1)
                .ThenBy(candidate => GetOperationIndex(operationIds, candidate.Id))
                .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
                .First();
        }

        private static bool IsExactNativeMatch(ShortcutSettings candidate, TextReplacement? nativeMapping)
        {
            return nativeMapping is not null &&
                   candidate.Shortcut.TriggerKey == nativeMapping.TriggerKey &&
                   string.Equals(candidate.Shortcut.TargetText, nativeMapping.TargetText, StringComparison.Ordinal);
        }

        private static int GetOperationIndex(List<string>? operationIds, string id)
        {
            int index = operationIds?.FindIndex(candidate => string.Equals(candidate, id, StringComparison.Ordinal)) ?? -1;
            return index >= 0 ? index : int.MaxValue;
        }

        private static void AddTextReplacement(EditorSettings settings, ShortcutKeyMapping mapping)
        {
            string id = Guid.NewGuid().ToString();
            settings.ShortcutSettingsDictionary[id] = new ShortcutSettings
            {
                Id = id,
                Shortcut = mapping,
                IsActive = true,
            };

            if (!settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out List<string>? operationMappingIds))
            {
                operationMappingIds = new List<string>();
                settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = operationMappingIds;
            }

            operationMappingIds.Add(id);
        }

        private static void MergeProfilesAndRemoveDuplicate(EditorSettings settings, ShortcutSettings canonical, ShortcutSettings duplicate)
        {
            foreach (string profile in duplicate.Profiles)
            {
                if (!canonical.Profiles.Contains(profile, StringComparer.Ordinal))
                {
                    canonical.Profiles.Add(profile);
                }

                if (!settings.ProfileDictionary.TryGetValue(profile, out List<string>? profileIds))
                {
                    profileIds = new List<string>();
                    settings.ProfileDictionary[profile] = profileIds;
                }

                ReplaceIdInPlace(profileIds, canonical.Id, duplicate.Id, ensureCanonical: true);
            }

            foreach (KeyValuePair<string, List<string>> profile in settings.ProfileDictionary)
            {
                bool containedDuplicate = profile.Value.Contains(duplicate.Id, StringComparer.Ordinal);
                bool containedCanonical = profile.Value.Contains(canonical.Id, StringComparer.Ordinal);
                ReplaceIdInPlace(profile.Value, canonical.Id, duplicate.Id, ensureCanonical: false);
                if ((containedDuplicate || containedCanonical) && !canonical.Profiles.Contains(profile.Key, StringComparer.Ordinal))
                {
                    canonical.Profiles.Add(profile.Key);
                }
            }

            foreach (KeyValuePair<ShortcutOperationType, List<string>> operation in settings.ShortcutsByOperationType)
            {
                if (operation.Key != ShortcutOperationType.RemapText)
                {
                    operation.Value.RemoveAll(id =>
                        string.Equals(id, duplicate.Id, StringComparison.Ordinal) ||
                        string.Equals(id, canonical.Id, StringComparison.Ordinal));
                }
            }

            if (!settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out List<string>? textReplacementIds))
            {
                textReplacementIds = new List<string>();
                settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = textReplacementIds;
            }

            ReplaceIdInPlace(textReplacementIds, canonical.Id, duplicate.Id, ensureCanonical: true);
            settings.ShortcutSettingsDictionary.Remove(duplicate.Id);
        }

        private static bool EnsureCanonicalReferences(EditorSettings settings, ShortcutSettings canonical)
        {
            bool changed = false;
            foreach (KeyValuePair<ShortcutOperationType, List<string>> operation in settings.ShortcutsByOperationType)
            {
                if (operation.Key != ShortcutOperationType.RemapText)
                {
                    changed = operation.Value.RemoveAll(id => string.Equals(id, canonical.Id, StringComparison.Ordinal)) > 0 || changed;
                }
            }

            if (!settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out List<string>? textReplacementIds))
            {
                textReplacementIds = new List<string>();
                settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = textReplacementIds;
                changed = true;
            }

            changed = ReplaceIdInPlace(textReplacementIds, canonical.Id, canonical.Id, ensureCanonical: true) || changed;

            foreach (string profileName in canonical.Profiles.ToList())
            {
                if (!settings.ProfileDictionary.TryGetValue(profileName, out List<string>? profileIds))
                {
                    profileIds = new List<string>();
                    settings.ProfileDictionary[profileName] = profileIds;
                    changed = true;
                }

                changed = ReplaceIdInPlace(profileIds, canonical.Id, canonical.Id, ensureCanonical: true) || changed;
            }

            foreach (KeyValuePair<string, List<string>> profile in settings.ProfileDictionary)
            {
                if (profile.Value.Contains(canonical.Id, StringComparer.Ordinal) &&
                    !canonical.Profiles.Contains(profile.Key, StringComparer.Ordinal))
                {
                    canonical.Profiles.Add(profile.Key);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool ReplaceIdInPlace(List<string> ids, string canonicalId, string duplicateId, bool ensureCanonical)
        {
            List<string> originalIds = ids.ToList();
            int insertionIndex = -1;
            for (int index = 0; index < ids.Count; ++index)
            {
                if ((string.Equals(ids[index], canonicalId, StringComparison.Ordinal) ||
                     string.Equals(ids[index], duplicateId, StringComparison.Ordinal)) &&
                    insertionIndex < 0)
                {
                    insertionIndex = index;
                }
            }

            ids.RemoveAll(id =>
                string.Equals(id, canonicalId, StringComparison.Ordinal) ||
                string.Equals(id, duplicateId, StringComparison.Ordinal));

            if (insertionIndex >= 0)
            {
                ids.Insert(Math.Min(insertionIndex, ids.Count), canonicalId);
            }
            else if (ensureCanonical)
            {
                ids.Add(canonicalId);
            }

            return !originalIds.SequenceEqual(ids, StringComparer.Ordinal);
        }
    }
}
