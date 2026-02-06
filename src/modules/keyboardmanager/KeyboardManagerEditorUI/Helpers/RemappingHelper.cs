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
using ManagedCommon;
using Windows.System;

namespace KeyboardManagerEditorUI.Helpers
{
    public static class RemappingHelper
    {
        public static bool SaveMapping(KeyboardMappingService mappingService, List<string> originalKeys, List<string> remappedKeys, bool isAppSpecific, string appName)
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
                            string targetKeys = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                            mappingService.AddSingleKeyMapping(originalKey, targetKeys);
                        }
                    }
                }
                else
                {
                    string originalKeysString = string.Join(";", originalKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));
                    string targetKeysString = string.Join(";", remappedKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

                    if (isAppSpecific && !string.IsNullOrEmpty(appName))
                    {
                        mappingService.AddShortcutMapping(originalKeysString, targetKeysString, appName);
                    }
                    else
                    {
                        mappingService.AddShortcutMapping(originalKeysString, targetKeysString);
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

        public static bool DeleteRemapping(KeyboardMappingService mappingService, Remapping remapping)
        {
            if (mappingService == null)
            {
                return false;
            }

            try
            {
                if (remapping.OriginalKeys.Count == 1)
                {
                    // Single key mapping
                    int originalKey = mappingService.GetKeyCodeFromName(remapping.OriginalKeys[0]);
                    if (originalKey != 0)
                    {
                        if (mappingService.DeleteSingleKeyMapping(originalKey))
                        {
                            // Save settings after successful deletion
                            return mappingService.SaveSettings();
                        }
                    }
                }
                else if (remapping.OriginalKeys.Count > 1)
                {
                    // Shortcut mapping
                    string originalKeysString = string.Join(";", remapping.OriginalKeys.Select(k => mappingService.GetKeyCodeFromName(k).ToString(CultureInfo.InvariantCulture)));

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
    }
}
