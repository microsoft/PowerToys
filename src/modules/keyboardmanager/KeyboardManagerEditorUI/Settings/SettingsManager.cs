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

        /// <summary>
        /// Gets a value indicating whether the engine configuration was read successfully. When it
        /// was not, this store must not be seeded or reconciled from the (empty) service view.
        /// </summary>
        internal static bool EngineConfigurationLoaded => _mappingService?.ConfigurationLoaded == true;

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
                    // Only seed from the service when it actually read the engine configuration:
                    // seeding from an empty view would write an empty store and, on the next save,
                    // present it as the user's whole configuration.
                    if (_mappingService is not null && _mappingService.ConfigurationLoaded)
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
            try
            {
                Directory.CreateDirectory(_settingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);
                File.WriteAllText(_settingsFilePath, json);
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

            return settings;
        }

        public static void CorrelateServiceAndEditorMappings()
        {
            if (_mappingService is not { } service)
            {
                return;
            }

            if (!service.ConfigurationLoaded)
            {
                // The service view is empty because the load failed, not because the user has no
                // remaps. Reconciling against it would mark every stored mapping inactive.
                ManagedCommon.Logger.LogWarning("Skipping reconciliation: the engine configuration was not loaded");
                return;
            }

            bool shortcutSettingsChanged = false;

            // Process all shortcut mappings
            foreach (ShortcutKeyMapping mapping in service.GetShortcutMappings())
            {
                ShortcutSettings? existing = FindByOrigin(mapping);
                if (existing is null)
                {
                    AddShortcutMapping(EditorSettings, mapping);
                    shortcutSettingsChanged = true;
                }
                else if (RepairFromService(existing, mapping))
                {
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

                if (!ShortcutMappingExists(shortcutMapping))
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

        /// <summary>
        /// A shortcut is identified by its origin keys *and* its target app: the engine keeps
        /// OS-level and app-specific remaps in separate tables and allows the same origin in both,
        /// so matching on the origin alone would hide one of them from the editor.
        /// </summary>
        private static bool ShortcutMappingExists(ShortcutKeyMapping mapping)
        {
            return FindByOrigin(mapping) is not null;
        }

        private static ShortcutSettings? FindByOrigin(ShortcutKeyMapping mapping)
        {
            return EditorSettings.ShortcutSettingsDictionary.Values.FirstOrDefault(s => IsSameOrigin(s.Shortcut, mapping));
        }

        /// <summary>
        /// Brings a stored row back in line with what the engine actually has for that origin.
        /// </summary>
        /// <remarks>
        /// Needed as a repair step, not just for tidiness: earlier builds recorded every
        /// shortcut-to-text remap as a key remap with an empty target, because GetShortcutRemap
        /// reported operationType 0 for text. Those rows are already in users' editorSettings.json
        /// and would otherwise stay filed under key remappings forever, showing a blank target and
        /// losing the text when opened for editing.
        /// </remarks>
        private static bool RepairFromService(ShortcutSettings stored, ShortcutKeyMapping fromService)
        {
            ShortcutOperationType previousType = stored.Shortcut.OperationType;
            if (stored.Shortcut.Equals(fromService))
            {
                return false;
            }

            // Keep the editor-only state (Id, IsActive, Profiles); the engine owns the payload.
            stored.Shortcut = fromService;

            if (previousType != fromService.OperationType)
            {
                if (EditorSettings.ShortcutsByOperationType.TryGetValue(previousType, out var previousBucket))
                {
                    previousBucket.Remove(stored.Id);
                }

                if (!EditorSettings.ShortcutsByOperationType.TryGetValue(fromService.OperationType, out var newBucket))
                {
                    newBucket = new List<string>();
                    EditorSettings.ShortcutsByOperationType[fromService.OperationType] = newBucket;
                }

                if (!newBucket.Contains(stored.Id))
                {
                    newBucket.Add(stored.Id);
                }
            }

            return true;
        }

        private static bool IsSameOrigin(ShortcutKeyMapping left, ShortcutKeyMapping right)
        {
            return left.OriginalKeys == right.OriginalKeys &&
                   string.Equals(left.TargetApp ?? string.Empty, right.TargetApp ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

            return shortcutKeyMappings.Any(m => IsSameOrigin(m, shortcutSettings.Shortcut));
        }
    }
}
