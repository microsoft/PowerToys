// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AllExperiments;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal sealed class LocalizationHelper
    {
        private static readonly Dictionary<(string ModuleName, int HotkeyID), string> HotkeyToResourceKeyMap = new()
        {
            // AdvancedPaste module mappings
            { (ModuleNames.AdvancedPaste, 0), "PasteAsPlainText_Shortcut" },
            { (ModuleNames.AdvancedPaste, 1), "AdvancedPasteUI_Shortcut" },
            { (ModuleNames.AdvancedPaste, 2), "PasteAsMarkdown_Shortcut" },
            { (ModuleNames.AdvancedPaste, 3), "PasteAsJson_Shortcut" },
            { (ModuleNames.AdvancedPaste, 4), "ImageToText" },
            { (ModuleNames.AdvancedPaste, 5), "PasteAsTxtFile" },
            { (ModuleNames.AdvancedPaste, 6), "PasteAsPngFile" },
            { (ModuleNames.AdvancedPaste, 7), "PasteAsHtmlFile" },
            { (ModuleNames.AdvancedPaste, 8), "TranscodeToMp3" },
            { (ModuleNames.AdvancedPaste, 9), "TranscodeToMp4" },

            // AlwaysOnTop module mappings
            { (ModuleNames.AlwaysOnTop, 0), "AlwaysOnTop_ActivationShortcut" },

            // ColorPicker module mappings
            { (ModuleNames.ColorPicker, 0), "Activation_Shortcut" },

            // CropAndLock module mappings
            { (ModuleNames.CropAndLock, 0), "CropAndLock_ReparentActivation_Shortcut" },
            { (ModuleNames.CropAndLock, 1), "CropAndLock_ThumbnailActivation_Shortcut" },

            // MeasureTool module mappings
            { (ModuleNames.MeasureTool, 0), "MeasureTool_ActivationShortcut" },

            // ShortcutGuide module mappings
            { (ModuleNames.ShortcutGuide, 0), "Activation_Shortcut" },

            // PowerOCR/TextExtractor module mappings
            { (ModuleNames.PowerOcr, 0), "Activation_Shortcut" },
            { (ModuleNames.TextExtractor, 0), "Activation_Shortcut" },

            // Workspaces module mappings
            { (ModuleNames.Workspaces, 0), "Workspaces_ActivationShortcut" },

            // Peek module mappings
            { (ModuleNames.Peek, 0), "Activation_Shortcut" },

            // PowerLauncher module mappings
            { (ModuleNames.PowerLauncher, 0), "PowerLauncher_OpenPowerLauncher" },

            // MouseUtils module mappings
            { (ModuleNames.MouseHighlighter, 0), "MouseUtils_MouseHighlighter_ActivationShortcut" },
            { (ModuleNames.MouseJump, 0), "MouseUtils_MouseJump_ActivationShortcut" },
            { (ModuleNames.MousePointerCrosshairs, 0), "MouseUtils_MousePointerCrosshairs_ActivationShortcut" },
            { (ModuleNames.FindMyMouse, 0), "MouseUtils_FindMyMouse_ActivationShortcut" },

            // Mouse without borders module mappings
            { (ModuleNames.MouseWithoutBorders, 0), "MouseWithoutBorders_ToggleEasyMouseShortcut" },
            { (ModuleNames.MouseWithoutBorders, 1), "MouseWithoutBorders_LockMachinesShortcut" },
            { (ModuleNames.MouseWithoutBorders, 2), "MouseWithoutBorders_Switch2AllPcShortcut" },
            { (ModuleNames.MouseWithoutBorders, 3), "MouseWithoutBorders_ReconnectShortcut" },

            // ZoomIt module mappings
            { (ModuleNames.ZoomIt, 0), "ZoomIt_Zoom_Shortcut" },
            { (ModuleNames.ZoomIt, 1), "ZoomIt_LiveZoom_Shortcut" },
            { (ModuleNames.ZoomIt, 2), "ZoomIt_Draw_Shortcut" },
            { (ModuleNames.ZoomIt, 3), "ZoomIt_Record_Shortcut" },
            { (ModuleNames.ZoomIt, 4), "ZoomIt_Snip_Shortcut" },
            { (ModuleNames.ZoomIt, 5), "ZoomIt_Break_Shortcut" },
            { (ModuleNames.ZoomIt, 6), "ZoomIt_DemoType_Shortcut" },
        };

        // Delegate for getting custom action names
        public static Func<string, int, string> GetCustomActionNameDelegate { get; set; }

        /// <summary>
        /// Gets the localized header text based on module name and hotkey name
        /// </summary>
        /// <param name="moduleName">The name of the module (case-insensitive)</param>
        /// <param name="hotkeyID">The ID of the hotkey</param>
        /// <returns>The localized header text, or the hotkey name if no resource is found</returns>
        public static string GetLocalizedHotkeyHeader(string moduleName, int hotkeyID)
        {
            if (string.IsNullOrEmpty(moduleName) || hotkeyID < 0)
            {
                return string.Empty;
            }

            var key = (moduleName.ToLowerInvariant(), hotkeyID);

            // Try to get from resource file using resource key mapping
            if (HotkeyToResourceKeyMap.TryGetValue(key, out string resourceKey))
            {
                var localizedText = GetLocalizedStringFromResource(resourceKey);
                if (!string.IsNullOrEmpty(localizedText))
                {
                    return localizedText;
                }
            }

            // Handle custom actions for AdvancedPaste, whose IDs start from 10
            if (moduleName.Equals(ModuleNames.AdvancedPaste, StringComparison.OrdinalIgnoreCase) && hotkeyID > 9)
            {
                // Try to get the custom action name using the delegate
                if (GetCustomActionNameDelegate != null)
                {
                    var actionID = hotkeyID - 10; // Adjust ID for custom actions
                    var customActionName = GetCustomActionNameDelegate(moduleName, actionID);
                    if (!string.IsNullOrEmpty(customActionName))
                    {
                        return customActionName;
                    }
                }

                // Fallback to resource
                var customActionText = GetLocalizedStringFromResource("PasteAsCustom_Shortcut");
                if (!string.IsNullOrEmpty(customActionText))
                {
                    return customActionText;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets a localized string from the resource file using ResourceLoaderInstance
        /// Tries multiple variations of the resource key to handle different naming conventions
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <returns>The localized string, or null if not found</returns>
        private static string GetLocalizedStringFromResource(string resourceKey)
        {
            if (string.IsNullOrEmpty(resourceKey))
            {
                return null;
            }

            try
            {
                var resourceLoader = ResourceLoaderInstance.ResourceLoader;
                var result = resourceLoader.GetString($"{resourceKey}/Header");
                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }
    }
}
