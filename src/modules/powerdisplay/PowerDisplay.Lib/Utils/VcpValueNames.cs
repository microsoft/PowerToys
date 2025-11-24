// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Provides human-readable names for VCP code values based on MCCS standard
    /// </summary>
    public static class VcpValueNames
    {
        // Dictionary<VcpCode, Dictionary<Value, Name>>
        private static readonly Dictionary<byte, Dictionary<int, string>> ValueNames = new()
        {
            // 0x14: Select Color Preset
            [0x14] = new Dictionary<int, string>
            {
                [0x01] = "sRGB",
                [0x02] = "Display Native",
                [0x03] = "4000K",
                [0x04] = "5000K",
                [0x05] = "6500K",
                [0x06] = "7500K",
                [0x08] = "9300K",
                [0x09] = "10000K",
                [0x0A] = "11500K",
                [0x0B] = "User 1",
                [0x0C] = "User 2",
                [0x0D] = "User 3",
            },

            // 0x60: Input Source
            [0x60] = new Dictionary<int, string>
            {
                [0x01] = "VGA-1",
                [0x02] = "VGA-2",
                [0x03] = "DVI-1",
                [0x04] = "DVI-2",
                [0x05] = "Composite Video 1",
                [0x06] = "Composite Video 2",
                [0x07] = "S-Video-1",
                [0x08] = "S-Video-2",
                [0x09] = "Tuner-1",
                [0x0A] = "Tuner-2",
                [0x0B] = "Tuner-3",
                [0x0C] = "Component Video 1",
                [0x0D] = "Component Video 2",
                [0x0E] = "Component Video 3",
                [0x0F] = "DisplayPort-1",
                [0x10] = "DisplayPort-2",
                [0x11] = "HDMI-1",
                [0x12] = "HDMI-2",
                [0x1B] = "USB-C",
            },

            // 0xD6: Power Mode
            [0xD6] = new Dictionary<int, string>
            {
                [0x01] = "On",
                [0x02] = "Standby",
                [0x03] = "Suspend",
                [0x04] = "Off (DPM)",
                [0x05] = "Off (Hard)",
            },

            // 0x8D: Audio Mute
            [0x8D] = new Dictionary<int, string>
            {
                [0x01] = "Muted",
                [0x02] = "Unmuted",
            },

            // 0xDC: Display Application
            [0xDC] = new Dictionary<int, string>
            {
                [0x00] = "Standard/Default",
                [0x01] = "Productivity",
                [0x02] = "Mixed",
                [0x03] = "Movie",
                [0x04] = "User Defined",
                [0x05] = "Games",
                [0x06] = "Sports",
                [0x07] = "Professional (calibration)",
                [0x08] = "Standard/Default with intermediate power consumption",
                [0x09] = "Standard/Default with low power consumption",
                [0x0A] = "Demonstration",
                [0xF0] = "Dynamic Contrast",
            },

            // 0xCC: OSD Language
            [0xCC] = new Dictionary<int, string>
            {
                [0x01] = "Chinese (traditional, Hantai)",
                [0x02] = "English",
                [0x03] = "French",
                [0x04] = "German",
                [0x05] = "Italian",
                [0x06] = "Japanese",
                [0x07] = "Korean",
                [0x08] = "Portuguese (Portugal)",
                [0x09] = "Russian",
                [0x0A] = "Spanish",
                [0x0B] = "Swedish",
                [0x0C] = "Turkish",
                [0x0D] = "Chinese (simplified, Kantai)",
                [0x0E] = "Portuguese (Brazil)",
                [0x0F] = "Arabic",
                [0x10] = "Bulgarian",
                [0x11] = "Croatian",
                [0x12] = "Czech",
                [0x13] = "Danish",
                [0x14] = "Dutch",
                [0x15] = "Estonian",
                [0x16] = "Finnish",
                [0x17] = "Greek",
                [0x18] = "Hebrew",
                [0x19] = "Hindi",
                [0x1A] = "Hungarian",
                [0x1B] = "Latvian",
                [0x1C] = "Lithuanian",
                [0x1D] = "Norwegian",
                [0x1E] = "Polish",
                [0x1F] = "Romanian",
                [0x20] = "Serbian",
                [0x21] = "Slovak",
                [0x22] = "Slovenian",
                [0x23] = "Thai",
                [0x24] = "Ukrainian",
                [0x25] = "Vietnamese",
            },

            // 0x62: Audio Speaker Volume
            [0x62] = new Dictionary<int, string>
            {
                [0x00] = "Mute",

                // Other values are continuous
            },

            // 0xDB: Image Mode (Dell monitors)
            [0xDB] = new Dictionary<int, string>
            {
                [0x00] = "Standard",
                [0x01] = "Multimedia",
                [0x02] = "Movie",
                [0x03] = "Game",
                [0x04] = "Sports",
                [0x05] = "Color Temperature",
                [0x06] = "Custom Color",
                [0x07] = "ComfortView",
            },
        };

        /// <summary>
        /// Get human-readable name for a VCP value
        /// </summary>
        /// <param name="vcpCode">VCP code (e.g., 0x14)</param>
        /// <param name="value">Value to translate</param>
        /// <returns>Name string like "sRGB" or null if unknown</returns>
        public static string? GetName(byte vcpCode, int value)
        {
            if (ValueNames.TryGetValue(vcpCode, out var codeValues))
            {
                if (codeValues.TryGetValue(value, out var name))
                {
                    return name;
                }
            }

            return null;
        }

        /// <summary>
        /// Get formatted display name for a VCP value (with hex value in parentheses)
        /// </summary>
        /// <param name="vcpCode">VCP code (e.g., 0x14)</param>
        /// <param name="value">Value to translate</param>
        /// <returns>Formatted string like "sRGB (0x01)" or "0x01" if unknown</returns>
        public static string GetFormattedName(byte vcpCode, int value)
        {
            var name = GetName(vcpCode, value);
            if (name != null)
            {
                return $"{name} (0x{value:X2})";
            }

            return $"0x{value:X2}";
        }

        /// <summary>
        /// Check if a VCP code has value name mappings
        /// </summary>
        /// <param name="vcpCode">VCP code to check</param>
        /// <returns>True if value names are available</returns>
        public static bool HasValueNames(byte vcpCode) => ValueNames.ContainsKey(vcpCode);
    }
}
