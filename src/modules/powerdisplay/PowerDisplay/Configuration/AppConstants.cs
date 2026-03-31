// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Configuration
{
    /// <summary>
    /// Application-wide constants and configuration values
    /// </summary>
    public static class AppConstants
    {
        /// <summary>
        /// UI layout and timing constants
        /// </summary>
        public static class UI
        {
            // Main flyout dimensions in device-independent pixels (DIP)
            public const int WindowWidthDip = 362;
            public const int WindowMinHeightDip = 100;
            public const int WindowMaxHeightDip = 650;
            public const int WindowRightMarginDip = 12;
            public const int WindowBottomMarginDip = WindowRightMarginDip;
            public const double WindowMaxWorkAreaHeightRatio = 0.75;

            // Adaptive flyout bounds in device-independent pixels (DIP)
            public const int FlyoutContextMenuMaxWidthDip = 320;
            public const int FlyoutContextMenuAdaptiveMaxWidthDip = 420;
            public const double FlyoutContextMenuMaxWorkAreaWidthRatio = 0.35;

            // Identify overlay bounds in device-independent pixels (DIP)
            public const int IdentifyWindowPreferredWidthDip = 300;
            public const int IdentifyWindowPreferredHeightDip = 280;
            public const int IdentifyWindowMinWidthDip = 160;
            public const int IdentifyWindowMinHeightDip = 160;
            public const double IdentifyWindowMaxWorkAreaRatio = 0.28;

            /// <summary>
            /// Icon glyph for internal/laptop displays (WMI)
            /// </summary>
            public const string InternalMonitorGlyph = "\uE7F8";

            /// <summary>
            /// Icon glyph for external monitors (DDC/CI)
            /// </summary>
            public const string ExternalMonitorGlyph = "\uE7F4";
        }
    }
}
