// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Classifies displays as internal (built-in) or external based on the
    /// DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY enum returned by QueryDisplayConfig.
    /// Pure function helper, no side effects.
    /// </summary>
    public static class DisplayClassifier
    {
        // High-bit flag indicating an internal display (DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL).
        private const uint InternalFlag = 0x80000000u;

        // Documented "embedded" subtypes that mean internal connection.
        private const uint DisplayPortEmbedded = 11u;
        private const uint UdiEmbedded = 13u;

        /// <summary>
        /// Returns true if the given OutputTechnology value indicates an internal display.
        /// Per Microsoft docs the INTERNAL flag is technically redundant when an embedded
        /// subtype is reported, but we check both defensively because some legacy drivers
        /// may set only the high-bit flag without an embedded subtype.
        /// LVDS (6) is intentionally NOT classified as internal — the official docs
        /// describe it only as a connector type, not as an internal-display marker.
        /// </summary>
        public static bool IsInternal(uint outputTechnology)
        {
            // Pure INTERNAL flag with no underlying value.
            if (outputTechnology == InternalFlag)
            {
                return true;
            }

            // INTERNAL combined with a known embedded subtype.
            if ((outputTechnology & InternalFlag) != 0)
            {
                var underlying = outputTechnology & ~InternalFlag;
                if (underlying == DisplayPortEmbedded || underlying == UdiEmbedded)
                {
                    return true;
                }

                // INTERNAL combined with unknown/undocumented subtype: treat as external.
                return false;
            }

            // Known embedded subtypes without the INTERNAL flag.
            return outputTechnology == DisplayPortEmbedded
                || outputTechnology == UdiEmbedded;
        }

        /// <summary>
        /// Returns a human-readable name for the OutputTechnology value (used for logging).
        /// If the INTERNAL high-bit is OR'd with a documented subtype, returns a composite
        /// name like "INTERNAL|DISPLAYPORT_EMBEDDED".
        /// Unknown values render as "Unknown(0xXXXXXXXX)".
        /// </summary>
        public static string GetOutputTechnologyName(uint outputTechnology)
        {
            // Pure INTERNAL with no underlying subtype.
            if (outputTechnology == InternalFlag)
            {
                return "INTERNAL";
            }

            // INTERNAL combined with a known embedded subtype.
            if ((outputTechnology & InternalFlag) != 0)
            {
                var underlying = outputTechnology & ~InternalFlag;
                if (underlying == DisplayPortEmbedded || underlying == UdiEmbedded)
                {
                    var underlyingName = GetSimpleName(underlying);
                    return $"INTERNAL|{underlyingName}";
                }

                // INTERNAL combined with unknown/undocumented subtype: render as-is.
            }

            return GetSimpleName(outputTechnology);
        }

        private static string GetSimpleName(uint value) => value switch
        {
            0xFFFFFFFFu => "OTHER",      // -1 cast to uint
            0u => "HD15",
            1u => "SVIDEO",
            2u => "COMPOSITE_VIDEO",
            3u => "COMPONENT_VIDEO",
            4u => "DVI",
            5u => "HDMI",
            6u => "LVDS",
            8u => "D_JPN",
            9u => "SDI",
            10u => "DISPLAYPORT_EXTERNAL",
            11u => "DISPLAYPORT_EMBEDDED",
            12u => "UDI_EXTERNAL",
            13u => "UDI_EMBEDDED",
            14u => "SDTVDONGLE",
            15u => "MIRACAST",
            16u => "INDIRECT_WIRED",
            17u => "INDIRECT_VIRTUAL",
            18u => "DISPLAYPORT_USB_TUNNEL",
            _ => $"Unknown(0x{value.ToString("X", CultureInfo.InvariantCulture)})",
        };
    }
}
