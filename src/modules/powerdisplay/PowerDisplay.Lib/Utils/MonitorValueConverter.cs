// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Provides conversion utilities for monitor hardware values.
    /// Use this class to convert between raw hardware values and display-friendly formats.
    /// </summary>
    public static class MonitorValueConverter
    {
        /// <summary>
        /// Formats a VCP color temperature value as a display name with preset name.
        /// </summary>
        /// <param name="vcpValue">The VCP preset value (e.g., 0x05).</param>
        /// <returns>Formatted string like "6500K (0x05)" or "sRGB (0x01)".</returns>
        public static string FormatColorTemperatureDisplay(int vcpValue)
        {
            return ColorTemperatureHelper.FormatColorTemperatureDisplayName(vcpValue);
        }
    }
}
