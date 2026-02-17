// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            // Check if original keys are empty
            if (originalKeys == null || originalKeys.Count == 0)
            {
                return ValidationErrorType.EmptyOriginalKeys;
            }

            // Check if remapped keys are empty
            if (remappedKeys == null || remappedKeys.Count == 0)
            {
                return ValidationErrorType.EmptyRemappedKeys;
            }

            // Check if shortcut contains only modifier keys
            if ((originalKeys.Count > 1 && ContainsOnlyModifierKeys(originalKeys)) ||
                (remappedKeys.Count > 1 && ContainsOnlyModifierKeys(remappedKeys)))
            {
                return ValidationErrorType.ModifierOnly;
            }

            // Check if app specific is checked but no app name is provided
            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            // Check if this is a shortcut (multiple keys) and if it's an illegal combination
            if (originalKeys.Count > 1)
            {
                string shortcutKeysString = string.Join(";", originalKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                if (KeyboardManagerInterop.IsShortcutIllegal(shortcutKeysString))
                {
                    return ValidationErrorType.IllegalShortcut;
                }
            }

            // Check for duplicate mappings
            if (IsDuplicateMapping(originalKeys, isEditMode, mappingService))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            // Check for self-mapping
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
            // Check if original keys are empty
            if (keys == null || keys.Count == 0)
            {
                return ValidationErrorType.EmptyOriginalKeys;
            }

            // Check if text content is empty
            if (string.IsNullOrWhiteSpace(textContent))
            {
                return ValidationErrorType.EmptyTargetText;
            }

            // Check if shortcut contains only modifier keys
            if (keys.Count > 1 && ContainsOnlyModifierKeys(keys))
            {
                return ValidationErrorType.ModifierOnly;
            }

            // Check if app specific is checked but no app name is provided
            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.EmptyAppName;
            }

            // Check if this is a shortcut (multiple keys) and if it's an illegal combination
            if (keys.Count > 1)
            {
                string shortcutKeysString = string.Join(";", keys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                if (KeyboardManagerInterop.IsShortcutIllegal(shortcutKeysString))
                {
                    return ValidationErrorType.IllegalShortcut;
                }
            }

            if (IsDuplicateMapping(keys, isEditMode, mappingService))
            {
                return ValidationErrorType.DuplicateMapping;
            }

            // No errors found
            return ValidationErrorType.NoError;
        }

        public static ValidationErrorType ValidateProgramOrUrlMapping(
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

            if (error == ValidationErrorType.SelfMapping)
            {
                return ValidationErrorType.NoError;
            }

            return error;
        }

        public static bool IsDuplicateMapping(List<string> keys, bool isEditMode, KeyboardMappingService mappingService)
        {
            int upperLimit = isEditMode ? 1 : 0;
            string shortcutKeysString = string.Join(";", keys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
            ICollection<ShortcutSettings> otherMappings = SettingsManager.EditorSettings.ShortcutSettingsDictionary.Values;
            int matchesCount = otherMappings.Count(settings =>
            {
                // Mouse mappings use a different format (mouse_X) - use string comparison
                if (settings.Shortcut.OriginalKeys.StartsWith("mouse_", StringComparison.Ordinal))
                {
                    return settings.Shortcut.OriginalKeys == shortcutKeysString;
                }

                // Keyboard mappings - use native comparison
                return KeyboardManagerInterop.AreShortcutsEqual(settings.Shortcut.OriginalKeys, shortcutKeysString);
            });
            return matchesCount > upperLimit;
        }

        public static bool IsSelfMapping(List<string> originalKeys, List<string> remappedKeys, KeyboardMappingService mappingService)
        {
            if (mappingService == null)
            {
                return false;
            }

            // If either list is empty, it's not a self-mapping
            if (originalKeys == null || remappedKeys == null ||
                originalKeys.Count == 0 || remappedKeys.Count == 0)
            {
                return false;
            }

            string originalKeysString = string.Join(";", originalKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
            string remappedKeysString = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

            return KeyboardManagerInterop.AreShortcutsEqual(originalKeysString, remappedKeysString);
        }

        public static bool ContainsOnlyModifierKeys(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            foreach (string key in keys)
            {
                int keyCode = KeyboardManagerInterop.GetKeyCodeFromName(key);
                var keyType = (KeyType)KeyboardManagerInterop.GetKeyType(keyCode);

                // If any key is an action key, return false
                if (keyType == KeyType.Action)
                {
                    return false;
                }
            }

            // All keys are modifier keys
            return true;
        }

        public static bool IsKeyOrphaned(int originalKey, KeyboardMappingService mappingService)
        {
            // Check all single key mappings
            foreach (var mapping in mappingService.GetSingleKeyMappings())
            {
                if (!mapping.IsShortcut && int.TryParse(mapping.TargetKey, out int targetKey) && targetKey == originalKey)
                {
                    return false;
                }
            }

            // Check all shortcut mappings
            foreach (var mapping in mappingService.GetShortcutMappings())
            {
                string[] targetKeys = mapping.TargetKeys.Split(';');
                if (targetKeys.Length == 1 && int.TryParse(targetKeys[0], out int shortcutTargetKey) && shortcutTargetKey == originalKey)
                {
                    return false;
                }
            }

            // No mapping found for the original key
            return true;
        }
    }
}
