// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using KeyboardManagerEditorUI.Interop;
using KeyboardManagerEditorUI.Settings;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class ValidationHelper
    {
        public static readonly Dictionary<ValidationErrorType, (string Title, string Message)> ValidationMessages = new()
        {
            { ValidationErrorType.EmptyOriginalKeys, ("Missing Original Keys", "Please enter at least one original key to create a remapping.") },
            { ValidationErrorType.EmptyRemappedKeys, ("Missing Target Keys", "Please enter at least one target key to create a remapping.") },
            { ValidationErrorType.ModifierOnly, ("Invalid Shortcut", "Shortcuts must contain at least one action key in addition to modifier keys (Ctrl, Alt, Shift, Win).") },
            { ValidationErrorType.EmptyAppName, ("Missing Application Name", "You've selected app-specific remapping but haven't specified an application name. Please enter the application name.") },
            { ValidationErrorType.IllegalShortcut, ("Reserved System Shortcut", "Win+L and Ctrl+Alt+Delete are reserved system shortcuts and cannot be remapped.") },
            { ValidationErrorType.DuplicateMapping, ("Duplicate Remapping", "This key or shortcut is already remapped.") },
            { ValidationErrorType.SelfMapping, ("Invalid Remapping", "A key or shortcut cannot be remapped to itself. Please choose a different target.") },
            { ValidationErrorType.EmptyTargetText, ("Missing Target Text", "Please enter the text to be inserted when the shortcut is pressed.") },
            { ValidationErrorType.EmptyUrl, ("Missing URL", "Please enter the URL to open when the shortcut is pressed.") },
            { ValidationErrorType.EmptyProgramPath, ("Missing Program Path", "Please enter the program path to launch when the shortcut is pressed.") },
            { ValidationErrorType.OneKeyMapping, ("Invalid Remapping", "A single key cannot be remapped to a Program or URL shortcut. Please choose a combination of keys.") },
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

            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            if (IsSelfMapping(originalKeys, remappedKeys, mappingService))
            {
                return ValidationErrorType.SelfMapping;
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

            if (string.IsNullOrWhiteSpace(textContent))
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

            if (IsDuplicateMapping(keys, isEditMode, mappingService))
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

        public static bool IsDuplicateMapping(List<string> keys, bool isEditMode, KeyboardMappingService mappingService)
        {
            int upperLimit = isEditMode ? 1 : 0;
            string shortcutKeysString = BuildKeyCodeString(keys, mappingService);
            return SettingsManager.EditorSettings.ShortcutSettingsDictionary.Values
                .Count(settings => KeyboardManagerInterop.AreShortcutsEqual(settings.Shortcut.OriginalKeys, shortcutKeysString)) > upperLimit;
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
            return KeyboardManagerInterop.IsShortcutIllegal(shortcutKeysString);
        }

        private static string BuildKeyCodeString(List<string> keys, KeyboardMappingService mappingService)
        {
            return string.Join(";", keys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
        }
    }
}
