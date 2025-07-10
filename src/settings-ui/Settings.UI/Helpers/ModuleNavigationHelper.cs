// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.PowerToys.Settings.UI.Views;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class ModuleNavigationHelper
    {
        private static readonly Dictionary<string, Type> ModulePageMapping = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "AdvancedPaste", typeof(AdvancedPastePage) },
            { "AlwaysOnTop", typeof(AlwaysOnTopPage) },
            { "Awake", typeof(AwakePage) },
            { "ColorPicker", typeof(ColorPickerPage) },
            { "CmdPal", typeof(CmdPalPage) },
            { "CropAndLock", typeof(CropAndLockPage) },
            { "EnvironmentVariables", typeof(EnvironmentVariablesPage) },
            { "FancyZones", typeof(FancyZonesPage) },
            { "FileLocksmith", typeof(FileLocksmithPage) },
            { "Hosts", typeof(HostsPage) },
            { "ImageResizer", typeof(ImageResizerPage) },
            { "KeyboardManager", typeof(KeyboardManagerPage) },
            { "MeasureTool", typeof(MeasureToolPage) },
            { "MouseHighlighter", typeof(MouseUtilsPage) },
            { "MouseJump", typeof(MouseUtilsPage) },
            { "MousePointerCrosshairs", typeof(MouseUtilsPage) },
            { "FindMyMouse", typeof(MouseUtilsPage) },
            { "MouseWithoutBorders", typeof(MouseWithoutBordersPage) },
            { "NewPlus", typeof(NewPlusPage) },
            { "Peek", typeof(PeekPage) },
            { "PowerAccent", typeof(PowerAccentPage) },
            { "PowerLauncher", typeof(PowerLauncherPage) },
            { "PowerOCR", typeof(PowerOcrPage) },
            { "PowerPreview", typeof(PowerPreviewPage) },
            { "PowerRename", typeof(PowerRenamePage) },
            { "RegistryPreview", typeof(RegistryPreviewPage) },
            { "ShortcutGuide", typeof(ShortcutGuidePage) },
            { "Workspaces", typeof(WorkspacesPage) },
            { "ZoomIt", typeof(ZoomItPage) },
        };

        /// <summary>
        /// Navigates to the settings page for the specified module
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        /// <returns>True if navigation was successful, false otherwise</returns>
        public static bool NavigateToModulePage(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            if (ModulePageMapping.TryGetValue(moduleName, out Type pageType))
            {
                return NavigationService.Navigate(pageType);
            }

            return false;
        }

        /// <summary>
        /// Gets the page type for the specified module
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        /// <returns>The page type if found, null otherwise</returns>
        public static Type GetModulePageType(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return null;
            }

            ModulePageMapping.TryGetValue(moduleName, out Type pageType);
            return pageType;
        }

        /// <summary>
        /// Checks if a module has a corresponding settings page
        /// </summary>
        /// <param name="moduleName">The name of the module</param>
        /// <returns>True if the module has a settings page, false otherwise</returns>
        public static bool HasModulePage(string moduleName)
        {
            return !string.IsNullOrEmpty(moduleName) && ModulePageMapping.ContainsKey(moduleName);
        }
    }
}
