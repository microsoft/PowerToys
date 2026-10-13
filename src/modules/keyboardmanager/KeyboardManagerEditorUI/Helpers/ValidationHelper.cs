// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;
using ManagedCommon;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class ValidationHelper
    {
        public static readonly Dictionary<ValidationErrorType, (string Title, string Message)> ValidationMessages = new()
        {
            { ValidationErrorType.EmptyOriginalKeys, (ResourceHelper.GetString("Validation_EmptyOriginalKeys_Title"), ResourceHelper.GetString("Validation_EmptyOriginalKeys_Message")) },
            { ValidationErrorType.EmptyRemappedKeys, (ResourceHelper.GetString("Validation_EmptyRemappedKeys_Title"), ResourceHelper.GetString("Validation_EmptyRemappedKeys_Message")) },
            { ValidationErrorType.ModifierOnly, (ResourceHelper.GetString("Validation_ModifierOnly_Title"), ResourceHelper.GetString("Validation_ModifierOnly_Message")) },
            { ValidationErrorType.EmptyAppName, (ResourceHelper.GetString("Validation_EmptyAppName_Title"), ResourceHelper.GetString("Validation_EmptyAppName_Message")) },
            { ValidationErrorType.IllegalShortcut, (ResourceHelper.GetString("Validation_IllegalShortcut_Title"), ResourceHelper.GetString("Validation_IllegalShortcut_Message")) },
            { ValidationErrorType.DuplicateMapping, (ResourceHelper.GetString("Validation_DuplicateMapping_Title"), ResourceHelper.GetString("Validation_DuplicateMapping_Message")) },
            { ValidationErrorType.ConflictingModifier, (ResourceHelper.GetString("Validation_ConflictingModifier_Title"), ResourceHelper.GetString("Validation_ConflictingModifier_Message")) },
            { ValidationErrorType.SelfMapping, (ResourceHelper.GetString("Validation_SelfMapping_Title"), ResourceHelper.GetString("Validation_SelfMapping_Message")) },
            { ValidationErrorType.EmptyTargetText, (ResourceHelper.GetString("Validation_EmptyTargetText_Title"), ResourceHelper.GetString("Validation_EmptyTargetText_Message")) },
            { ValidationErrorType.EmptyUrl, (ResourceHelper.GetString("Validation_EmptyUrl_Title"), ResourceHelper.GetString("Validation_EmptyUrl_Message")) },
            { ValidationErrorType.EmptyProgramPath, (ResourceHelper.GetString("Validation_EmptyProgramPath_Title"), ResourceHelper.GetString("Validation_EmptyProgramPath_Message")) },
            { ValidationErrorType.OneKeyMapping, (ResourceHelper.GetString("Validation_OneKeyMapping_Title"), ResourceHelper.GetString("Validation_OneKeyMapping_Message")) },
            { ValidationErrorType.ShortcutMissingModifier, (ResourceHelper.GetString("Validation_ShortcutMissingModifier_Title"), ResourceHelper.GetString("Validation_ShortcutMissingModifier_Message")) },
        };

        public static ValidationErrorType ValidateKeyMapping(
            List<string> originalKeys,
            List<string> remappedKeys,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null,
            SingleKeyRemapCondition condition = SingleKeyRemapCondition.Always)
        {
            if (originalKeys == null || originalKeys.Count == 0)
            {
                return ValidationErrorType.EmptyOriginalKeys;
            }

            if (remappedKeys == null || remappedKeys.Count == 0)
            {
                return ValidationErrorType.EmptyRemappedKeys;
            }

            if ((originalKeys.Count > 1 && ContainsOnlyModifierKeys(originalKeys)) ||
                (remappedKeys.Count > 1 && ContainsOnlyModifierKeys(remappedKeys)))
            {
                return ValidationErrorType.ModifierOnly;
            }

            if ((originalKeys.Count > 1 && !ContainsModifierKey(originalKeys)) ||
                (remappedKeys.Count > 1 && !ContainsModifierKey(remappedKeys)))
            {
                return ValidationErrorType.ShortcutMissingModifier;
            }

            if (originalKeys.Count > 1 && isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            if (originalKeys.Count > 1 && IsIllegalShortcut(originalKeys, mappingService))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            // The classic editor runs the illegal-shortcut check on whichever column is being
            // edited, so a reserved combination is rejected as a target too. Windows intercepts
            // Win+L and Ctrl+Alt+Del before the engine can synthesize them, so remapping *to* one
            // produces a mapping that silently never works.
            if (remappedKeys.Count > 1 && IsIllegalShortcut(remappedKeys, mappingService))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService, appName, editingId, condition))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (originalKeys.Count == 1 && HasConflictingModifierMapping(originalKeys[0], isEditMode, editingId, condition))
            {
                return ValidationErrorType.ConflictingModifier;
            }

            if (originalKeys.Count > 1 && HasOverlappingShortcut(originalKeys, mappingService, appName, editingId))
            {
                return ValidationErrorType.ConflictingModifier;
            }

            if (IsSelfMapping(originalKeys, remappedKeys, mappingService))
            {
                return ValidationErrorType.SelfMapping;
            }

            return ValidationErrorType.NoError;
        }

        public static ValidationErrorType ValidateDisableMapping(
            List<string> originalKeys,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null,
            SingleKeyRemapCondition condition = SingleKeyRemapCondition.Always)
        {
            if (originalKeys == null || originalKeys.Count == 0)
            {
                return ValidationErrorType.EmptyOriginalKeys;
            }

            if (originalKeys.Count > 1 && ContainsOnlyModifierKeys(originalKeys))
            {
                return ValidationErrorType.ModifierOnly;
            }

            if (originalKeys.Count > 1 && !ContainsModifierKey(originalKeys))
            {
                return ValidationErrorType.ShortcutMissingModifier;
            }

            if (originalKeys.Count > 1 && isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            if (originalKeys.Count > 1 && IsIllegalShortcut(originalKeys, mappingService))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService, appName, editingId, condition))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (originalKeys.Count == 1 && HasConflictingModifierMapping(originalKeys[0], isEditMode, editingId, condition))
            {
                return ValidationErrorType.ConflictingModifier;
            }

            return ValidationErrorType.NoError;
        }

        public static ValidationErrorType ValidateTextMapping(
            List<string> keys,
            string textContent,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null)
        {
            if (keys == null || keys.Count == 0)
            {
                return ValidationErrorType.EmptyOriginalKeys;
            }

            if (string.IsNullOrEmpty(textContent))
            {
                return ValidationErrorType.EmptyTargetText;
            }

            if (keys.Count > 1 && ContainsOnlyModifierKeys(keys))
            {
                return ValidationErrorType.ModifierOnly;
            }

            if (keys.Count > 1 && !ContainsModifierKey(keys))
            {
                return ValidationErrorType.ShortcutMissingModifier;
            }

            if (keys.Count > 1 && isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            if (keys.Count > 1 && IsIllegalShortcut(keys, mappingService))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            if (IsDuplicateMapping(keys, isEditMode, mappingService, appName, editingId))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            return ValidationErrorType.NoError;
        }

        public static ValidationErrorType ValidateUrlMapping(
            List<string> originalKeys,
            string url,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ValidationErrorType.EmptyUrl;
            }

            return ValidateProgramOrUrlMapping(originalKeys, isAppSpecific, appName, mappingService, isEditMode, editingId);
        }

        public static ValidationErrorType ValidateAppMapping(
            List<string> originalKeys,
            string programPath,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null)
        {
            if (string.IsNullOrWhiteSpace(programPath))
            {
                return ValidationErrorType.EmptyProgramPath;
            }

            return ValidateProgramOrUrlMapping(originalKeys, isAppSpecific, appName, mappingService, isEditMode, editingId);
        }

        public static bool IsDuplicateMapping(
            List<string> keys,
            bool isEditMode,
            KeyboardMappingService mappingService,
            string appName,
            string? editingId = null,
            SingleKeyRemapCondition condition = SingleKeyRemapCondition.Always)
        {
            string shortcutKeysString = BuildKeyCodeString(keys, mappingService);
            string targetApp = appName ?? string.Empty;

            // Only active rows belong to the engine configuration. Always and Alone occupy
            // separate single-key tables, and global shortcuts may have app-specific overrides.
            int matches = SettingsManager.EditorSettings.ShortcutSettingsDictionary
                .Where(entry => entry.Value.IsActive)
                .Where(entry => !string.Equals(entry.Key, editingId, StringComparison.OrdinalIgnoreCase))
                .Count(entry => KeyboardManagerInterop.AreShortcutsEqual(entry.Value.Shortcut.OriginalKeys, shortcutKeysString) &&
                                (keys.Count != 1 || entry.Value.Shortcut.Condition == condition) &&
                                (keys.Count == 1 || string.Equals(entry.Value.Shortcut.TargetApp ?? string.Empty, targetApp, StringComparison.OrdinalIgnoreCase)));

            // Older callers without an ID may count the edited row once. Identity-aware
            // callers exclude precisely that row, so every remaining match is a duplicate.
            int upperLimit = editingId != null ? 0 : (isEditMode ? 1 : 0);
            return matches > upperLimit;
        }

        public static bool IsSelfMapping(List<string> originalKeys, List<string> remappedKeys, KeyboardMappingService mappingService)
        {
            if (mappingService == null || originalKeys == null || remappedKeys == null ||
                originalKeys.Count == 0 || remappedKeys.Count == 0)
            {
                return false;
            }

            // A single key remapped onto its own combined or side-specific form (Ctrl -> Left Ctrl)
            // is a self-map too: the origin is expanded to the left/right pair before it reaches
            // the engine, so one half of the mapping points at itself. The classic editor rejects
            // this through IsKeyRemappingToItsCombinedKey.
            if (originalKeys.Count == 1 && remappedKeys.Count == 1)
            {
                int originalKey = mappingService.GetKeyCodeFromName(originalKeys[0]);
                int remappedKey = mappingService.GetKeyCodeFromName(remappedKeys[0]);
                if (originalKey != 0 && remappedKey != 0 && IsKeyRemappingToItsCombinedKey(originalKey, remappedKey))
                {
                    return true;
                }
            }

            string originalKeysString = BuildKeyCodeString(originalKeys, mappingService);
            string remappedKeysString = BuildKeyCodeString(remappedKeys, mappingService);

            return KeyboardManagerInterop.AreShortcutsEqual(originalKeysString, remappedKeysString);
        }

        /// <summary>
        /// Mirrors BufferValidationHelpers::IsKeyRemappingToItsCombinedKey: true when one of the
        /// keys is the combined form and both belong to the same modifier family.
        /// </summary>
        private static bool IsKeyRemappingToItsCombinedKey(int first, int second)
        {
            int firstCombined = KeyboardManagerInterop.GetCombinedKey(first);
            int secondCombined = KeyboardManagerInterop.GetCombinedKey(second);

            return (first == firstCombined || second == secondCombined) && firstCombined == secondCombined;
        }

        /// <summary>
        /// True when the shortcut covers, or is covered by, an already stored shortcut for the same
        /// target app - for example Ctrl+A against Left Ctrl+A. Equality is already reported as a
        /// duplicate, so only the covering case is treated as a conflict here.
        /// </summary>
        private static bool HasOverlappingShortcut(List<string> keys, KeyboardMappingService mappingService, string appName, string? editingId)
        {
            string shortcutKeysString = BuildKeyCodeString(keys, mappingService);
            string targetApp = appName ?? string.Empty;
            foreach (var entry in SettingsManager.EditorSettings.ShortcutSettingsDictionary)
            {
                ShortcutSettings settings = entry.Value;
                string existing = settings.Shortcut.OriginalKeys;
                if (!settings.IsActive || string.Equals(entry.Key, editingId, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(existing) ||
                    !string.Equals(settings.Shortcut.TargetApp ?? string.Empty, targetApp, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var overlap = (ShortcutOverlap)KeyboardManagerInterop.DoShortcutsOverlap(existing, shortcutKeysString);
                if (overlap == ShortcutOverlap.ConflictingModifierShortcut)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool ContainsOnlyModifierKeys(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            return keys.All(key =>
            {
                int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(key);
                var keyType = (KeyType)KeyboardManagerInterop.GetKeyType(keyCode);
                return keyType != KeyType.Action;
            });
        }

        /// <summary>
        /// Returns true when at least one of the keys is a modifier. A multi-key shortcut without a
        /// modifier is rejected by the engine's own notion of a valid shortcut
        /// (<c>EditorHelpers::IsValidShortcut</c>), so it would be stored but never match.
        /// </summary>
        public static bool ContainsModifierKey(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            return keys.Any(key =>
            {
                int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(key);
                var keyType = (KeyType)KeyboardManagerInterop.GetKeyType(keyCode);
                return keyType != KeyType.Action;
            });
        }

        public static bool IsKeyOrphaned(int originalKey, KeyboardMappingService mappingService)
        {
            // Check single key mappings
            foreach (var mapping in mappingService.GetSingleKeyMappings())
            {
                if (!mapping.IsShortcut && int.TryParse(mapping.TargetKey, out int targetKey) && targetKey == originalKey)
                {
                    return false;
                }
            }

            // Check shortcut mappings. Only key-output remaps (RemapShortcut) can make a key
            // reachable again; program/URL/text targets do not restore keyboard input.
            foreach (var mapping in mappingService.GetShortcutMappings())
            {
                if (mapping.OperationType != ShortcutOperationType.RemapShortcut)
                {
                    continue;
                }

                string[] targetKeys = mapping.TargetKeys.Split(';');
                if (targetKeys.Length == 1 && int.TryParse(targetKeys[0], out int shortcutTargetKey) && shortcutTargetKey == originalKey)
                {
                    return false;
                }
            }

            return true;
        }

        private static ValidationErrorType ValidateProgramOrUrlMapping(
            List<string> originalKeys,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null)
        {
            if (originalKeys.Count < 2)
            {
                return ValidationErrorType.OneKeyMapping;
            }

            ValidationErrorType error = ValidateKeyMapping(originalKeys, originalKeys, isAppSpecific, appName, mappingService, isEditMode, editingId);

            return error == ValidationErrorType.SelfMapping ? ValidationErrorType.NoError : error;
        }

        private static bool IsIllegalShortcut(List<string> keys, KeyboardMappingService mappingService)
        {
            string shortcutKeysString = BuildKeyCodeString(keys, mappingService);
            Logger.LogInfo($"Checking if shortcut is illegal: {shortcutKeysString}");
            return KeyboardManagerInterop.IsShortcutIllegal(shortcutKeysString);
        }

        /// <summary>
        /// Checks if a single key conflicts with existing single-key mappings via modifier variants.
        /// E.g., remapping LCtrl when Ctrl is already mapped, or vice versa.
        /// </summary>
        /// <remarks>
        /// Delegates the actual rule to EditorHelpers::DoKeysOverlap through the wrapper. Comparing
        /// key types directly, as this used to, also flags a left/right pair of the same modifier
        /// (Left Ctrl against Right Ctrl), which the classic editor explicitly allows - the two
        /// cover disjoint physical keys.
        /// </remarks>
        private static bool HasConflictingModifierMapping(
            string keyName,
            bool isEditMode,
            string? editingId,
            SingleKeyRemapCondition condition)
        {
            int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(keyName);
            int keyType = KeyboardManagerInterop.GetKeyType(keyCode);

            if (keyType >= 4)
            {
                return false;
            }

            int upperLimit = editingId != null ? 0 : (isEditMode ? 1 : 0);
            int conflictCount = 0;
            foreach (var entry in SettingsManager.EditorSettings.ShortcutSettingsDictionary)
            {
                ShortcutSettings settings = entry.Value;
                string existingOriginal = settings.Shortcut.OriginalKeys;

                // Other profiles and the other single-key condition have independent mappings.
                if (!settings.IsActive || settings.Shortcut.Condition != condition ||
                    string.Equals(entry.Key, editingId, StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(existingOriginal) || existingOriginal.Contains(';'))
                {
                    continue;
                }

                if (int.TryParse(existingOriginal, out int existingKeyCode))
                {
                    if (existingKeyCode == keyCode)
                    {
                        continue; // Exact match handled by DuplicateMapping.
                    }

                    var overlap = (ShortcutOverlap)KeyboardManagerInterop.DoKeysOverlap(existingKeyCode, keyCode);
                    if (overlap == ShortcutOverlap.ConflictingModifierKey && ++conflictCount > upperLimit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string BuildKeyCodeString(List<string> keys, KeyboardMappingService mappingService)
        {
            return string.Join(";", keys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
        }
    }
}
