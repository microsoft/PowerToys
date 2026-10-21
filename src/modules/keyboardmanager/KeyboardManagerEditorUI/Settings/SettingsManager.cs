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
    internal static partial class SettingsManager
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
            string? temporaryPath = null;
            try
            {
                Directory.CreateDirectory(_settingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);

                // Replace the file only after the complete document has been written in the same directory.
                temporaryPath = Path.Combine(_settingsDirectory, $"editorSettings.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, _settingsFilePath, overwrite: true);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                try
                {
                    if (temporaryPath is not null && File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch (Exception)
                {
                    // A leftover temporary file must not change the result of saving settings.
                }
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

            return settings;
        }

        public static void CorrelateServiceAndEditorMappings()
        {
            if (_mappingService is not { } service)
            {
                return;
            }

            bool shortcutSettingsChanged = false;

            // Process all shortcut mappings
            foreach (ShortcutKeyMapping mapping in service.GetShortcutMappings())
            {
                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s =>
                    s.CliBinding is not null
                        ? ObservedShortcutMatches(s.Shortcut, mapping)
                        : s.Shortcut.OriginalKeys == mapping.OriginalKeys))
                {
                    AddShortcutMapping(EditorSettings, mapping);
                    shortcutSettingsChanged = true;
                }
            }

            // Process single key to key mappings
            foreach (var mapping in service.GetSingleKeyMappings())
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

            // Process single key to text mappings
            foreach (var mapping in service.GetKeyToTextMappings())
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

            // Mark inactive mappings
            var singleKeyMappings = service.GetSingleKeyMappings();
            var keyToTextMappings = service.GetKeyToTextMappings();
            var shortcutKeyMappings = service.GetShortcutMappings();

            foreach (ShortcutSettings shortcutSettings in EditorSettings.ShortcutSettingsDictionary.Values.ToList())
            {
                bool foundInService = IsMappingActiveInService(
                    shortcutSettings,
                    keyToTextMappings,
                    singleKeyMappings,
                    shortcutKeyMappings);

                if (!foundInService && shortcutSettings.IsActive)
                {
                    shortcutSettingsChanged = true;
                    shortcutSettings.IsActive = false;
                }
            }

            if (shortcutSettingsChanged)
            {
                WriteSettings();
            }
        }

        public static void AddShortcutKeyMappingToSettings(ShortcutKeyMapping shortcutKeyMapping)
        {
            AddShortcutMapping(EditorSettings, shortcutKeyMapping);
            WriteSettings();
        }

        public static void RemoveShortcutKeyMappingFromSettings(string guid)
        {
            ShortcutOperationType operationType = EditorSettings.ShortcutSettingsDictionary[guid].Shortcut.OperationType;
            EditorSettings.ShortcutSettingsDictionary.Remove(guid);

            if (EditorSettings.ShortcutsByOperationType.TryGetValue(operationType, out var value))
            {
                value.Remove(guid);
            }

            WriteSettings();
        }

        public static void ToggleShortcutKeyMappingActiveState(string guid)
        {
            if (EditorSettings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
            {
                shortcutSettings.IsActive = !shortcutSettings.IsActive;
                WriteSettings();
            }
        }

        private static void AddShortcutMapping(EditorSettings settings, ShortcutKeyMapping mapping)
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
        }

        private static bool MappingExists(ShortcutKeyMapping mapping)
        {
            return EditorSettings.ShortcutSettingsDictionary.Values.Any(s =>
                s.Shortcut.OperationType == mapping.OperationType &&
                s.Shortcut.OriginalKeys == mapping.OriginalKeys &&
                s.Shortcut.TargetKeys == mapping.TargetKeys);
        }

        private static bool IsMappingActiveInService(
            ShortcutSettings shortcutSettings,
            List<KeyToTextMapping> keyToTextMappings,
            List<KeyMapping> singleKeyMappings,
            List<ShortcutKeyMapping> shortcutKeyMappings)
        {
            if (string.IsNullOrEmpty(shortcutSettings.Shortcut.OriginalKeys))
            {
                return false;
            }

            if (shortcutSettings.CliBinding is not null)
            {
                // Retain a disabled CLI binding's parameter values, but never attach its metadata to
                // a different command that was written externally for the same trigger.
                return shortcutKeyMappings.Any(m => ObservedShortcutMatches(shortcutSettings.Shortcut, m));
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
