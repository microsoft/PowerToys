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
        /// Gets a value indicating whether the engine configuration was read successfully or does
        /// not exist yet. A failed read must not seed or reconcile this store from an empty view.
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
                    // Only seed from a successfully read configuration or a new configuration.
                    // An empty service view following a failed read must not replace user data.
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

            foreach (var mapping in GetSingleKeyMappingsForImport(settings, _mappingService!.GetSingleKeyMappings(), _mappingService.GetKeyToTextMappings()))
            {
                AddShortcutMapping(settings, mapping);
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
            var singleKeyMappings = service.GetSingleKeyMappings();
            var keyToTextMappings = service.GetKeyToTextMappings();
            var shortcutKeyMappings = service.GetShortcutMappings();

            // Process all shortcut mappings
            foreach (ShortcutKeyMapping mapping in shortcutKeyMappings)
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

            foreach (var mapping in GetSingleKeyMappingsForImport(EditorSettings, singleKeyMappings, keyToTextMappings))
            {
                if (!SingleKeyMappingExists(mapping, keyToTextMappings, singleKeyMappings))
                {
                    AddShortcutMapping(EditorSettings, mapping);
                    shortcutSettingsChanged = true;
                }
            }

            // Mark inactive mappings
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

                // Single-key remaps are always global in the engine. Clear unsupported app
                // scope retained by earlier editor versions when this is an active engine row.
                if (foundInService && shortcutSettings.IsActive &&
                    !string.IsNullOrEmpty(shortcutSettings.Shortcut.TargetApp) &&
                    int.TryParse(shortcutSettings.Shortcut.OriginalKeys, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                {
                    shortcutSettings.Shortcut.TargetApp = string.Empty;
                    shortcutSettingsChanged = true;
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

        private static List<ShortcutKeyMapping> GetSingleKeyMappingsForImport(
            EditorSettings settings,
            List<KeyMapping> singleKeyMappings,
            List<KeyToTextMapping> keyToTextMappings)
        {
            var mappings = singleKeyMappings.Select(mapping => new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapShortcut,
                OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = mapping.TargetKey,
            }).Concat(keyToTextMappings.Select(mapping => new ShortcutKeyMapping
            {
                OperationType = ShortcutOperationType.RemapText,
                OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                TargetKeys = mapping.TargetText,
                TargetText = mapping.TargetText,
            })).ToList();

            foreach (var (combined, left, right) in KeyboardMappingService.CombinedModifierKeys)
            {
                string combinedKey = combined.ToString(CultureInfo.InvariantCulture);
                string leftKey = left.ToString(CultureInfo.InvariantCulture);
                string rightKey = right.ToString(CultureInfo.InvariantCulture);

                // Collapse only newly imported rows. Authored side rows may have different IDs,
                // profile memberships or activation states, even when their targets are equal.
                if (settings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == leftKey || s.Shortcut.OriginalKeys == rightKey))
                {
                    continue;
                }

                foreach (var operation in new[] { ShortcutOperationType.RemapShortcut, ShortcutOperationType.RemapText })
                {
                    var leftMapping = mappings.Find(m => m.OperationType == operation && m.OriginalKeys == leftKey);
                    var rightMapping = mappings.Find(m => m.OperationType == operation && m.OriginalKeys == rightKey);
                    if (leftMapping is null || rightMapping is null ||
                        leftMapping.TargetKeys != rightMapping.TargetKeys ||
                        mappings.Any(m => m.OperationType == operation && m.OriginalKeys == combinedKey))
                    {
                        continue;
                    }

                    // A physical pair targeting its own combined key must not become Ctrl->Ctrl.
                    if (operation == ShortcutOperationType.RemapShortcut && leftMapping.TargetKeys == combinedKey)
                    {
                        continue;
                    }

                    leftMapping.OriginalKeys = combinedKey;
                    mappings.Remove(rightMapping);
                }
            }

            return mappings;
        }

        private static bool SingleKeyMappingExists(
            ShortcutKeyMapping mapping,
            List<KeyToTextMapping> keyToTextMappings,
            List<KeyMapping> singleKeyMappings)
        {
            int keyCode = int.Parse(mapping.OriginalKeys, CultureInfo.InvariantCulture);
            return EditorSettings.ShortcutSettingsDictionary.Values.Any(settings =>
            {
                ShortcutKeyMapping stored = settings.Shortcut;
                if (stored.OperationType != mapping.OperationType ||
                    (mapping.OperationType == ShortcutOperationType.RemapText ? stored.TargetText != mapping.TargetText : stored.TargetKeys != mapping.TargetKeys) ||
                    !int.TryParse(stored.OriginalKeys, NumberStyles.Integer, CultureInfo.InvariantCulture, out int storedKey))
                {
                    return false;
                }

                // Keep separately authored left/right rows separate. An existing combined row
                // owns the physical pair only while its complete mapping is still in the engine.
                return storedKey == keyCode ||
                       (KeyboardMappingService.ExpandCombinedModifier(storedKey).Contains(keyCode) &&
                        !ContainsSingleKeyOrigin(stored.OperationType, storedKey, keyToTextMappings, singleKeyMappings) &&
                        IsSingleKeyMappingActiveInService(stored, storedKey, keyToTextMappings, singleKeyMappings));
            });
        }

        /// <summary>
        /// A shortcut is identified by its origin keys *and* its target app: the engine keeps
        /// OS-level and app-specific remaps in separate tables and allows the same origin in both,
        /// so matching on the origin alone would hide one of them from the editor.
        /// </summary>
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

            if (int.TryParse(shortcutSettings.Shortcut.OriginalKeys, NumberStyles.Integer, CultureInfo.InvariantCulture, out int keyCode))
            {
                return IsSingleKeyMappingActiveInService(shortcutSettings.Shortcut, keyCode, keyToTextMappings, singleKeyMappings);
            }

            return shortcutKeyMappings.Any(m => IsSameOrigin(m, shortcutSettings.Shortcut));
        }

        private static bool IsSingleKeyMappingActiveInService(
            ShortcutKeyMapping mapping,
            int keyCode,
            List<KeyToTextMapping> keyToTextMappings,
            List<KeyMapping> singleKeyMappings)
        {
            bool HasMapping(int origin) => mapping.OperationType switch
            {
                ShortcutOperationType.RemapText => keyToTextMappings.Any(m => m.OriginalKey == origin && m.TargetText == mapping.TargetText),
                ShortcutOperationType.RemapShortcut => singleKeyMappings.Any(m => m.OriginalKey == origin && m.TargetKey == mapping.TargetKeys),
                _ => false,
            };

            // A legacy raw combined code has its own identity; it must not claim physical rows
            // which may coexist with it. New combined rows require BOTH physical targets.
            return ContainsSingleKeyOrigin(mapping.OperationType, keyCode, keyToTextMappings, singleKeyMappings)
                ? HasMapping(keyCode)
                : KeyboardMappingService.ExpandCombinedModifier(keyCode).All(HasMapping);
        }

        private static bool ContainsSingleKeyOrigin(
            ShortcutOperationType operation,
            int keyCode,
            List<KeyToTextMapping> keyToTextMappings,
            List<KeyMapping> singleKeyMappings)
        {
            return operation switch
            {
                ShortcutOperationType.RemapText => keyToTextMappings.Any(m => m.OriginalKey == keyCode),
                ShortcutOperationType.RemapShortcut => singleKeyMappings.Any(m => m.OriginalKey == keyCode),
                _ => false,
            };
        }
    }
}
