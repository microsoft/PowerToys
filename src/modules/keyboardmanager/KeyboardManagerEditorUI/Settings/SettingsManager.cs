// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    internal static class SettingsManager
    {
        private static readonly string _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "Keyboard Manager");

        private static readonly string _settingsFilePath = Path.Combine(_settingsDirectory, "editorSettings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static readonly KeyboardMappingService? _mappingService;

        /// <summary>
        /// Gets a value indicating whether the native C++ wrapper DLL is available.
        /// When false the editor runs in read-only / XAML-preview mode using JSON settings only.
        /// </summary>
        internal static bool IsNativeServiceAvailable => _mappingService is not null;

        public static EditorSettings EditorSettings { get; set; }

        static SettingsManager()
        {
            try
            {
                _mappingService = new KeyboardMappingService();
            }
            catch (Exception ex) when (ex is DllNotFoundException or InvalidOperationException)
            {
                ManagedCommon.Logger.LogWarning($"Native KBM library unavailable, running in standalone mode: {ex.Message}");
                _mappingService = null;
            }

            EditorSettings = LoadSettings();
        }

        public static EditorSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    if (_mappingService is not null)
                    {
                        EditorSettings createdSettings = CreateSettingsFromKeyboardManagerService();
                        WriteSettings(createdSettings);
                        return createdSettings;
                    }

                    return new EditorSettings();
                }

                string json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
            }
            catch (Exception)
            {
                return new EditorSettings();
            }
        }

        public static bool WriteSettings(EditorSettings editorSettings)
        {
            string temporaryFilePath = _settingsFilePath + ".tmp";
            try
            {
                Directory.CreateDirectory(_settingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);
                File.WriteAllText(temporaryFilePath, json);
                File.Move(temporaryFilePath, _settingsFilePath, true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool WriteSettings() => WriteSettings(EditorSettings);

        private static EditorSettings CreateSettingsFromKeyboardManagerService()
        {
            EditorSettings settings = new EditorSettings();

            // Process all shortcut mappings (RunProgram, OpenUri, RemapShortcut, RemapText)
            foreach (ShortcutKeyMapping mapping in _mappingService!.GetShortcutMappings())
            {
                AddShortcutMapping(settings, mapping);
            }

            // Process single key to key mappings
            foreach (var mapping in _mappingService!.GetSingleKeyMappings())
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapShortcut,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetKey,
                };
                AddShortcutMapping(settings, shortcutMapping);
            }

            // Process single key to text mappings
            foreach (var mapping in _mappingService!.GetKeyToTextMappings())
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetText,
                    TargetText = mapping.TargetText,
                };
                AddShortcutMapping(settings, shortcutMapping);
            }

            // Process typed text replacements
            foreach (var mapping in _mappingService!.GetTextReplacementMappings())
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    TriggerText = mapping.Trigger,
                    TargetKeys = mapping.TargetText,
                    TargetText = mapping.TargetText,
                };
                AddShortcutMapping(settings, shortcutMapping);
            }

            return settings;
        }

        public static void CorrelateServiceAndEditorMappings()
        {
            if (_mappingService is not { } service)
            {
                return;
            }

            bool shortcutSettingsChanged = false;
            List<ShortcutKeyMapping> shortcutKeyMappings = service.GetShortcutMappings();
            List<KeyMapping> singleKeyMappings = service.GetSingleKeyMappings();
            List<KeyToTextMapping> keyToTextMappings = service.GetKeyToTextMappings();
            List<TextReplacement> textReplacementMappings = service.GetTextReplacementMappings();

            foreach (ShortcutKeyMapping mapping in shortcutKeyMappings)
            {
                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == mapping.OriginalKeys))
                {
                    AddShortcutMapping(EditorSettings, mapping);
                    shortcutSettingsChanged = true;
                }
            }

            foreach (KeyMapping mapping in singleKeyMappings)
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapShortcut,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetKey,
                };

                if (!MappingExists(shortcutMapping))
                {
                    AddShortcutMapping(EditorSettings, shortcutMapping);
                    shortcutSettingsChanged = true;
                }
            }

            foreach (KeyToTextMapping mapping in keyToTextMappings)
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetText,
                    TargetText = mapping.TargetText,
                };

                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == shortcutMapping.OriginalKeys))
                {
                    AddShortcutMapping(EditorSettings, shortcutMapping);
                    shortcutSettingsChanged = true;
                }
            }

            foreach (TextReplacement mapping in textReplacementMappings)
            {
                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(settings =>
                    settings.Shortcut.OperationType == ShortcutOperationType.RemapText &&
                    string.Equals(settings.Shortcut.TriggerText, mapping.Trigger, StringComparison.Ordinal) &&
                    string.Equals(settings.Shortcut.TargetText, mapping.TargetText, StringComparison.Ordinal)))
                {
                    AddShortcutMapping(EditorSettings, new ShortcutKeyMapping
                    {
                        OperationType = ShortcutOperationType.RemapText,
                        TriggerText = mapping.Trigger,
                        TargetKeys = mapping.TargetText,
                        TargetText = mapping.TargetText,
                    });
                    shortcutSettingsChanged = true;
                }
            }

            foreach (ShortcutSettings shortcutSettings in EditorSettings.ShortcutSettingsDictionary.Values)
            {
                bool foundInService = IsMappingActiveInService(
                    shortcutSettings,
                    keyToTextMappings,
                    textReplacementMappings,
                    singleKeyMappings,
                    shortcutKeyMappings);

                if (shortcutSettings.IsActive != foundInService)
                {
                    shortcutSettings.IsActive = foundInService;
                    shortcutSettingsChanged = true;
                }
            }

            if (shortcutSettingsChanged)
            {
                WriteSettings();
            }
        }

        public static bool AddShortcutKeyMappingToSettings(ShortcutKeyMapping shortcutKeyMapping)
        {
            ArgumentNullException.ThrowIfNull(shortcutKeyMapping);
            string guid = AddShortcutMapping(EditorSettings, shortcutKeyMapping);
            if (WriteSettings())
            {
                return true;
            }

            EditorSettings.ShortcutSettingsDictionary.Remove(guid);
            EditorSettings.ShortcutsByOperationType[shortcutKeyMapping.OperationType].Remove(guid);
            return false;
        }

        public static bool RemoveShortcutKeyMappingFromSettings(string guid)
        {
            if (!EditorSettings.ShortcutSettingsDictionary.Remove(guid, out ShortcutSettings? shortcutSettings))
            {
                return false;
            }

            var profileNames = new List<string>();
            if (EditorSettings.ShortcutsByOperationType.TryGetValue(shortcutSettings.Shortcut.OperationType, out List<string>? operationMappingIds))
            {
                operationMappingIds.Remove(guid);
            }

            foreach (KeyValuePair<string, List<string>> profile in EditorSettings.ProfileDictionary)
            {
                if (profile.Value.Remove(guid))
                {
                    profileNames.Add(profile.Key);
                }
            }

            if (WriteSettings())
            {
                return true;
            }

            EditorSettings.ShortcutSettingsDictionary[guid] = shortcutSettings;
            operationMappingIds?.Add(guid);
            foreach (string profileName in profileNames)
            {
                EditorSettings.ProfileDictionary[profileName].Add(guid);
            }

            return false;
        }

        public static bool ReplaceShortcutKeyMappingInSettings(string guid, ShortcutKeyMapping replacement, bool isActive)
        {
            ArgumentNullException.ThrowIfNull(replacement);

            if (!EditorSettings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
            {
                return false;
            }

            ShortcutKeyMapping originalShortcut = shortcutSettings.Shortcut;
            bool originalActiveState = shortcutSettings.IsActive;
            shortcutSettings.Shortcut = replacement;
            shortcutSettings.IsActive = isActive;
            if (WriteSettings())
            {
                return true;
            }

            shortcutSettings.Shortcut = originalShortcut;
            shortcutSettings.IsActive = originalActiveState;
            return false;
        }

        public static bool SetShortcutKeyMappingActiveState(string guid, bool isActive)
        {
            if (!EditorSettings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
            {
                return false;
            }

            bool previousState = shortcutSettings.IsActive;
            shortcutSettings.IsActive = isActive;
            if (WriteSettings())
            {
                return true;
            }

            shortcutSettings.IsActive = previousState;
            return false;
        }

        public static void ToggleShortcutKeyMappingActiveState(string guid)
        {
            if (EditorSettings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
            {
                shortcutSettings.IsActive = !shortcutSettings.IsActive;
                WriteSettings();
            }
        }

        private static string AddShortcutMapping(EditorSettings settings, ShortcutKeyMapping mapping)
        {
            string guid = Guid.NewGuid().ToString();
            var shortcutSettings = new ShortcutSettings
            {
                Id = guid,
                Shortcut = mapping,
                IsActive = true,
            };

            settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

            if (!settings.ShortcutsByOperationType.TryGetValue(mapping.OperationType, out System.Collections.Generic.List<string>? value))
            {
                value = new System.Collections.Generic.List<string>();
                settings.ShortcutsByOperationType[mapping.OperationType] = value;
            }

            value.Add(guid);
            return guid;
        }

        private static bool MappingExists(ShortcutKeyMapping mapping)
        {
            return EditorSettings.ShortcutSettingsDictionary.Values.Any(s =>
                s.Shortcut.OperationType == mapping.OperationType &&
                s.Shortcut.TriggerText == mapping.TriggerText &&
                s.Shortcut.OriginalKeys == mapping.OriginalKeys &&
                s.Shortcut.TargetKeys == mapping.TargetKeys);
        }

        private static bool IsMappingActiveInService(
            ShortcutSettings shortcutSettings,
            List<KeyToTextMapping> keyToTextMappings,
            List<TextReplacement> textReplacementMappings,
            List<KeyMapping> singleKeyMappings,
            List<ShortcutKeyMapping> shortcutKeyMappings)
        {
            if (!string.IsNullOrEmpty(shortcutSettings.Shortcut.TriggerText))
            {
                return textReplacementMappings.Any(m =>
                    m.Trigger == shortcutSettings.Shortcut.TriggerText &&
                    m.TargetText == shortcutSettings.Shortcut.TargetText);
            }

            if (string.IsNullOrEmpty(shortcutSettings.Shortcut.OriginalKeys))
            {
                return false;
            }

            bool isSingleKey = shortcutSettings.Shortcut.OriginalKeys.Split(';').Length == 1;

            if (isSingleKey && int.TryParse(shortcutSettings.Shortcut.OriginalKeys, out int keyCode))
            {
                if (shortcutSettings.Shortcut.OperationType == ShortcutOperationType.RemapText)
                {
                    return keyToTextMappings.Any(m =>
                        m.OriginalKey == keyCode &&
                        m.TargetText == shortcutSettings.Shortcut.TargetText);
                }
                else if (shortcutSettings.Shortcut.OperationType == ShortcutOperationType.RemapShortcut)
                {
                    return singleKeyMappings.Any(m =>
                        m.OriginalKey == keyCode &&
                        m.TargetKey == shortcutSettings.Shortcut.TargetKeys);
                }
            }

            return shortcutKeyMappings.Any(m => m.OriginalKeys == shortcutSettings.Shortcut.OriginalKeys);
        }
    }
}
