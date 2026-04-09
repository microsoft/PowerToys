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

        public static ValidationErrorType ValidateKeyMapping(
            List<string> originalKeys,
            List<string> remappedKeys,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            Remapping? editingRemapping = null)
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

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService, appName))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (originalKeys.Count == 1 && HasConflictingModifierMapping(originalKeys[0], isEditMode, mappingService))
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
            Remapping? editingRemapping = null)
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

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService, appName))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (originalKeys.Count == 1 && HasConflictingModifierMapping(originalKeys[0], isEditMode, mappingService))
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
            bool isEditMode = false)
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

            if (IsDuplicateMapping(keys, isEditMode, mappingService, appName))
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
            Remapping? editingRemapping = null)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return ValidationErrorType.EmptyUrl;
            }

            return ValidateProgramOrUrlMapping(originalKeys, isAppSpecific, appName, mappingService, isEditMode, editingRemapping);
        }

        public static ValidationErrorType ValidateAppMapping(
            List<string> originalKeys,
            string programPath,
            bool isAppSpecific,
            string appName,
            KeyboardMappingService mappingService,
            bool isEditMode = false,
            Remapping? editingRemapping = null)
        {
            if (string.IsNullOrWhiteSpace(programPath))
            {
                return ValidationErrorType.EmptyProgramPath;
            }

            return ValidateProgramOrUrlMapping(originalKeys, isAppSpecific, appName, mappingService, isEditMode, editingRemapping);
        }

        public static bool IsDuplicateMapping(List<string> keys, bool isEditMode, KeyboardMappingService mappingService, string appName)
        {
            int upperLimit = isEditMode ? 1 : 0;
            string shortcutKeysString = BuildKeyCodeString(keys, mappingService);
            return SettingsManager.EditorSettings.ShortcutSettingsDictionary.Values
                .Count(settings => KeyboardManagerInterop.AreShortcutsEqual(settings.Shortcut.OriginalKeys, shortcutKeysString) &&
                                   (string.IsNullOrEmpty(settings.Shortcut.TargetApp) || string.IsNullOrEmpty(appName) || settings.Shortcut.TargetApp == appName)) > upperLimit;
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
            Remapping? editingRemapping = null)
        {
            if (originalKeys.Count < 2)
            {
                return ValidationErrorType.OneKeyMapping;
            }

            ValidationErrorType error = ValidateKeyMapping(originalKeys, originalKeys, isAppSpecific, appName, mappingService, isEditMode, editingRemapping);

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
        private static bool HasConflictingModifierMapping(string keyName, bool isEditMode, KeyboardMappingService mappingService)
        {
            int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(keyName);
            int keyType = KeyboardManagerInterop.GetKeyType(keyCode);

            // Only modifier keys can conflict with their variants
            if (keyType >= 4)
            {
                return false;
            }

            int upperLimit = isEditMode ? 1 : 0;
            int conflictCount = 0;

            foreach (var settings in SettingsManager.EditorSettings.ShortcutSettingsDictionary.Values)
            {
                string existingOriginal = settings.Shortcut.OriginalKeys;

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

                    // Same modifier type (e.g., Ctrl and LCtrl) = conflict
                    if (existingKeyType == keyType)
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

        private static string BuildKeyCodeString(List<string> keys, KeyboardMappingService mappingService)
        {
            return string.Join(";", keys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
        }
    }
}
