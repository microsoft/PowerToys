// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    /// <summary>
    /// Helper class for accessing and updating hotkey settings through ViewModels
    /// </summary>
    public static class HotkeyAccessorHelper
    {
        /// <summary>
        /// Gets hotkey settings from a ViewModel using the standardized accessor pattern
        /// </summary>
        public static HotkeySettings GetHotkeySettings(PageViewModelBase viewModel, string moduleName, int hotkeyID)
        {
            var hotkeyAccessors = viewModel?.GetAllHotkeyAccessors();
            if (hotkeyAccessors == null)
            {
                return null;
            }

            var accessorKey = GetAccessorKey(moduleName);
            var kvp = hotkeyAccessors.FirstOrDefault(x =>
                string.Equals(x.Key, accessorKey, StringComparison.OrdinalIgnoreCase));

            if (kvp.Key != null && hotkeyID >= 0 && hotkeyID < kvp.Value.Length)
            {
                return kvp.Value[hotkeyID].Value;
            }

            return null;
        }

        /// <summary>
        /// Updates hotkey settings in a ViewModel using the standardized accessor pattern
        /// </summary>
        public static bool UpdateHotkeySettings(PageViewModelBase viewModel, string moduleName, int hotkeyID, HotkeySettings newSettings)
        {
            var hotkeyAccessors = viewModel?.GetAllHotkeyAccessors();
            if (hotkeyAccessors == null)
            {
                return false;
            }

            var accessorKey = GetAccessorKey(moduleName);
            var kvp = hotkeyAccessors.FirstOrDefault(x =>
                string.Equals(x.Key, accessorKey, StringComparison.OrdinalIgnoreCase));

            if (kvp.Key != null && hotkeyID >= 0 && hotkeyID < kvp.Value.Length)
            {
                kvp.Value[hotkeyID].Value = newSettings;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines the correct accessor key for a given module name
        /// </summary>
        private static string GetAccessorKey(string moduleName)
        {
            var moduleKey = GetModuleKey(moduleName);
            return moduleKey == ModuleNames.MouseUtils ? moduleName?.ToLowerInvariant() : moduleKey;
        }

        /// <summary>
        /// Maps specific module names to their primary module key
        /// </summary>
        private static string GetModuleKey(string moduleName)
        {
            return moduleName?.ToLowerInvariant() switch
            {
                ModuleNames.MouseHighlighter or ModuleNames.MouseJump or
                ModuleNames.MousePointerCrosshairs or ModuleNames.FindMyMouse => ModuleNames.MouseUtils,
                _ => moduleName?.ToLowerInvariant(),
            };
        }
    }
}
