// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

            return settings;
        }

        public static void CorrelateServiceAndEditorMappings()
        {
            bool shortcutSettingsChanged = false;

            if (_mappingService is null)
            {
                return;
            }

            List<ShortcutKeyMapping> shortcutKeyMappings = _mappingService.GetShortcutMappings();
            foreach (ShortcutKeyMapping mapping in shortcutKeyMappings)
            {
                if (!EditorSettings.ShortcutSettingsDictionary.Values.Any(s => s.Shortcut.Equals(mapping)))
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

            foreach (ShortcutSettings shortcutSettings in EditorSettings.ShortcutSettingsDictionary.Values.ToList())
            {
                if (!shortcutKeyMappings.Any(m => m.Equals(shortcutSettings.Shortcut)))
                {
                    shortcutSettingsChanged = true;
                    ToggleShortcutKeyMappingActiveState(shortcutSettings.Id);
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
    }
}
