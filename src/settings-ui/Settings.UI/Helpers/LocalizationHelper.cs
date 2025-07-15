// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    internal sealed class LocalizationHelper
    {
        private static readonly Dictionary<(string ModuleName, string HotkeyName), string> HotkeyToResourceKeyMap = new()
        {
            // AdvancedPaste module mappings
            { ("advancedpaste", "AdvancedPasteUIShortcut"), "AdvancedPasteUI_Shortcut" },
            { ("advancedpaste", "PasteAsPlainTextShortcut"), "PasteAsPlainText_Shortcut" },
            { ("advancedpaste", "PasteAsMarkdownShortcut"), "PasteAsMarkdown_Shortcut" },
            { ("advancedpaste", "PasteAsJsonShortcut"), "PasteAsJson_Shortcut" },
            { ("advancedpaste", "ImageToTextShortcut"), "ImageToText" },
            { ("advancedpaste", "PasteAsTxtFileShortcut"), "PasteAsTxtFile" },
            { ("advancedpaste", "PasteAsPngFileShortcut"), "PasteAsPngFile" },
            { ("advancedpaste", "PasteAsHtmlFileShortcut"), "PasteAsHtmlFile" },
            { ("advancedpaste", "TranscodeToMp3Shortcut"), "TranscodeToMp3" },
            { ("advancedpaste", "TranscodeToMp4Shortcut"), "TranscodeToMp4" },

            // AlwaysOnTop module mappings
            { ("alwaysontop", "Hotkey"), "AlwaysOnTop_ActivationShortcut" },

            // ColorPicker module mappings
            { ("colorpicker", "ActivationShortcut"), "Activation_Shortcut" },

            // CropAndLock module mappings
            { ("cropandlock", "ThumbnailHotkey"), "CropAndLock_ThumbnailActivation_Shortcut" },
            { ("cropandlock", "ReparentHotkey"), "CropAndLock_ReparentActivation_Shortcut" },

            // MeasureTool module mappings
            { ("measuretool", "ActivationShortcut"), "MeasureTool_ActivationShortcut" },

            // ShortcutGuide module mappings
            { ("shortcutguide", "OpenShortcutGuide"), "Activation_Shortcut" },

            // PowerOCR/TextExtractor module mappings
            { ("textextractor", "ActivationShortcut"), "Activation_Shortcut" },

            // Workspaces module mappings
            { ("workspaces", "Hotkey"), "Workspaces_ActivationShortcut" },

            // Peek module mappings
            { ("peek", "ActivationShortcut"), "Activation_Shortcut" },

            // PowerLauncher module mappings
            { ("powerlauncher", "OpenPowerLauncher"), "PowerLauncher_OpenPowerLauncher" },

            // MouseUtils module mappings
            { ("mousehighlighter", "ActivationShortcut"), "MouseUtils_MouseHighlighter_ActivationShortcut" },
            { ("mousejump", "ActivationShortcut"), "MouseUtils_MouseJump_ActivationShortcut" },
            { ("mousepointercrosshairs", "ActivationShortcut"), "MouseUtils_MousePointerCrosshairs_ActivationShortcut" },
            { ("findmymouse", "ActivationShortcut"), "MouseUtils_FindMyMouse_ActivationShortcut" },

            // Mouse without borders module mappings
            { ("mousewithoutborders", "HotKeySwitch2AllPC"), "MouseWithoutBorders_Switch2AllPcShortcut" },
            { ("mousewithoutborders", "HotKeyLockMachine"), "MouseWithoutBorders_LockMachinesShortcut" },
            { ("mousewithoutborders", "HotKeyReconnect"), "MouseWithoutBorders_ReconnectShortcut" },
            { ("mousewithoutborders", "HotKeyToggleEasyMouse"), "MouseWithoutBorders_ToggleEasyMouseShortcut" },
        };

        // Delegate for getting custom action names
        public static Func<string, int, string> GetCustomActionNameDelegate { get; set; }

        /// <summary>
        /// Gets the localized header text based on module name and hotkey name
        /// </summary>
        /// <param name="moduleName">The name of the module (case-insensitive)</param>
        /// <param name="hotkeyName">The name of the hotkey</param>
        /// <returns>The localized header text, or the hotkey name if no resource is found</returns>
        public static string GetLocalizedHotkeyHeader(string moduleName, string hotkeyName)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(hotkeyName))
            {
                return hotkeyName ?? string.Empty;
            }

            var key = (moduleName.ToLowerInvariant(), hotkeyName);

            // Try to get from resource file using resource key mapping
            if (HotkeyToResourceKeyMap.TryGetValue(key, out string resourceKey))
            {
                var localizedText = GetLocalizedStringFromResource(resourceKey);
                if (!string.IsNullOrEmpty(localizedText))
                {
                    return localizedText;
                }
            }

            // Handle custom actions for AdvancedPaste
            if (moduleName.Equals("advancedpaste", StringComparison.OrdinalIgnoreCase) &&
                hotkeyName.StartsWith("CustomAction_", StringComparison.OrdinalIgnoreCase))
            {
                // Try to get the custom action name using the delegate
                if (GetCustomActionNameDelegate != null &&
                    int.TryParse(hotkeyName.AsSpan("CustomAction_".Length), out int actionId))
                {
                    var customActionName = GetCustomActionNameDelegate(moduleName, actionId);
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

            // Try to generate resource key from hotkey name
            var fallbackResourceKey = GenerateResourceKeyFromHotkeyName(moduleName, hotkeyName);
            var fallbackText = GetLocalizedStringFromResource(fallbackResourceKey);
            if (!string.IsNullOrEmpty(fallbackText))
            {
                return fallbackText;
            }

            // Final fallback: return the hotkey name as-is
            return hotkeyName;
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
                if (resourceLoader != null)
                {
                    // Try different variations of the resource key
                    string[] keyVariations =
                    {
                        $"{resourceKey}.Header",  // Try with .Header suffix first
                        resourceKey,              // Try the key as-is
                        $"{resourceKey}/Header",  // Try with /Header suffix (some resources use this format)
                        $"{resourceKey}_Header",   // Try with _Header suffix
                    };

                    foreach (var keyVariation in keyVariations)
                    {
                        try
                        {
                            var result = resourceLoader.GetString(keyVariation);
                            if (!string.IsNullOrEmpty(result))
                            {
                                return result;
                            }
                        }
                        catch
                        {
                            // Continue to next variation
                            continue;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If resource loading fails, return null to allow fallback
            }

            return null;
        }

        /// <summary>
        /// Generates a resource key from module name and hotkey name
        /// </summary>
        /// <param name="moduleName">The module name</param>
        /// <param name="hotkeyName">The hotkey name</param>
        /// <returns>Generated resource key</returns>
        private static string GenerateResourceKeyFromHotkeyName(string moduleName, string hotkeyName)
        {
            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(hotkeyName))
            {
                return string.Empty;
            }

            // Clean module name - capitalize first letter and make rest lowercase
            var cleanModuleName = char.ToUpperInvariant(moduleName[0]) + moduleName.Substring(1).ToLowerInvariant();

            // Clean hotkey name
            string cleanHotkeyName = hotkeyName;
            if (hotkeyName.EndsWith("Shortcut", StringComparison.OrdinalIgnoreCase))
            {
                cleanHotkeyName = hotkeyName.Substring(0, hotkeyName.Length - "Shortcut".Length);
            }
            else if (cleanHotkeyName.EndsWith("Hotkey", StringComparison.OrdinalIgnoreCase))
            {
                cleanHotkeyName = cleanHotkeyName.Substring(0, cleanHotkeyName.Length - "Hotkey".Length);
            }

            // Generate resource key pattern: ModuleName_HotkeyName_Shortcut
            return $"{cleanModuleName}_{cleanHotkeyName}_Shortcut";
        }
    }
}
