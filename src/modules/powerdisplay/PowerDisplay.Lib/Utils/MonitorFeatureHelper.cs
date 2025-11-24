// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using ManagedCommon;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Unified helper class for parsing monitor feature support from VCP capabilities.
    /// This eliminates duplicate VCP parsing logic across PowerDisplay.exe and Settings.UI.
    /// </summary>
    public static class MonitorFeatureHelper
    {
        /// <summary>
        /// VCP code for Brightness (0x10) - Standard VESA MCCS brightness control
        /// </summary>
        public const int VcpCodeBrightness = 0x10;

        /// <summary>
        /// VCP code for Contrast (0x12) - Standard VESA MCCS contrast control
        /// </summary>
        public const int VcpCodeContrast = 0x12;

        /// <summary>
        /// VCP code for Select Color Preset (0x14) - Color temperature control
        /// </summary>
        public const int VcpCodeSelectColorPreset = 0x14;

        /// <summary>
        /// VCP code for Audio Speaker Volume (0x62)
        /// </summary>
        public const int VcpCodeVolume = 0x62;

        /// <summary>
        /// Result of parsing monitor feature support from VCP capabilities
        /// </summary>
        public readonly struct FeatureSupportResult
        {
            public bool SupportsBrightness { get; init; }

            public bool SupportsContrast { get; init; }

            public bool SupportsColorTemperature { get; init; }

            public bool SupportsVolume { get; init; }

            public string CapabilitiesStatus { get; init; }

            public static FeatureSupportResult Unavailable => new()
            {
                SupportsBrightness = false,
                SupportsContrast = false,
                SupportsColorTemperature = false,
                SupportsVolume = false,
                CapabilitiesStatus = "unavailable",
            };
        }

        /// <summary>
        /// Parse feature support from a list of VCP code strings.
        /// This is the single source of truth for determining monitor capabilities.
        /// </summary>
        /// <param name="vcpCodes">List of VCP codes as strings (e.g., "0x10", "10", "0x12")</param>
        /// <param name="capabilitiesRaw">Raw capabilities string, used to determine availability status</param>
        /// <returns>Feature support result</returns>
        public static FeatureSupportResult ParseFeatureSupport(IReadOnlyList<string>? vcpCodes, string? capabilitiesRaw)
        {
            // Check capabilities availability
            if (string.IsNullOrEmpty(capabilitiesRaw))
            {
                return FeatureSupportResult.Unavailable;
            }

            // Convert all VCP codes to integers for comparison
            var vcpCodeInts = ParseVcpCodesToIntegers(vcpCodes);

            // Determine feature support based on VCP codes
            return new FeatureSupportResult
            {
                SupportsBrightness = vcpCodeInts.Contains(VcpCodeBrightness),
                SupportsContrast = vcpCodeInts.Contains(VcpCodeContrast),
                SupportsColorTemperature = vcpCodeInts.Contains(VcpCodeSelectColorPreset),
                SupportsVolume = vcpCodeInts.Contains(VcpCodeVolume),
                CapabilitiesStatus = "available",
            };
        }

        /// <summary>
        /// Parse VCP codes from string list to integer set
        /// Handles both hex formats: "0x10" and "10"
        /// </summary>
        public static HashSet<int> ParseVcpCodesToIntegers(IReadOnlyList<string>? vcpCodes)
        {
            var result = new HashSet<int>();

            if (vcpCodes == null)
            {
                return result;
            }

            foreach (var code in vcpCodes)
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                // Remove "0x" prefix if present and parse as hex
                var cleanCode = code.Trim();
                if (cleanCode.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    cleanCode = cleanCode.Substring(2);
                }

                if (int.TryParse(cleanCode, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int codeInt))
                {
                    result.Add(codeInt);
                }
            }

            return result;
        }

        /// <summary>
        /// Check if a specific VCP code is supported
        /// </summary>
        public static bool SupportsVcpCode(IReadOnlyList<string>? vcpCodes, int vcpCode)
        {
            var parsed = ParseVcpCodesToIntegers(vcpCodes);
            return parsed.Contains(vcpCode);
        }

        /// <summary>
        /// Format VCP code for display (e.g., 0x10 -> "0x10")
        /// </summary>
        public static string FormatVcpCode(int vcpCode)
        {
            return $"0x{vcpCode:X2}";
        }
    }
}
