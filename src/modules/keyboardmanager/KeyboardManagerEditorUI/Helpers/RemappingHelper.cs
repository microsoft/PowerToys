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
using ManagedCommon;
using Windows.System;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class RemappingHelper
    {
        public static bool SaveMapping(KeyboardMappingService mappingService, List<string> originalKeys, List<string> remappedKeys, bool isAppSpecific, string appName, bool exactMatch = false, bool saveToSettings = true)
        {
            if (mappingService == null)
            {
                Logger.LogError("Mapping service is null, cannot save mapping");
                return false;
            }

            try
            {
                if (originalKeys == null || originalKeys.Count == 0 || remappedKeys == null || remappedKeys.Count == 0)
                {
                    return false;
                }

                ShortcutKeyMapping shortcutKeyMapping;
                bool added;
                if (originalKeys.Count == 1)
                {
                    int originalKey = mappingService.GetKeyCodeFromName(originalKeys[0]);
                    if (originalKey == 0)
                    {
                        return false;
                    }

                    string targetKeysString = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                    shortcutKeyMapping = new ShortcutKeyMapping()
                    {
                        OperationType = ShortcutOperationType.RemapShortcut,
                        OriginalKeys = originalKey.ToString(CultureInfo.InvariantCulture),
                        TargetKeys = targetKeysString,
                    };
                    if (remappedKeys.Count == 1)
                    {
                        int targetKey = mappingService.GetKeyCodeFromName(remappedKeys[0]);
                        added = targetKey != 0 && mappingService.AddSingleKeyMapping(originalKey, targetKey);
                    }
                    else
                    {
                        added = mappingService.AddSingleKeyMapping(originalKey, targetKeysString);
                    }
                }
                else
                {
                    string originalKeysString = string.Join(";", originalKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                    string targetKeysString = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                    shortcutKeyMapping = new ShortcutKeyMapping()
                    {
                        OperationType = ShortcutOperationType.RemapShortcut,
                        OriginalKeys = originalKeysString,
                        TargetKeys = targetKeysString,
                        TargetApp = isAppSpecific ? appName : string.Empty,
                        ExactMatch = exactMatch,
                    };

                    added = mappingService.AddShortcutMapping(originalKeysString, targetKeysString, shortcutKeyMapping.TargetApp, exactMatch: exactMatch);
                }

                return CompleteSave(mappingService, shortcutKeyMapping, added, saveToSettings);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving mapping: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Publishes an editor row only after both the native add and configuration save succeed.
        /// Failed saves remove the new in-memory mapping so a later save cannot persist a phantom row.
        /// </summary>
        public static bool CompleteSave(KeyboardMappingService mappingService, ShortcutKeyMapping mapping, bool added, bool saveToSettings = true)
        {
            if (!added)
            {
                return false;
            }

            if (!mappingService.SaveSettings())
            {
                if (int.TryParse(mapping.OriginalKeys, out int originalKey))
                {
                    if (mapping.OperationType == ShortcutOperationType.RemapText)
                    {
                        mappingService.DeleteSingleKeyToTextMapping(originalKey);
                    }
                    else
                    {
                        mappingService.DeleteSingleKeyMapping(originalKey);
                    }
                }
                else
                {
                    mappingService.DeleteShortcutMapping(mapping.OriginalKeys, mapping.TargetApp);
                }

                return false;
            }

            if (saveToSettings)
            {
                SettingsManager.AddShortcutKeyMappingToSettings(mapping);
            }

            return true;
        }

        public static bool RestoreMapping(KeyboardMappingService mappingService, ShortcutKeyMapping mapping)
        {
            if (int.TryParse(mapping.OriginalKeys, out int originalKey))
            {
                return mapping.OperationType == ShortcutOperationType.RemapText
                    ? mappingService.AddSingleKeyToTextMapping(originalKey, mapping.TargetText)
                    : mappingService.AddSingleKeyMapping(originalKey, mapping.TargetKeys);
            }

            return mapping.OperationType == ShortcutOperationType.RemapText
                ? mappingService.AddShortcutMapping(mapping.OriginalKeys, mapping.TargetText, mapping.TargetApp, ShortcutOperationType.RemapText, mapping.ExactMatch)
                : mappingService.AddShortcutMapping(mapping);
        }

        public static bool CompleteDelete(KeyboardMappingService mappingService, string mappingId, bool deleted)
        {
            if (!deleted)
            {
                return false;
            }

            if (mappingService.SaveSettings())
            {
                return true;
            }

            // The file still represents the old row. Restore the service's in-memory view so a
            // retry, or a later save of another row, cannot silently drop this mapping.
            if (SettingsManager.EditorSettings.ShortcutSettingsDictionary.TryGetValue(mappingId, out ShortcutSettings? settings) &&
                !RestoreMapping(mappingService, settings.Shortcut))
            {
                Logger.LogError("Failed to restore the mapping after an unsuccessful delete");
            }

            return false;
        }

        public static bool DeleteRemapping(KeyboardMappingService mappingService, Remapping remapping, bool deleteFromSettings = true, bool saveSettings = true)
        {
            if (mappingService == null)
            {
                return false;
            }

            try
            {
                if (remapping.Shortcut.Count == 1)
                {
                    // Single key mapping
                    int originalKey = mappingService.GetKeyCodeFromName(remapping.Shortcut[0]);
                    if (originalKey != 0)
                    {
                        bool deleted = mappingService.DeleteSingleKeyMapping(originalKey);
                        if (saveSettings ? CompleteDelete(mappingService, remapping.Id, deleted) : deleted)
                        {
                            if (deleteFromSettings)
                            {
                                SettingsManager.RemoveShortcutKeyMappingFromSettings(remapping.Id);
                            }

                            return true;
                        }
                    }
                }
                else if (remapping.Shortcut.Count > 1)
                {
                    // Shortcut mapping
                    string originalKeysString = string.Join(";", remapping.Shortcut.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                    bool deleteResult;
                    if (!remapping.IsAllApps && !string.IsNullOrEmpty(remapping.AppName))
                    {
                        // App-specific shortcut key mapping
                        deleteResult = mappingService.DeleteShortcutMapping(originalKeysString, remapping.AppName);
                    }
                    else
                    {
                        // Global shortcut key mapping
                        deleteResult = mappingService.DeleteShortcutMapping(originalKeysString);
                    }

                    if (!(saveSettings ? CompleteDelete(mappingService, remapping.Id, deleteResult) : deleteResult))
                    {
                        return false;
                    }

                    if (deleteFromSettings)
                    {
                        SettingsManager.RemoveShortcutKeyMappingFromSettings(remapping.Id);
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error deleting remapping: {ex.Message}");
                return false;
            }
        }

        public static bool IsModifierKey(VirtualKey key)
        {
            return key == VirtualKey.Control
                || key == VirtualKey.LeftControl
                || key == VirtualKey.RightControl
                || key == VirtualKey.Menu
                || key == VirtualKey.LeftMenu
                || key == VirtualKey.RightMenu
                || key == VirtualKey.Shift
                || key == VirtualKey.LeftShift
                || key == VirtualKey.RightShift
                || key == VirtualKey.LeftWindows
                || key == VirtualKey.RightWindows;
        }
    }
}
