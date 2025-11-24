// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
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
        /// VCP code for Select Color Preset (Color Temperature).
        /// </summary>
        public const byte ColorTemperatureVcpCode = 0x14;

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
        /// Uses VcpValueNames for standard VCP value mappings if no custom name is provided.
        /// </summary>
        /// <param name="vcpValue">The VCP value.</param>
        /// <param name="customName">Optional custom name from capabilities string.</param>
        /// <returns>Formatted display name with hex value.</returns>
        public static string FormatColorTemperatureDisplayName(int vcpValue, string? customName = null)
        {
            var hexValue = $"0x{vcpValue:X2}";

            // Priority: use name from VCP capabilities if available
            if (!string.IsNullOrEmpty(customName))
            {
                return $"{customName} ({hexValue})";
            }

            // Fall back to standard VCP value name from shared library
            var standardName = VcpValueNames.GetName(ColorTemperatureVcpCode, vcpValue);
            if (standardName != null)
            {
                return $"{standardName} ({hexValue})";
            }

            // Unknown value
            return $"Manufacturer Defined ({hexValue})";
        }

        /// <summary>
        /// Formats a display name for a custom (non-preset) color temperature value.
        /// Used when the current value is not in the available preset list.
        /// </summary>
        /// <param name="vcpValue">The VCP value.</param>
        /// <returns>Formatted display name with "Custom" indicator.</returns>
        public static string FormatCustomColorTemperatureDisplayName(int vcpValue)
        {
            var standardName = VcpValueNames.GetName(ColorTemperatureVcpCode, vcpValue);
            return string.IsNullOrEmpty(standardName)
                ? $"Custom (0x{vcpValue:X2})"
                : $"{standardName} (0x{vcpValue:X2}) - Custom";
        }
    }
}
