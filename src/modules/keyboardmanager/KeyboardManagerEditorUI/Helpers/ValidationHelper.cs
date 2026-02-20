// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Controls;
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
            { ValidationErrorType.DuplicateMouseMapping, ("Duplicate Mouse Remapping", "This mouse button is already remapped. Please remove the existing remapping first, or edit it instead.") },
            { ValidationErrorType.EmptyMouseActionKeys, ("Missing Target Keys", "Please enter at least one target key or shortcut for the mouse button remapping.") },
            { ValidationErrorType.EmptyMouseTargetText, ("Missing Target Text", "Please enter the text to be inserted when the mouse button is pressed.") },
            { ValidationErrorType.EmptyMouseUrl, ("Missing URL", "Please enter a URL to open when the mouse button is pressed.") },
            { ValidationErrorType.EmptyMouseProgramPath, ("Missing Program Path", "Please enter the path to the program to launch when the mouse button is pressed.") },
            { ValidationErrorType.EmptyMouseTargetButton, ("Missing Target Mouse Button", "Please select a target mouse button for the remapping.") },
            { ValidationErrorType.MouseAppNameMissing, ("Missing Application Name", "You've selected app-specific remapping but haven't specified an application name. Please enter the application name.") },
            { ValidationErrorType.MouseSelfMapping, ("Invalid Mouse Remapping", "A mouse button cannot be remapped to itself. Please choose a different target.") },
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

        /// <summary>
        /// Validates a mouse button remapping before saving. Checks for missing action data,
        /// duplicate mappings, self-mappings, and missing app names.
        /// </summary>
        public static ValidationErrorType ValidateMouseButtonMapping(
            int mouseButtonCode,
            UnifiedMappingControl.ActionType actionType,
            UnifiedMappingControl control,
            KeyboardMappingService mappingService,
            bool isEditMode = false)
        {
            bool isAppSpecific = control.GetIsAppSpecific();
            string appName = control.GetAppName();

            // Check if app specific is checked but no app name is provided
            if (isAppSpecific && string.IsNullOrWhiteSpace(appName))
            {
                return ValidationErrorType.MouseAppNameMissing;
            }

            // Validate action-specific data
            switch (actionType)
            {
                case UnifiedMappingControl.ActionType.KeyOrShortcut:
                    {
                        var actionKeys = control.GetActionKeys();
                        if (actionKeys == null || actionKeys.Count == 0)
                        {
                            return ValidationErrorType.EmptyMouseActionKeys;
                        }

                        break;
                    }

                case UnifiedMappingControl.ActionType.Text:
                    {
                        string textContent = control.GetTextContent();
                        if (string.IsNullOrEmpty(textContent))
                        {
                            return ValidationErrorType.EmptyMouseTargetText;
                        }

                        break;
                    }

                case UnifiedMappingControl.ActionType.OpenUrl:
                    {
                        string url = control.GetUrl();
                        if (string.IsNullOrEmpty(url))
                        {
                            return ValidationErrorType.EmptyMouseUrl;
                        }

                        break;
                    }

                case UnifiedMappingControl.ActionType.OpenApp:
                    {
                        string programPath = control.GetProgramPath();
                        if (string.IsNullOrEmpty(programPath))
                        {
                            return ValidationErrorType.EmptyMouseProgramPath;
                        }

                        break;
                    }

                case UnifiedMappingControl.ActionType.MouseClick:
                    {
                        int? targetMouseButton = control.GetMouseActionButtonCode();
                        if (targetMouseButton == null)
                        {
                            return ValidationErrorType.EmptyMouseTargetButton;
                        }

                        // Check self-mapping (mouse button to itself)
                        if (targetMouseButton.Value == mouseButtonCode)
                        {
                            return ValidationErrorType.MouseSelfMapping;
                        }

                        break;
                    }
            }

            // Check for duplicate mouse mappings
            if (IsMouseDuplicateMapping(mouseButtonCode, isAppSpecific ? appName : string.Empty, isEditMode))
            {
                return ValidationErrorType.DuplicateMouseMapping;
            }

            return ValidationErrorType.NoError;
        }

        /// <summary>
        /// Checks if a mouse button mapping already exists for the given button and target app.
        /// </summary>
        public static bool IsMouseDuplicateMapping(int mouseButtonCode, string targetApp, bool isEditMode)
        {
            int upperLimit = isEditMode ? 1 : 0;
            string mouseOriginalKeys = $"mouse_{mouseButtonCode}";

            ICollection<ShortcutSettings> allMappings = SettingsManager.EditorSettings.ShortcutSettingsDictionary.Values;
            int matchesCount = allMappings.Count(settings =>
            {
                if (!settings.Shortcut.OriginalKeys.StartsWith("mouse_", StringComparison.Ordinal))
                {
                    return false;
                }

                // Match on same mouse button
                if (settings.Shortcut.OriginalKeys != mouseOriginalKeys)
                {
                    return false;
                }

                // For app-specific mappings, also check app name
                string existingApp = settings.Shortcut.TargetApp ?? string.Empty;
                return string.Equals(existingApp, targetApp, StringComparison.OrdinalIgnoreCase);
            });

            return matchesCount > upperLimit;
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
