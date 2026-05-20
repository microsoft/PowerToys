// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Classifies displays as internal (built-in) or external based on the
    /// DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY enum returned by QueryDisplayConfig.
    /// Pure function helper, no side effects.
    /// </summary>
    /// <remarks>
    /// Reference for the full set of OutputTechnology values:
    /// https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ne-wingdi-displayconfig_video_output_technology
    ///
    /// Common values seen in the wild:
    ///   0  HD15 (VGA)        5  HDMI                 10 DISPLAYPORT_EXTERNAL
    ///   1  SVIDEO            6  LVDS                 11 DISPLAYPORT_EMBEDDED  (internal)
    ///   2  COMPOSITE_VIDEO   8  D_JPN                12 UDI_EXTERNAL
    ///   3  COMPONENT_VIDEO   9  SDI                  13 UDI_EMBEDDED          (internal)
    ///   4  DVI                                       15 MIRACAST
    ///                                                17 INDIRECT_VIRTUAL
    ///                              0x80000000 INTERNAL high-bit flag, may be combined with a subtype
    ///                              0xFFFFFFFF OTHER (signed -1)
    /// </remarks>
    public static class DisplayClassifier
    {
        // High-bit flag indicating an internal display (DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL).
        private const uint InternalFlag = 0x80000000u;

        // Documented "embedded" subtypes that mean internal connection.
        private const uint DisplayPortEmbedded = 11u;
        private const uint UdiEmbedded = 13u;

        /// <summary>
        /// Returns true if the given OutputTechnology value indicates an internal display.
        /// Conservative rule: a value is internal only when it is either
        /// (a) the bare INTERNAL high-bit flag (0x80000000) with no subtype,
        /// (b) the INTERNAL flag combined with a documented embedded subtype
        ///     (DISPLAYPORT_EMBEDDED 11 or UDI_EMBEDDED 13), or
        /// (c) one of those embedded subtypes on its own.
        /// Any other value — including the INTERNAL flag combined with an
        /// undocumented subtype — is treated as external. Misclassifying an
        /// external display as internal would silently drop it from DDC/CI
        /// discovery (WMI has no fallback), so we err on the side of external.
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
    }
}
