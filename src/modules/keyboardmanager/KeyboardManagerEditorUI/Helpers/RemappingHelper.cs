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
        public static bool SaveMapping(KeyboardMappingService mappingService, List<string> originalKeys, List<string> remappedKeys, bool isAppSpecific, string appName, bool saveToSettings = true)
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

                if (originalKeys.Count == 1)
                {
                    int originalKey = mappingService.GetKeyCodeFromName(originalKeys[0]);

                    if (originalKey != 0)
                    {
                        string targetKeysString = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                        ShortcutKeyMapping shortcutKeyMapping = new ShortcutKeyMapping()
                        {
                            OperationType = ShortcutOperationType.RemapShortcut,
                            OriginalKeys = originalKey.ToString(CultureInfo.InvariantCulture),
                            TargetKeys = targetKeysString,
                            TargetApp = isAppSpecific ? appName : string.Empty,
                        };
                        if (remappedKeys.Count == 1)
                        {
                            int targetKey = mappingService.GetKeyCodeFromName(remappedKeys[0]);
                            if (targetKey != 0)
                            {
                                mappingService.AddSingleKeyMapping(originalKey, targetKey);
                            }
                        }
                        else
                        {
                            mappingService.AddSingleKeyMapping(originalKey, targetKeysString);
                        }

                        if (saveToSettings)
                        {
                            SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
                        }
                    }
                }
                else
                {
                    string originalKeysString = string.Join(";", originalKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                    string targetKeysString = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                    ShortcutKeyMapping shortcutKeyMapping = new ShortcutKeyMapping()
                    {
                        OperationType = ShortcutOperationType.RemapShortcut,
                        OriginalKeys = originalKeysString,
                        TargetKeys = targetKeysString,
                        TargetApp = isAppSpecific ? appName : string.Empty,
                    };

                    if (isAppSpecific && !string.IsNullOrEmpty(appName))
                    {
                        mappingService.AddShortcutMapping(originalKeysString, targetKeysString, appName);
                    }
                    else
                    {
                        mappingService.AddShortcutMapping(originalKeysString, targetKeysString);
                    }

                    if (saveToSettings)
                    {
                        SettingsManager.AddShortcutKeyMappingToSettings(shortcutKeyMapping);
                    }
                }

                return mappingService.SaveSettings();
            }
            catch (Exception ex)
            {
                Logger.LogError("Error saving mapping: " + ex.Message);
                return false;
            }
        }

        public static bool DeleteRemapping(KeyboardMappingService mappingService, Remapping remapping, bool deleteFromSettings = true)
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
                        if (mappingService.DeleteSingleKeyMapping(originalKey))
                        {
                            if (deleteFromSettings)
                            {
                                SettingsManager.RemoveShortcutKeyMappingFromSettings(remapping.Id);
                            }

                            return mappingService.SaveSettings();
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

                    if (deleteResult && deleteFromSettings)
                    {
                        SettingsManager.RemoveShortcutKeyMappingFromSettings(remapping.Id);
                    }

                    return deleteResult ? mappingService.SaveSettings() : false;
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

        /// <summary>
        /// Returns the sort order for a modifier key in the standard display order:
        /// Win(0) → Ctrl(1) → Alt(2) → Shift(3).
        /// Non-modifier (action) keys return 4.
        /// This matches the old C++ GetKeyVector behavior.
        /// </summary>
        public static int GetModifierSortOrder(VirtualKey key)
        {
            return key switch
            {
                VirtualKey.LeftWindows or VirtualKey.RightWindows => 0,
                VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => 1,
                VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => 2,
                VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => 3,
                _ => 4,
            };
        }

        /// <summary>
        /// Sorts a list of modifier keys in the standard display order:
        /// Win → Ctrl → Alt → Shift.
        /// </summary>
        public static void SortModifierKeys(List<VirtualKey> modifierKeys)
        {
            modifierKeys.Sort((a, b) => GetModifierSortOrder(a).CompareTo(GetModifierSortOrder(b)));
        }
    }
}
