// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using PowerDisplay.Common.Drivers;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Helper class for color temperature preset computation.
    /// Provides shared logic for computing available color presets from VCP capabilities.
    /// </summary>
    public static class ColorTemperatureHelper
    {
        /// <summary>
        /// Computes available color temperature presets from VCP value data.
        /// </summary>
        /// <param name="colorTemperatureValues">
        /// Collection of tuples containing (VcpValue, Name) for each color temperature preset.
        /// The VcpValue is the VCP value, Name is the name from capabilities string if available.
        /// </param>
        /// <returns>Sorted list of ColorPresetItem objects.</returns>
        public static List<ColorPresetItem> ComputeColorPresets(IEnumerable<(int VcpValue, string? Name)> colorTemperatureValues)
        {
            if (colorTemperatureValues == null)
            {
                return new List<ColorPresetItem>();
            }

            var presetList = new List<ColorPresetItem>();

            foreach (var item in colorTemperatureValues)
            {
                var displayName = FormatColorTemperatureDisplayName(item.VcpValue, item.Name);
                presetList.Add(new ColorPresetItem(item.VcpValue, displayName));
            }

            // Sort by VCP value for consistent ordering
            return presetList.OrderBy(p => p.VcpValue).ToList();
        }

        /// <summary>
        /// Formats a color temperature display name.
        /// Uses VcpNames for standard VCP value mappings if no custom name is provided.
        /// </summary>
        /// <param name="vcpValue">The VCP value.</param>
        /// <param name="customName">Optional custom name from capabilities string.</param>
        /// <returns>Formatted display name.</returns>
        public static string FormatColorTemperatureDisplayName(int vcpValue, string? customName = null)
        {
            // Priority: use name from VCP capabilities if available
            if (!string.IsNullOrEmpty(customName))
            {
                return customName;
            }

            // Fall back to standard VCP value name from shared library
            return VcpNames.GetValueName(NativeConstants.VcpCodeSelectColorPreset, vcpValue)
                   ?? "Manufacturer Defined";
        }

        /// <summary>
        /// Formats a display name for a custom (non-preset) color temperature value.
        /// Used when the current value is not in the available preset list.
        /// </summary>
        /// <param name="vcpValue">The VCP value.</param>
        /// <returns>Formatted display name with "Custom" indicator.</returns>
        public static string FormatCustomColorTemperatureDisplayName(int vcpValue)
        {
            var standardName = VcpNames.GetValueName(NativeConstants.VcpCodeSelectColorPreset, vcpValue);
            return string.IsNullOrEmpty(standardName)
                ? "Custom"
                : $"{standardName} (Custom)";
        }
    }
}
