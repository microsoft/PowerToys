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

        private static readonly object _settingsLock = new object();

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
            lock (_settingsLock)
            {
                return WriteSettingsCore(editorSettings);
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

            lock (_settingsLock)
            {
                try
                {
                    EditorSettings correlatedSettings = CloneSettings(EditorSettings);
                    bool shortcutSettingsChanged = false;

                    // Process all shortcut mappings
                    List<ShortcutKeyMapping> shortcutKeyMappings = service.GetShortcutMappings();
                    foreach (ShortcutKeyMapping mapping in shortcutKeyMappings)
                    {
                        if (!correlatedSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == mapping.OriginalKeys))
                        {
                            AddShortcutMapping(correlatedSettings, mapping);
                            shortcutSettingsChanged = true;
                        }
                    }

                    // Process single key to key mappings
                    List<KeyMapping> singleKeyMappings = service.GetSingleKeyMappings();
                    foreach (KeyMapping mapping in singleKeyMappings)
                    {
                        var shortcutMapping = new ShortcutKeyMapping
                        {
                            OperationType = ShortcutOperationType.RemapShortcut,
                            OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                            TargetKeys = mapping.TargetKey,
                        };

                        if (!MappingExists(correlatedSettings, shortcutMapping))
                        {
                            AddShortcutMapping(correlatedSettings, shortcutMapping);
                            shortcutSettingsChanged = true;
                        }
                    }

                    // Process single key to text mappings
                    List<KeyToTextMapping> keyToTextMappings = service.GetKeyToTextMappings();
                    foreach (KeyToTextMapping mapping in keyToTextMappings)
                    {
                        var shortcutMapping = new ShortcutKeyMapping
                        {
                            OperationType = ShortcutOperationType.RemapText,
                            OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                            TargetKeys = mapping.TargetText,
                            TargetText = mapping.TargetText,
                        };

                        if (!correlatedSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == shortcutMapping.OriginalKeys))
                        {
                            AddShortcutMapping(correlatedSettings, shortcutMapping);
                            shortcutSettingsChanged = true;
                        }
                    }

                    // Process typed text replacements. Prefer an active exact match, restore an
                    // inactive exact match, and remove only exact duplicates so disabled alternate
                    // replacements are preserved.
                    List<TextReplacement> textReplacementMappings = service.GetTextReplacementMappings();
                    foreach (TextReplacement mapping in textReplacementMappings)
                    {
                        List<KeyValuePair<string, ShortcutSettings>> matches = correlatedSettings.ShortcutSettingsDictionary
                            .Where(entry =>
                                entry.Value.Shortcut.OperationType == ShortcutOperationType.RemapText &&
                                string.Equals(entry.Value.Shortcut.TriggerText, mapping.Trigger, StringComparison.Ordinal) &&
                                string.Equals(GetTargetText(entry.Value.Shortcut), mapping.TargetText, StringComparison.Ordinal))
                            .ToList();

                        if (matches.Count == 0)
                        {
                            AddShortcutMapping(correlatedSettings, new ShortcutKeyMapping
                            {
                                OperationType = ShortcutOperationType.RemapText,
                                TriggerText = mapping.Trigger,
                                TargetKeys = mapping.TargetText,
                                TargetText = mapping.TargetText,
                            });
                            shortcutSettingsChanged = true;
                            continue;
                        }

                        KeyValuePair<string, ShortcutSettings> canonical = matches.FirstOrDefault(entry => entry.Value.IsActive);
                        if (string.IsNullOrEmpty(canonical.Key))
                        {
                            canonical = matches[0];
                        }

                        foreach (KeyValuePair<string, ShortcutSettings> duplicate in matches)
                        {
                            if (!string.Equals(duplicate.Key, canonical.Key, StringComparison.Ordinal))
                            {
                                RemoveShortcutMapping(correlatedSettings, duplicate.Key);
                                shortcutSettingsChanged = true;
                            }
                        }
                    }

                    // Synchronize both directions. In particular, a mapping that exists in the
                    // native configuration must be restored to active in the editor cache.
                    foreach (KeyValuePair<string, ShortcutSettings> entry in correlatedSettings.ShortcutSettingsDictionary)
                    {
                        bool foundInService = IsMappingActiveInService(
                            entry.Value,
                            keyToTextMappings,
                            textReplacementMappings,
                            singleKeyMappings,
                            shortcutKeyMappings);

                        if (entry.Value.IsActive != foundInService)
                        {
                            entry.Value.IsActive = foundInService;
                            shortcutSettingsChanged = true;
                        }

                        if (!string.Equals(entry.Value.Id, entry.Key, StringComparison.Ordinal))
                        {
                            entry.Value.Id = entry.Key;
                            shortcutSettingsChanged = true;
                        }
                    }

                    shortcutSettingsChanged |= RebuildOperationTypeIndex(correlatedSettings);

                    if (!shortcutSettingsChanged)
                    {
                        return;
                    }

                    if (WriteSettingsCore(correlatedSettings))
                    {
                        EditorSettings = correlatedSettings;
                    }
                    else
                    {
                        ManagedCommon.Logger.LogError("Failed to persist correlated Keyboard Manager editor settings");
                    }
                }
                catch (Exception ex)
                {
                    ManagedCommon.Logger.LogError("Failed to correlate Keyboard Manager service and editor settings", ex);
                }
            }
        }

        public static bool AddShortcutKeyMappingToSettings(ShortcutKeyMapping shortcutKeyMapping)
        {
            ArgumentNullException.ThrowIfNull(shortcutKeyMapping);

            return ExecuteSettingsTransaction(settings =>
            {
                AddShortcutMapping(settings, shortcutKeyMapping);
                return true;
            });
        }

        public static bool RemoveShortcutKeyMappingFromSettings(string guid)
        {
            return ExecuteSettingsTransaction(settings => RemoveShortcutMapping(settings, guid));
        }

        public static bool ReplaceShortcutKeyMappingInSettings(string guid, ShortcutKeyMapping replacement, bool isActive = true)
        {
            ArgumentNullException.ThrowIfNull(replacement);

            return ExecuteSettingsTransaction(settings =>
            {
                if (!settings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
                {
                    return false;
                }

                shortcutSettings.Shortcut = replacement;
                shortcutSettings.IsActive = isActive;
                RebuildOperationTypeIndex(settings);
                return true;
            });
        }

        public static bool SetShortcutKeyMappingActiveState(string guid, bool isActive)
        {
            return ExecuteSettingsTransaction(settings =>
            {
                if (!settings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
                {
                    return false;
                }

                shortcutSettings.IsActive = isActive;
                return true;
            });
        }

        public static bool ToggleShortcutKeyMappingActiveState(string guid)
        {
            return ExecuteSettingsTransaction(settings =>
            {
                if (!settings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
                {
                    return false;
                }

                shortcutSettings.IsActive = !shortcutSettings.IsActive;
                return true;
            });
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

        private static string GetTargetText(ShortcutKeyMapping mapping)
        {
            return string.IsNullOrEmpty(mapping.TargetText) ? mapping.TargetKeys ?? string.Empty : mapping.TargetText;
        }

        private static bool MappingExists(EditorSettings settings, ShortcutKeyMapping mapping)
        {
            return settings.ShortcutSettingsDictionary.Values.Any(s =>
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
                    m.TargetText == GetTargetText(shortcutSettings.Shortcut));
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
                        m.TargetText == GetTargetText(shortcutSettings.Shortcut));
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

        private static bool RemoveShortcutMapping(EditorSettings settings, string guid)
        {
            if (!settings.ShortcutSettingsDictionary.Remove(guid))
            {
                return false;
            }

            foreach (List<string> mappingIds in settings.ShortcutsByOperationType.Values)
            {
                mappingIds.RemoveAll(id => string.Equals(id, guid, StringComparison.Ordinal));
            }

            foreach (List<string> mappingIds in settings.ProfileDictionary.Values)
            {
                mappingIds.RemoveAll(id => string.Equals(id, guid, StringComparison.Ordinal));
            }

            return true;
        }

        private static bool RebuildOperationTypeIndex(EditorSettings settings)
        {
            var rebuiltIndex = new Dictionary<ShortcutOperationType, List<string>>();
            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                ShortcutOperationType operationType = entry.Value.Shortcut.OperationType;
                if (!rebuiltIndex.TryGetValue(operationType, out List<string>? mappingIds))
                {
                    mappingIds = new List<string>();
                    rebuiltIndex[operationType] = mappingIds;
                }

                mappingIds.Add(entry.Key);
            }

            bool changed = settings.ShortcutsByOperationType.Count != rebuiltIndex.Count ||
                           rebuiltIndex.Any(entry =>
                               !settings.ShortcutsByOperationType.TryGetValue(entry.Key, out List<string>? existingIds) ||
                               !existingIds.SequenceEqual(entry.Value));

            if (changed)
            {
                settings.ShortcutsByOperationType = rebuiltIndex;
            }

            return changed;
        }

        private static bool ExecuteSettingsTransaction(Func<EditorSettings, bool> mutation)
        {
            lock (_settingsLock)
            {
                try
                {
                    EditorSettings candidate = CloneSettings(EditorSettings);
                    if (!mutation(candidate))
                    {
                        return false;
                    }

                    RebuildOperationTypeIndex(candidate);
                    if (!WriteSettingsCore(candidate))
                    {
                        return false;
                    }

                    EditorSettings = candidate;
                    return true;
                }
                catch (Exception ex)
                {
                    ManagedCommon.Logger.LogError("Failed to update Keyboard Manager editor settings", ex);
                    return false;
                }
            }
        }

        private static EditorSettings CloneSettings(EditorSettings settings)
        {
            var clone = new EditorSettings
            {
                ActiveProfile = settings.ActiveProfile,
                ProfileDictionary = settings.ProfileDictionary.ToDictionary(
                    entry => entry.Key,
                    entry => new List<string>(entry.Value),
                    StringComparer.Ordinal),
                ShortcutsByOperationType = settings.ShortcutsByOperationType.ToDictionary(
                    entry => entry.Key,
                    entry => new List<string>(entry.Value)),
            };

            foreach (KeyValuePair<string, ShortcutSettings> entry in settings.ShortcutSettingsDictionary)
            {
                ShortcutSettings shortcutSettings = entry.Value;
                clone.ShortcutSettingsDictionary[entry.Key] = new ShortcutSettings
                {
                    Id = shortcutSettings.Id,
                    IsActive = shortcutSettings.IsActive,
                    Profiles = new List<string>(shortcutSettings.Profiles),
                    Shortcut = CloneShortcutMapping(shortcutSettings.Shortcut),
                };
            }

            return clone;
        }

        private static ShortcutKeyMapping CloneShortcutMapping(ShortcutKeyMapping mapping)
        {
            return new ShortcutKeyMapping
            {
                OriginalKeys = mapping.OriginalKeys,
                TriggerText = mapping.TriggerText,
                TargetKeys = mapping.TargetKeys,
                TargetApp = mapping.TargetApp,
                OperationType = mapping.OperationType,
                TargetText = mapping.TargetText,
                ProgramPath = mapping.ProgramPath,
                ProgramArgs = mapping.ProgramArgs,
                StartInDirectory = mapping.StartInDirectory,
                Elevation = mapping.Elevation,
                IfRunningAction = mapping.IfRunningAction,
                Visibility = mapping.Visibility,
                UriToOpen = mapping.UriToOpen,
            };
        }

        private static bool WriteSettingsCore(EditorSettings editorSettings)
        {
            string? temporaryFilePath = null;

            try
            {
                Directory.CreateDirectory(_settingsDirectory);
                string json = JsonSerializer.Serialize(editorSettings, _jsonOptions);
                temporaryFilePath = Path.Combine(
                    _settingsDirectory,
                    $"editorSettings.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");
                File.WriteAllText(temporaryFilePath, json);
                File.Move(temporaryFilePath, _settingsFilePath, true);
                temporaryFilePath = null;
                return true;
            }
            catch (Exception ex)
            {
                ManagedCommon.Logger.LogError("Failed to write Keyboard Manager editor settings", ex);
                return false;
            }
            finally
            {
                if (temporaryFilePath is not null)
                {
                    try
                    {
                        File.Delete(temporaryFilePath);
                    }
                    catch (Exception ex)
                    {
                        ManagedCommon.Logger.LogWarning($"Failed to remove temporary Keyboard Manager editor settings file: {ex.Message}");
                    }
                }
            }
        }
    }
}
