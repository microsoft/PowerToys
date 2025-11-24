// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Provides conversion utilities for monitor hardware values.
    /// Use this class to convert between raw hardware values and display-friendly formats.
    /// </summary>
    public static class MonitorValueConverter
    {
        /// <summary>
        /// Standard VCP color temperature preset to Kelvin value mapping.
        /// Based on MCCS (Monitor Control Command Set) standard.
        /// </summary>
        private static readonly Dictionary<int, int> VcpToKelvinMap = new()
        {
            [0x03] = 4000,
            [0x04] = 5000,
            [0x05] = 6500,
            [0x06] = 7500,
            [0x08] = 9300,
            [0x09] = 10000,
            [0x0A] = 11500,
        };

        /// <summary>
        /// Reverse mapping from Kelvin to VCP value. Auto-generated from VcpToKelvinMap.
        /// </summary>
        private static readonly Dictionary<int, int> KelvinToVcpMap = BuildReverseMap(VcpToKelvinMap);

        /// <summary>
        /// Builds a reverse lookup dictionary from the source mapping.
        /// </summary>
        private static Dictionary<int, int> BuildReverseMap(Dictionary<int, int> source)
        {
            var result = new Dictionary<int, int>();
            foreach (var kvp in source)
            {
                result[kvp.Value] = kvp.Key;
            }

            return result;
        }

        /// <summary>
        /// Converts a VCP color temperature preset value to approximate Kelvin temperature.
        /// </summary>
        /// <param name="vcpValue">The VCP preset value (e.g., 0x05).</param>
        /// <returns>The Kelvin temperature (e.g., 6500), or 0 if unknown.</returns>
        public static int VcpToKelvin(int vcpValue)
        {
            return VcpToKelvinMap.TryGetValue(vcpValue, out var kelvin) ? kelvin : 0;
        }

        /// <summary>
        /// Converts a Kelvin temperature to VCP color temperature preset value.
        /// </summary>
        /// <param name="kelvin">The Kelvin temperature (e.g., 6500).</param>
        /// <returns>The VCP preset value (e.g., 0x05), or 0x05 (6500K) as default if unknown.</returns>
        public static int KelvinToVcp(int kelvin)
        {
            return KelvinToVcpMap.TryGetValue(kelvin, out var vcpValue) ? vcpValue : 0x05;
        }

        /// <summary>
        /// Formats a VCP color temperature value as a Kelvin string for display.
        /// </summary>
        /// <param name="vcpValue">The VCP preset value (e.g., 0x05).</param>
        /// <returns>Formatted string like "6500K" or "Unknown (0x05)" if not a standard preset.</returns>
        public static string FormatVcpAsKelvin(int vcpValue)
        {
            var kelvin = VcpToKelvin(vcpValue);
            if (kelvin > 0)
            {
                return $"{kelvin}K";
            }

            // Use VcpValueNames for special presets like sRGB, User 1, etc.
            var name = VcpValueNames.GetName(ColorTemperatureHelper.ColorTemperatureVcpCode, vcpValue);
            return name ?? $"Unknown (0x{vcpValue:X2})";
        }

        /// <summary>
        /// Formats a VCP color temperature value as a display name with preset name.
        /// </summary>
        /// <param name="vcpValue">The VCP preset value (e.g., 0x05).</param>
        /// <returns>Formatted string like "6500K (0x05)" or "sRGB (0x01)".</returns>
        public static string FormatColorTemperatureDisplay(int vcpValue)
        {
            return ColorTemperatureHelper.FormatColorTemperatureDisplayName(vcpValue);
        }

        /// <summary>
        /// Gets the preset name for a VCP color temperature value.
        /// </summary>
        /// <param name="vcpValue">The VCP preset value (e.g., 0x05).</param>
        /// <returns>Preset name like "6500K", "sRGB", or null if unknown.</returns>
        public static string? GetColorTemperaturePresetName(int vcpValue)
        {
            return VcpValueNames.GetName(ColorTemperatureHelper.ColorTemperatureVcpCode, vcpValue);
        }

        /// <summary>
        /// Formats a percentage value for display.
        /// </summary>
        /// <param name="value">Value (0-100).</param>
        /// <returns>Formatted string like "50%".</returns>
        public static string FormatPercentage(int value) => $"{value}%";

        /// <summary>
        /// Formats a brightness value for display.
        /// </summary>
        /// <param name="brightness">Brightness value (0-100).</param>
        /// <returns>Formatted string like "50%".</returns>
        public static string FormatBrightness(int brightness) => FormatPercentage(brightness);

        /// <summary>
        /// Formats a contrast value for display.
        /// </summary>
        /// <param name="contrast">Contrast value (0-100).</param>
        /// <returns>Formatted string like "50%".</returns>
        public static string FormatContrast(int contrast) => FormatPercentage(contrast);

        /// <summary>
        /// Formats a volume value for display.
        /// </summary>
        /// <param name="volume">Volume value (0-100).</param>
        /// <returns>Formatted string like "50%".</returns>
        public static string FormatVolume(int volume) => FormatPercentage(volume);

        /// <summary>
        /// Checks if a VCP value represents a known color temperature preset.
        /// </summary>
        /// <param name="vcpValue">The VCP preset value.</param>
        /// <returns>True if the value is a known preset.</returns>
        public static bool IsKnownColorTemperaturePreset(int vcpValue)
        {
            return VcpToKelvinMap.ContainsKey(vcpValue) ||
                   VcpValueNames.GetName(ColorTemperatureHelper.ColorTemperatureVcpCode, vcpValue) != null;
        }
    }
}
