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
        };

        // Note on the edit-mode parameters below: <paramref name="isEditMode"/> is the legacy
        // count-based tolerance flag; <paramref name="editingId"/> is the id of the row currently being
        // edited (its key in ShortcutSettingsDictionary). When editingId is supplied, the duplicate /
        // conflict checks exclude that one row by identity, so an edit that collides with any *other*
        // row is correctly rejected. When it is null they fall back to the old count tolerance.
        public static ValidationErrorType ValidateKeyMapping(
            List<string> originalKeys,
            List<string> remappedKeys,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            string? editingId = null)
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

            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            if (originalKeys.Count > 1 && IsIllegalShortcut(originalKeys, mappingService))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService, appName, editingId))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (originalKeys.Count == 1 && HasConflictingModifierMapping(originalKeys[0], isEditMode, mappingService, editingId))
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
            string? editingId = null)
        {
            if (originalKeys == null || originalKeys.Count == 0)
            {
                return ValidationErrorType.EmptyOriginalKeys;
            }

            if (originalKeys.Count > 1 && ContainsOnlyModifierKeys(originalKeys))
            {
                return ValidationErrorType.ModifierOnly;
            }

            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            if (originalKeys.Count > 1 && IsIllegalShortcut(originalKeys, mappingService))
            {
                return ValidationErrorType.IllegalShortcut;
            }

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService, appName, editingId))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (originalKeys.Count == 1 && HasConflictingModifierMapping(originalKeys[0], isEditMode, mappingService, editingId))
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

            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
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

        public static bool IsDuplicateMapping(List<string> keys, bool isEditMode, KeyboardMappingService mappingService, string appName, string? editingId = null)
        {
            string shortcutKeysString = BuildKeyCodeString(keys, mappingService);

            // Only rows that are active belong to the current profile's engine configuration;
            // inactive ones are retained metadata for other profiles and must not block an edit.
            int matches = SettingsManager.EditorSettings.ShortcutSettingsDictionary
                .Where(kvp => kvp.Value.IsActive)
                .Where(kvp => editingId == null || kvp.Key != editingId)
                .Count(kvp => KeyboardManagerInterop.AreShortcutsEqual(kvp.Value.Shortcut.OriginalKeys, shortcutKeysString) &&
                              (string.IsNullOrEmpty(kvp.Value.Shortcut.TargetApp) || string.IsNullOrEmpty(appName) || kvp.Value.Shortcut.TargetApp == appName));

            // With the edited row's identity we exclude exactly that row above, so any remaining match is
            // a genuine duplicate against a *different* row. Without it, fall back to the old tolerance
            // (edit mode may still match its own not-yet-excluded row once).
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

            string originalKeysString = BuildKeyCodeString(originalKeys, mappingService);
            string remappedKeysString = BuildKeyCodeString(remappedKeys, mappingService);

            return KeyboardManagerInterop.AreShortcutsEqual(originalKeysString, remappedKeysString);
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

            // Check shortcut mappings
            foreach (var mapping in mappingService.GetShortcutMappings())
            {
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
        private static bool HasConflictingModifierMapping(string keyName, bool isEditMode, KeyboardMappingService mappingService, string? editingId = null)
        {
            int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(keyName);
            int keyType = KeyboardManagerInterop.GetKeyType(keyCode);

            // Only modifier keys can conflict with their variants
            if (keyType >= 4)
            {
                return false;
            }

            // With the edited row's identity we exclude it below and any remaining conflict is real;
            // without it, fall back to the old count tolerance.
            int upperLimit = editingId != null ? 0 : (isEditMode ? 1 : 0);
            int conflictCount = 0;

            foreach (var kvp in SettingsManager.EditorSettings.ShortcutSettingsDictionary)
            {
                // Inactive rows are retained for other profiles and are not part of the
                // candidate engine configuration, so they cannot conflict with this edit.
                if (!kvp.Value.IsActive)
                {
                    continue;
                }

                if (editingId != null && kvp.Key == editingId)
                {
                    continue; // exclude the row being edited by identity, not by count
                }

                string existingOriginal = kvp.Value.Shortcut.OriginalKeys;

                // Only check single-key mappings (no semicolons)
                if (string.IsNullOrEmpty(existingOriginal) || existingOriginal.Contains(';'))
                {
                    continue;
                }

                if (int.TryParse(existingOriginal, out int existingKeyCode))
                {
                    if (existingKeyCode == keyCode)
                    {
                        continue; // Exact match handled by DuplicateMapping
                    }

                    int existingKeyType = KeyboardManagerInterop.GetKeyType(existingKeyCode);

                    // Same modifier family but a different key code. This is only a genuine (ambiguous)
                    // conflict when one side is the generic, side-agnostic modifier (e.g. Ctrl vs Left
                    // Ctrl): the generic one matches either physical side, so mapping both is ambiguous.
                    // Two DISTINCT specific sides (e.g. Left Ctrl vs Right Ctrl) are separate physical
                    // keys and can coexist (this is exactly the left/right ⌘ -> 英数/かな use case).
                    if (existingKeyType == keyType && (IsGenericModifier(keyCode) || IsGenericModifier(existingKeyCode)))
                    {
                        conflictCount++;
                        if (conflictCount > upperLimit)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the key code is a generic, side-agnostic modifier (Ctrl/Alt/Shift, or the
        /// "both Windows keys" pseudo-key). These match either physical side at the hook level, so
        /// pairing one with a specific side of the same family is ambiguous; two specific sides are not.
        /// </summary>
        private static bool IsGenericModifier(int keyCode)
        {
            const int VK_SHIFT = 0x10;
            const int VK_CONTROL = 0x11;
            const int VK_MENU = 0x12;    // Alt
            const int VK_WIN_BOTH = 0x104; // CommonSharedConstants::VK_WIN_BOTH
            return keyCode == VK_SHIFT || keyCode == VK_CONTROL || keyCode == VK_MENU || keyCode == VK_WIN_BOTH;
        }

        private static string BuildKeyCodeString(List<string> keys, KeyboardMappingService mappingService)
        {
            return string.Join(";", keys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
        }
    }
}
