// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Common.Drivers;

namespace PowerDisplay.Common.Utils
{
    /// <summary>
    /// Unified helper class for parsing monitor feature support from VCP capabilities.
    /// This eliminates duplicate VCP parsing logic across PowerDisplay.exe and Settings.UI.
    /// </summary>
    public static class MonitorFeatureHelper
    {
        /// <summary>
        /// Result of parsing monitor feature support from VCP capabilities
        /// </summary>
        public readonly struct FeatureSupportResult
        {
            public bool SupportsBrightness { get; init; }

            public bool SupportsContrast { get; init; }

            public bool SupportsColorTemperature { get; init; }

            public bool SupportsVolume { get; init; }

            public bool SupportsInputSource { get; init; }

            public string CapabilitiesStatus { get; init; }

            public static FeatureSupportResult Unavailable => new()
            {
                SupportsBrightness = false,
                SupportsContrast = false,
                SupportsColorTemperature = false,
                SupportsVolume = false,
                SupportsInputSource = false,
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
                SupportsBrightness = vcpCodeInts.Contains(NativeConstants.VcpCodeBrightness),
                SupportsContrast = vcpCodeInts.Contains(NativeConstants.VcpCodeContrast),
                SupportsColorTemperature = vcpCodeInts.Contains(NativeConstants.VcpCodeSelectColorPreset),
                SupportsVolume = vcpCodeInts.Contains(NativeConstants.VcpCodeVolume),
                SupportsInputSource = vcpCodeInts.Contains(NativeConstants.VcpCodeInputSource),
                CapabilitiesStatus = "available",
            };
        }

        /// <summary>
        /// Parse VCP codes from string list to integer set
        /// Handles both hex formats: "0x10" and "10"
        /// </summary>
        private static HashSet<int> ParseVcpCodesToIntegers(IReadOnlyList<string>? vcpCodes)
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
    }
}
