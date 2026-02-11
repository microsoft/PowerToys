// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using KeyboardManagerEditorUI.Interop;

namespace KeyboardManagerEditorUI.Settings
{
    internal static class SettingsManager
    {
        private static readonly string _settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "PowerToys",
            "KeyboardManager");

        private static readonly string _settingsFilePath = Path.Combine(_settingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private static KeyboardMappingService? _mappingService;

        public static EditorSettings EditorSettings { get; set; }

        static SettingsManager()
        {
            _mappingService = new KeyboardMappingService();
            EditorSettings = LoadSettings();
        }

        public static EditorSettings LoadSettings()
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    EditorSettings createdSettings = CreateSettingsFromKeyboardManagerService();
                    WriteSettings(createdSettings);
                    return createdSettings;
                }

                string json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<EditorSettings>(json, _jsonOptions);

                return settings ?? new EditorSettings();
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

        public static bool WriteSettings()
        {
            return WriteSettings(EditorSettings);
        }

        private static EditorSettings CreateSettingsFromKeyboardManagerService()
        {
            EditorSettings settings = new EditorSettings();

            // Handle shortcut mappings (RunProgram, OpenUri, RemapShortcut, RemapText shortcuts)
            foreach (ShortcutKeyMapping mapping in _mappingService!.GetShortcutMappings())
            {
                string guid = Guid.NewGuid().ToString();
                ShortcutSettings shortcutSettings = new ShortcutSettings
                {
                    Id = guid,
                    Shortcut = mapping,
                    IsActive = true,
                };

                settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

                if (settings.ShortcutsByOperationType.TryGetValue(mapping.OperationType, out List<string>? value))
                {
                    value.Add(guid);
                }
                else
                {
                    settings.ShortcutsByOperationType[mapping.OperationType] = new List<string> { guid };
                }
            }

            // Handle single key to key mappings
            var singleKeyMappings = _mappingService.GetSingleKeyMappings();
            foreach (var mapping in singleKeyMappings)
            {
                // Create a ShortcutKeyMapping representation for single key to key mappings
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapShortcut,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetKey,
                };

                string guid = Guid.NewGuid().ToString();
                ShortcutSettings shortcutSettings = new ShortcutSettings
                {
                    Id = guid,
                    Shortcut = shortcutMapping,
                    IsActive = true,
                };

                settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

                if (settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapShortcut, out List<string>? value))
                {
                    value.Add(guid);
                }
                else
                {
                    settings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut] = new List<string> { guid };
                }
            }

            // Handle single key to text mappings
            var keyToTextMappings = _mappingService.GetKeyToTextMappings();
            foreach (var mapping in keyToTextMappings)
            {
                // Create a ShortcutKeyMapping representation for single key to text mappings
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetText,
                    TargetText = mapping.TargetText,
                };

                string guid = Guid.NewGuid().ToString();
                ShortcutSettings shortcutSettings = new ShortcutSettings
                {
                    Id = guid,
                    Shortcut = shortcutMapping,
                    IsActive = true,
                };

                settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

                if (settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out List<string>? value))
                {
                    value.Add(guid);
                }
                else
                {
                    settings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = new List<string> { guid };
                }
            }

            // Handle mouse button → key/shortcut/text/etc. mappings
            var mouseButtonMappings = _mappingService.GetMouseButtonMappings();
            foreach (var mapping in mouseButtonMappings)
            {
                var shortcutMapping = ConvertMouseMappingToShortcutKeyMapping(mapping);

                string guid = Guid.NewGuid().ToString();
                ShortcutSettings shortcutSettings = new ShortcutSettings
                {
                    Id = guid,
                    Shortcut = shortcutMapping,
                    IsActive = true,
                };

                settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

                if (settings.ShortcutsByOperationType.TryGetValue(shortcutMapping.OperationType, out List<string>? mouseValue))
                {
                    mouseValue.Add(guid);
                }
                else
                {
                    settings.ShortcutsByOperationType[shortcutMapping.OperationType] = new List<string> { guid };
                }
            }

            // Handle key → mouse button mappings
            var keyToMouseMappings = _mappingService.GetKeyToMouseMappings();
            foreach (var mapping in keyToMouseMappings)
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapKeyToMouse,
                    OriginalKeys = mapping.OriginalKeyCode.ToString(CultureInfo.InvariantCulture),
                    TargetApp = mapping.TargetApp,
                    TargetMouseButton = mapping.TargetMouseButton,
                };

                string guid = Guid.NewGuid().ToString();
                ShortcutSettings shortcutSettings = new ShortcutSettings
                {
                    Id = guid,
                    Shortcut = shortcutMapping,
                    IsActive = true,
                };

                settings.ShortcutSettingsDictionary[guid] = shortcutSettings;

                if (settings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapKeyToMouse, out List<string>? keyToMouseValue))
                {
                    keyToMouseValue.Add(guid);
                }
                else
                {
                    settings.ShortcutsByOperationType[ShortcutOperationType.RemapKeyToMouse] = new List<string> { guid };
                }
            }

            return settings;
        }

        public static void CorrelateServiceAndEditorMappings()
        {
            bool shortcutSettingsChanged = false;

            if (_mappingService is null)
            {
                return;
            }

            // Handle shortcut mappings (RunProgram, OpenUri, RemapShortcut, RemapText shortcuts)
            List<ShortcutKeyMapping> shortcutKeyMappings = _mappingService.GetShortcutMappings();
            foreach (ShortcutKeyMapping mapping in shortcutKeyMappings)
            {
                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == mapping.OriginalKeys))
                {
                    shortcutSettingsChanged = true;
                    string guid = Guid.NewGuid().ToString();
                    ShortcutSettings shortcutSettings = new ShortcutSettings
                    {
                        Id = guid,
                        Shortcut = mapping,
                        IsActive = true,
                    };
                    EditorSettings.ShortcutSettingsDictionary[guid] = shortcutSettings;
                    if (EditorSettings.ShortcutsByOperationType.TryGetValue(mapping.OperationType, out List<string>? value))
                    {
                        value.Add(guid);
                    }
                    else
                    {
                        EditorSettings.ShortcutsByOperationType[mapping.OperationType] = new List<string> { guid };
                    }
                }
            }

            // Handle single key to key mappings
            var singleKeyMappings = _mappingService.GetSingleKeyMappings();
            foreach (var mapping in singleKeyMappings)
            {
                // Create a ShortcutKeyMapping representation for single key to key mappings
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapShortcut,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetKey,
                };

                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s =>
                    s.Shortcut.OperationType == ShortcutOperationType.RemapShortcut &&
                    s.Shortcut.OriginalKeys == shortcutMapping.OriginalKeys &&
                    s.Shortcut.TargetKeys == shortcutMapping.TargetKeys))
                {
                    shortcutSettingsChanged = true;
                    string guid = Guid.NewGuid().ToString();
                    ShortcutSettings shortcutSettings = new ShortcutSettings
                    {
                        Id = guid,
                        Shortcut = shortcutMapping,
                        IsActive = true,
                    };
                    EditorSettings.ShortcutSettingsDictionary[guid] = shortcutSettings;
                    if (EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapShortcut, out List<string>? value))
                    {
                        value.Add(guid);
                    }
                    else
                    {
                        EditorSettings.ShortcutsByOperationType[ShortcutOperationType.RemapShortcut] = new List<string> { guid };
                    }
                }
            }

            // Handle single key to text mappings
            var keyToTextMappings = _mappingService.GetKeyToTextMappings();
            foreach (var mapping in keyToTextMappings)
            {
                // Create a ShortcutKeyMapping representation for single key to text mappings
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapText,
                    OriginalKeys = mapping.OriginalKey.ToString(CultureInfo.InvariantCulture),
                    TargetKeys = mapping.TargetText,
                    TargetText = mapping.TargetText,
                };

                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.OriginalKeys == shortcutMapping.OriginalKeys))
                {
                    shortcutSettingsChanged = true;
                    string guid = Guid.NewGuid().ToString();
                    ShortcutSettings shortcutSettings = new ShortcutSettings
                    {
                        Id = guid,
                        Shortcut = shortcutMapping,
                        IsActive = true,
                    };
                    EditorSettings.ShortcutSettingsDictionary[guid] = shortcutSettings;
                    if (EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapText, out List<string>? value))
                    {
                        value.Add(guid);
                    }
                    else
                    {
                        EditorSettings.ShortcutsByOperationType[ShortcutOperationType.RemapText] = new List<string> { guid };
                    }
                }
            }

            // Handle mouse button mappings
            var mouseButtonMappings = _mappingService.GetMouseButtonMappings();
            foreach (var mapping in mouseButtonMappings)
            {
                var shortcutMapping = ConvertMouseMappingToShortcutKeyMapping(mapping);

                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s =>
                    s.Shortcut.OriginalKeys == shortcutMapping.OriginalKeys &&
                    s.Shortcut.TargetApp == shortcutMapping.TargetApp))
                {
                    shortcutSettingsChanged = true;
                    string guid = Guid.NewGuid().ToString();
                    ShortcutSettings shortcutSettings = new ShortcutSettings
                    {
                        Id = guid,
                        Shortcut = shortcutMapping,
                        IsActive = true,
                    };
                    EditorSettings.ShortcutSettingsDictionary[guid] = shortcutSettings;
                    if (EditorSettings.ShortcutsByOperationType.TryGetValue(shortcutMapping.OperationType, out List<string>? mouseValue))
                    {
                        mouseValue.Add(guid);
                    }
                    else
                    {
                        EditorSettings.ShortcutsByOperationType[shortcutMapping.OperationType] = new List<string> { guid };
                    }
                }
            }

            // Handle key → mouse button mappings
            var keyToMouseMappings = _mappingService.GetKeyToMouseMappings();
            foreach (var mapping in keyToMouseMappings)
            {
                var shortcutMapping = new ShortcutKeyMapping
                {
                    OperationType = ShortcutOperationType.RemapKeyToMouse,
                    OriginalKeys = mapping.OriginalKeyCode.ToString(CultureInfo.InvariantCulture),
                    TargetApp = mapping.TargetApp,
                    TargetMouseButton = mapping.TargetMouseButton,
                };

                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s =>
                    s.Shortcut.OperationType == ShortcutOperationType.RemapKeyToMouse &&
                    s.Shortcut.OriginalKeys == shortcutMapping.OriginalKeys &&
                    s.Shortcut.TargetApp == shortcutMapping.TargetApp))
                {
                    shortcutSettingsChanged = true;
                    string guid = Guid.NewGuid().ToString();
                    ShortcutSettings shortcutSettings = new ShortcutSettings
                    {
                        Id = guid,
                        Shortcut = shortcutMapping,
                        IsActive = true,
                    };
                    EditorSettings.ShortcutSettingsDictionary[guid] = shortcutSettings;
                    if (EditorSettings.ShortcutsByOperationType.TryGetValue(ShortcutOperationType.RemapKeyToMouse, out List<string>? keyToMouseValue))
                    {
                        keyToMouseValue.Add(guid);
                    }
                    else
                    {
                        EditorSettings.ShortcutsByOperationType[ShortcutOperationType.RemapKeyToMouse] = new List<string> { guid };
                    }
                }
            }

            // Mark as inactive any settings that no longer exist in the mapping service
            foreach (ShortcutSettings shortcutSettings in EditorSettings.ShortcutSettingsDictionary.Values.ToList())
            {
                bool foundInService = false;

                if (shortcutSettings.Shortcut.OperationType == ShortcutOperationType.RemapText &&
                         !string.IsNullOrEmpty(shortcutSettings.Shortcut.OriginalKeys) &&
                         shortcutSettings.Shortcut.OriginalKeys.Split(';').Length == 1)
                {
                    if (int.TryParse(shortcutSettings.Shortcut.OriginalKeys, out int keyCode))
                    {
                        foundInService = keyToTextMappings.Any(m =>
                            m.OriginalKey == keyCode &&
                            m.TargetText == shortcutSettings.Shortcut.TargetText);
                    }
                }
                else if (shortcutSettings.Shortcut.OperationType == ShortcutOperationType.RemapShortcut &&
                         !string.IsNullOrEmpty(shortcutSettings.Shortcut.OriginalKeys) &&
                         shortcutSettings.Shortcut.OriginalKeys.Split(';').Length == 1)
                {
                    if (int.TryParse(shortcutSettings.Shortcut.OriginalKeys, out int keyCode))
                    {
                        foundInService = singleKeyMappings.Any(m =>
                            m.OriginalKey == keyCode &&
                            m.TargetKey == shortcutSettings.Shortcut.TargetKeys);
                    }
                }
                else if (shortcutSettings.Shortcut.OperationType == ShortcutOperationType.RemapMouseButton ||
                         shortcutSettings.Shortcut.OriginalKeys?.StartsWith("mouse_", StringComparison.Ordinal) == true)
                {
                    // Check mouse button mappings
                    if (shortcutSettings.Shortcut.OriginalKeys?.StartsWith("mouse_", StringComparison.Ordinal) == true &&
                        int.TryParse(shortcutSettings.Shortcut.OriginalKeys.AsSpan(6), out int buttonCode))
                    {
                        foundInService = mouseButtonMappings.Any(m =>
                            m.OriginalButtonCode == buttonCode &&
                            m.TargetApp == shortcutSettings.Shortcut.TargetApp);
                    }
                }
                else if (shortcutSettings.Shortcut.OperationType == ShortcutOperationType.RemapKeyToMouse)
                {
                    if (int.TryParse(shortcutSettings.Shortcut.OriginalKeys, out int keyCode))
                    {
                        foundInService = keyToMouseMappings.Any(m =>
                            m.OriginalKeyCode == keyCode &&
                            m.TargetApp == shortcutSettings.Shortcut.TargetApp);
                    }
                }
                else if (shortcutKeyMappings.Any(m => m.OriginalKeys == shortcutSettings.Shortcut.OriginalKeys))
                {
                    foundInService = true;
                }

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

        public static List<ShortcutSettings> GetShortcutSettingsByOperationType(ShortcutOperationType operationType)
        {
            List<ShortcutSettings> shortcutSettingsListForType = new List<ShortcutSettings>();

            if (EditorSettings.ShortcutsByOperationType.TryGetValue(operationType, out List<string>? guids))
            {
                foreach (string guid in guids)
                {
                    if (EditorSettings.ShortcutSettingsDictionary.TryGetValue(guid, out ShortcutSettings? shortcutSettings))
                    {
                        shortcutSettingsListForType.Add(shortcutSettings);
                    }
                }
            }

            return shortcutSettingsListForType;
        }

        public static void AddShortcutKeyMappingToSettings(ShortcutKeyMapping shortcutKeyMapping)
        {
            ShortcutSettings shortcutSettings = new ShortcutSettings
            {
                Id = Guid.NewGuid().ToString(),
                Shortcut = shortcutKeyMapping,
                IsActive = true,
            };

            EditorSettings.ShortcutSettingsDictionary[shortcutSettings.Id] = shortcutSettings;
            if (EditorSettings.ShortcutsByOperationType.TryGetValue(shortcutSettings.Shortcut.OperationType, out List<string>? value))
            {
                value.Add(shortcutSettings.Id);
            }
            else
            {
                EditorSettings.ShortcutsByOperationType[shortcutSettings.Shortcut.OperationType] = new List<string> { shortcutSettings.Id };
            }

            WriteSettings();
        }

        public static void RemoveShortcutKeyMappingFromSettings(string guid)
        {
            ShortcutOperationType operationType = EditorSettings.ShortcutSettingsDictionary[guid].Shortcut.OperationType;

            EditorSettings.ShortcutSettingsDictionary.Remove(guid);

            if (EditorSettings.ShortcutsByOperationType.TryGetValue(operationType, out List<string>? value))
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

        private static ShortcutKeyMapping ConvertMouseMappingToShortcutKeyMapping(Helpers.MouseMapping mapping)
        {
            var operationType = mapping.TargetType switch
            {
                "RunProgram" => ShortcutOperationType.RunProgram,
                "OpenUri" => ShortcutOperationType.OpenUri,
                "Text" => ShortcutOperationType.RemapText,
                _ => ShortcutOperationType.RemapMouseButton,
            };

            return new ShortcutKeyMapping
            {
                OperationType = operationType,
                OriginalKeys = $"mouse_{mapping.OriginalButtonCode}",
                TargetKeys = mapping.TargetType == "Key" ? mapping.TargetKeyCode.ToString(CultureInfo.InvariantCulture) : mapping.TargetShortcutKeys,
                TargetApp = mapping.TargetApp,
                TargetText = mapping.TargetText,
                ProgramPath = mapping.ProgramPath,
                ProgramArgs = mapping.ProgramArgs,
                UriToOpen = mapping.UriToOpen,
            };
        }
    }
}
