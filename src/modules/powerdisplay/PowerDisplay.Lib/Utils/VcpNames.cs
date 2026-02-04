// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using PowerDisplay.Common.Models;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Provides human-readable names for VCP codes and their values based on MCCS v2.2a specification.
    /// Combines VCP code names (e.g., 0x10 = "Brightness") and VCP value names (e.g., 0x14:0x05 = "6500K").
    /// </summary>
    public static class VcpNames
    {
        /// <summary>
        /// VCP code to name mapping
        /// </summary>
        private static readonly Dictionary<byte, string> CodeNames = new()
        {
            // Control codes (0x00-0x0F)
            { 0x00, "Code Page" },
            { 0x01, "Degauss" },
            { 0x02, "New Control Value" },
            { 0x03, "Soft Controls" },

            // Preset operations (0x04-0x0A)
            { 0x04, "Restore Factory Defaults" },
            { 0x05, "Restore Brightness and Contrast" },
            { 0x06, "Restore Factory Geometry" },
            { 0x08, "Restore Color Defaults" },
            { 0x0A, "Restore Factory TV Defaults" },

            // Color temperature codes
            { 0x0B, "Color Temperature Increment" },
            { 0x0C, "Color Temperature Request" },
            { 0x0E, "Clock" },
            { 0x0F, "Color Saturation" },

            // Image adjustment codes
            { 0x10, "Brightness" },
            { 0x11, "Flesh Tone Enhancement" },
            { 0x12, "Contrast" },
            { 0x13, "Backlight Control" },
            { 0x14, "Select Color Preset" },
            { 0x16, "Video Gain: Red" },
            { 0x17, "User Color Vision Compensation" },
            { 0x18, "Video Gain: Green" },
            { 0x1A, "Video Gain: Blue" },
            { 0x1C, "Focus" },
            { 0x1E, "Auto Setup" },
            { 0x1F, "Auto Color Setup" },

            // Geometry controls (0x20-0x4C)
            { 0x20, "Horizontal Position" },
            { 0x22, "Horizontal Size" },
            { 0x24, "Horizontal Pincushion" },
            { 0x26, "Horizontal Pincushion Balance" },
            { 0x28, "Horizontal Convergence R/B" },
            { 0x29, "Horizontal Convergence M/G" },
            { 0x2A, "Horizontal Linearity" },
            { 0x2C, "Horizontal Linearity Balance" },
            { 0x2E, "Gray Scale Expansion" },
            { 0x30, "Vertical Position" },
            { 0x32, "Vertical Size" },
            { 0x34, "Vertical Pincushion" },
            { 0x36, "Vertical Pincushion Balance" },
            { 0x38, "Vertical Convergence R/B" },
            { 0x39, "Vertical Convergence M/G" },
            { 0x3A, "Vertical Linearity" },
            { 0x3C, "Vertical Linearity Balance" },
            { 0x3E, "Clock Phase" },

            // Miscellaneous codes
            { 0x40, "Horizontal Parallelogram" },
            { 0x41, "Vertical Parallelogram" },
            { 0x42, "Horizontal Keystone" },
            { 0x43, "Vertical Keystone" },
            { 0x44, "Rotation" },
            { 0x46, "Top Corner Flare" },
            { 0x48, "Top Corner Hook" },
            { 0x4A, "Bottom Corner Flare" },
            { 0x4C, "Bottom Corner Hook" },

            // Advanced codes
            { 0x52, "Active Control" },
            { 0x54, "Performance Preservation" },
            { 0x56, "Horizontal Moire" },
            { 0x58, "Vertical Moire" },
            { 0x59, "6 Axis Saturation: Red" },
            { 0x5A, "6 Axis Saturation: Yellow" },
            { 0x5B, "6 Axis Saturation: Green" },
            { 0x5C, "6 Axis Saturation: Cyan" },
            { 0x5D, "6 Axis Saturation: Blue" },
            { 0x5E, "6 Axis Saturation: Magenta" },

            // Input source codes
            { 0x60, "Input Source" },
            { 0x62, "Audio Speaker Volume" },
            { 0x63, "Speaker Select" },
            { 0x64, "Audio: Microphone Volume" },
            { 0x66, "Ambient Light Sensor" },
            { 0x6B, "Backlight Level: White" },
            { 0x6C, "Video Black Level: Red" },
            { 0x6D, "Backlight Level: Red" },
            { 0x6E, "Video Black Level: Green" },
            { 0x6F, "Backlight Level: Green" },
            { 0x70, "Video Black Level: Blue" },
            { 0x71, "Backlight Level: Blue" },
            { 0x72, "Gamma" },
            { 0x73, "LUT Size" },
            { 0x74, "Single Point LUT Operation" },
            { 0x75, "Block LUT Operation" },
            { 0x76, "Remote Procedure Call" },
            { 0x78, "Display Identification Data Operation" },
            { 0x7A, "Adjust Focal Plane" },
            { 0x7C, "Adjust Zoom" },
            { 0x7E, "Trapezoid" },
            { 0x80, "Keystone" },
            { 0x82, "Horizontal Mirror (Flip)" },
            { 0x84, "Vertical Mirror (Flip)" },

            // Image adjustment codes (0x86-0x9F)
            { 0x86, "Display Scaling" },
            { 0x87, "Sharpness" },
            { 0x88, "Velocity Scan Modulation" },
            { 0x8A, "Color Saturation" },
            { 0x8B, "TV Channel Up/Down" },
            { 0x8C, "TV Sharpness" },
            { 0x8D, "Audio Mute/Screen Blank" },
            { 0x8E, "TV Contrast" },
            { 0x8F, "Audio Treble" },
            { 0x90, "Hue" },
            { 0x91, "Audio Bass" },
            { 0x92, "TV Black Level/Luminance" },
            { 0x93, "Audio Balance L/R" },
            { 0x94, "Audio Processor Mode" },
            { 0x95, "Window Position(TL_X)" },
            { 0x96, "Window Position(TL_Y)" },
            { 0x97, "Window Position(BR_X)" },
            { 0x98, "Window Position(BR_Y)" },
            { 0x99, "Window Background" },
            { 0x9A, "6 Axis Hue Control: Red" },
            { 0x9B, "6 Axis Hue Control: Yellow" },
            { 0x9C, "6 Axis Hue Control: Green" },
            { 0x9D, "6 Axis Hue Control: Cyan" },
            { 0x9E, "6 Axis Hue Control: Blue" },
            { 0x9F, "6 Axis Hue Control: Magenta" },

            // Window control codes
            { 0xA0, "Auto Setup On/Off" },
            { 0xA2, "Auto Color Setup On/Off" },
            { 0xA4, "Window Mask Control" },
            { 0xA5, "Window Select" },
            { 0xA6, "Window Size" },
            { 0xA7, "Window Transparency" },
            { 0xA8, "Window Control" },
            { 0xAA, "Screen Orientation" },
            { 0xAC, "Horizontal Frequency" },
            { 0xAE, "Vertical Frequency" },

            // Misc advanced codes
            { 0xB0, "Settings" },
            { 0xB2, "Flat Panel Sub-Pixel Layout" },
            { 0xB4, "Source Timing Mode" },
            { 0xB6, "Display Technology Type" },
            { 0xB7, "Monitor Status" },
            { 0xB8, "Packet Count" },
            { 0xB9, "Monitor X Origin" },
            { 0xBA, "Monitor Y Origin" },
            { 0xBB, "Header Error Count" },
            { 0xBC, "Body CRC Error Count" },
            { 0xBD, "Client ID" },
            { 0xBE, "Link Control" },

            // Display controller codes
            { 0xC0, "Display Usage Time" },
            { 0xC2, "Display Firmware Level" },
            { 0xC4, "Display Descriptor Length" },
            { 0xC5, "Transmit Display Descriptor" },
            { 0xC6, "Enable Display of 'Display Descriptor'" },
            { 0xC8, "Display Controller Type" },
            { 0xC9, "Display Firmware Level" },
            { 0xCA, "OSD" },
            { 0xCC, "OSD Language" },
            { 0xCD, "Status Indicators" },
            { 0xCE, "Auxiliary Display Size" },
            { 0xCF, "Auxiliary Display Data" },
            { 0xD0, "Output Select" },
            { 0xD2, "Asset Tag" },
            { 0xD4, "Stereo Video Mode" },
            { 0xD6, "Power Mode" },
            { 0xD7, "Auxiliary Power Output" },
            { 0xD8, "Scan Mode" },
            { 0xD9, "Image Mode" },
            { 0xDA, "On Screen Display" },
            { 0xDB, "Backlight Level: White" },
            { 0xDC, "Display Application" },
            { 0xDD, "Application Enable Key" },
            { 0xDE, "Scratch Pad" },
            { 0xDF, "VCP Version" },

            // Manufacturer specific codes (0xE0-0xFF)
            // Per MCCS 2.2a: "The 32 control codes E0h through FFh have been
            // allocated to allow manufacturers to issue their own specific controls."
            { 0xE0, "Manufacturer Specific" },
            { 0xE1, "Manufacturer Specific" },
            { 0xE2, "Manufacturer Specific" },
            { 0xE3, "Manufacturer Specific" },
            { 0xE4, "Manufacturer Specific" },
            { 0xE5, "Manufacturer Specific" },
            { 0xE6, "Manufacturer Specific" },
            { 0xE7, "Manufacturer Specific" },
            { 0xE8, "Manufacturer Specific" },
            { 0xE9, "Manufacturer Specific" },
            { 0xEA, "Manufacturer Specific" },
            { 0xEB, "Manufacturer Specific" },
            { 0xEC, "Manufacturer Specific" },
            { 0xED, "Manufacturer Specific" },
            { 0xEE, "Manufacturer Specific" },
            { 0xEF, "Manufacturer Specific" },
            { 0xF0, "Manufacturer Specific" },
            { 0xF1, "Manufacturer Specific" },
            { 0xF2, "Manufacturer Specific" },
            { 0xF3, "Manufacturer Specific" },
            { 0xF4, "Manufacturer Specific" },
            { 0xF5, "Manufacturer Specific" },
            { 0xF6, "Manufacturer Specific" },
            { 0xF7, "Manufacturer Specific" },
            { 0xF8, "Manufacturer Specific" },
            { 0xF9, "Manufacturer Specific" },
            { 0xFA, "Manufacturer Specific" },
            { 0xFB, "Manufacturer Specific" },
            { 0xFC, "Manufacturer Specific" },
            { 0xFD, "Manufacturer Specific" },
            { 0xFE, "Manufacturer Specific" },
            { 0xFF, "Manufacturer Specific" },
        };

        /// <summary>
        /// Get the friendly name for a VCP code
        /// </summary>
        /// <param name="code">VCP code (e.g., 0x10)</param>
        /// <returns>Friendly name, or hex representation if unknown</returns>
        public static string GetCodeName(byte code)
        {
            return CodeNames.TryGetValue(code, out var name) ? name : $"Unknown (0x{code:X2})";
        }

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
                [0x07] = "8200K",
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
        /// Get all known values for a VCP code
        /// </summary>
        /// <param name="vcpCode">VCP code (e.g., 0x14)</param>
        /// <returns>Dictionary of value to name mappings, or null if no mappings exist</returns>
        public static IReadOnlyDictionary<int, string>? GetValueMappings(byte vcpCode)
        {
            return ValueNames.TryGetValue(vcpCode, out var values) ? values : null;
        }

        /// <summary>
        /// Get human-readable name for a VCP value
        /// </summary>
        /// <param name="vcpCode">VCP code (e.g., 0x14)</param>
        /// <param name="value">Value to translate</param>
        /// <returns>Name string like "sRGB" or null if unknown</returns>
        public static string? GetValueName(byte vcpCode, int value)
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
        public static string GetFormattedValueName(byte vcpCode, int value)
        {
            var name = GetValueName(vcpCode, value);
            if (name != null)
            {
                return $"{name} (0x{value:X2})";
            }

            return $"0x{value:X2}";
        }

        /// <summary>
        /// Get human-readable name for a VCP value with custom mapping support.
        /// Custom mappings take priority over built-in mappings.
        /// Monitor ID is required to properly filter monitor-specific mappings.
        /// </summary>
        /// <param name="vcpCode">VCP code (e.g., 0x14)</param>
        /// <param name="value">Value to translate</param>
        /// <param name="customMappings">Optional custom mappings that take priority</param>
        /// <param name="monitorId">Monitor ID to filter mappings</param>
        /// <returns>Name string like "sRGB" or null if unknown</returns>
        public static string? GetValueName(byte vcpCode, int value, IEnumerable<CustomVcpValueMapping>? customMappings, string monitorId)
        {
            // 1. Priority: Check custom mappings first
            if (customMappings != null)
            {
                // Find a matching custom mapping:
                // - ApplyToAll = true (global), OR
                // - ApplyToAll = false AND TargetMonitorId matches the given monitorId
                var custom = customMappings.FirstOrDefault(m =>
                    m.VcpCode == vcpCode &&
                    m.Value == value &&
                    (m.ApplyToAll || (!m.ApplyToAll && m.TargetMonitorId == monitorId)));

                if (custom != null && !string.IsNullOrEmpty(custom.CustomName))
                {
                    return custom.CustomName;
                }
            }

            // 2. Fallback to built-in mappings
            return GetValueName(vcpCode, value);
        }

        /// <summary>
        /// Get formatted display name for a VCP value with custom mapping support.
        /// Custom mappings take priority over built-in mappings.
        /// Monitor ID is required to properly filter monitor-specific mappings.
        /// </summary>
        /// <param name="vcpCode">VCP code (e.g., 0x14)</param>
        /// <param name="value">Value to translate</param>
        /// <param name="customMappings">Optional custom mappings that take priority</param>
        /// <param name="monitorId">Monitor ID to filter mappings</param>
        /// <returns>Formatted string like "sRGB (0x01)" or "0x01" if unknown</returns>
        public static string GetFormattedValueName(byte vcpCode, int value, IEnumerable<CustomVcpValueMapping>? customMappings, string monitorId)
        {
            var name = GetValueName(vcpCode, value, customMappings, monitorId);
            if (name != null)
            {
                return $"{name} (0x{value:X2})";
            }

            return $"0x{value:X2}";
        }
    }
}
