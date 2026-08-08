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

        // The editor cache is per-profile so switching profiles never mixes mappings.
        // The built-in "default" profile keeps the legacy "editorSettings.json" name for back-compat.
        private static string CurrentCacheFilePath
        {
            get
            {
                string profile = ProfileManager.GetActiveProfile();
                return profile.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(_settingsDirectory, "editorSettings.json")
                    : Path.Combine(_settingsDirectory, $"editorSettings.{profile}.json");
            }
        }

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
                if (!File.Exists(CurrentCacheFilePath))
                {
                    if (_mappingService is not null)
                    {
                        EditorSettings createdSettings = CreateSettingsFromKeyboardManagerService(_mappingService);
                        WriteSettings(createdSettings);
                        return createdSettings;
                    }

                    return new EditorSettings();
                }

                string json = File.ReadAllText(CurrentCacheFilePath);
                return JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions) ?? new EditorSettings();
            }
            catch (Exception)
            {
                return new EditorSettings();
            }
        }

        public static bool WriteSettings(EditorSettings editorSettings)
        {
            try
            {
                Directory.CreateDirectory(_settingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);
                File.WriteAllText(CurrentCacheFilePath, json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool WriteSettings() => WriteSettings(EditorSettings);

        private static EditorSettings CreateSettingsFromKeyboardManagerService(KeyboardMappingService service)
        {
            EditorSettings settings = new EditorSettings();

            // Process all shortcut mappings (RunProgram, OpenUri, RemapShortcut, RemapText)
            foreach (ShortcutKeyMapping mapping in service.GetShortcutMappings())
            {
                AddShortcutMapping(settings, mapping);
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
                AddShortcutMapping(settings, shortcutMapping);
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
                AddShortcutMapping(settings, shortcutMapping);
            }

            return settings;
        }

        /// <summary>
        /// Reloads the editor cache to reflect whatever profile is currently active (call after
        /// <see cref="ProfileManager.SetActiveProfile"/>). Rebuilds from the now-active profile's
        /// native config so the editor shows exactly what the engine will apply, and persists it to
        /// that profile's cache file. No-op when the native service is unavailable (preview mode).
        /// </summary>
        public static void ReloadForActiveProfile()
        {
            if (_mappingService is null)
            {
                return;
            }

            try
            {
                using var service = new KeyboardMappingService();
                EditorSettings = CreateSettingsFromKeyboardManagerService(service);
                WriteSettings();
            }
            catch (Exception ex)
            {
                ManagedCommon.Logger.LogError($"SettingsManager.ReloadForActiveProfile failed: {ex.Message}");
                EditorSettings = new EditorSettings();
            }
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
                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == mapping.OriginalKeys))
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

                if (!foundInService)
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
