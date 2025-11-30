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
            // Window dimensions
            public const int WindowWidth = 362;
            public const int MaxWindowHeight = 650;
            public const int WindowRightMargin = 12;

            /// <summary>
            /// Debounce delay for slider controls in milliseconds
            /// </summary>
            public const int SliderDebounceDelayMs = 300;

            /// <summary>
            /// Icon glyph for internal/laptop displays (WMI)
            /// </summary>
            public const string InternalMonitorGlyph = "\uE7F8";

            /// <summary>
            /// Icon glyph for external monitors (DDC/CI)
            /// </summary>
            public const string ExternalMonitorGlyph = "\uE7F4";
        }

        /// <summary>
        /// DDC/CI protocol constants
        /// </summary>
        public static class Ddc
        {
            /// <summary>
            /// Retry delay between DDC/CI operations in milliseconds
            /// </summary>
            public const int RetryDelayMs = 100;

            /// <summary>
            /// Maximum number of retries for DDC/CI operations
            /// </summary>
            public const int MaxRetries = 3;

            /// <summary>
            /// Timeout for getting monitor capabilities in milliseconds
            /// </summary>
            public const int CapabilitiesTimeoutMs = 5000;

            // VCP Codes
            /// <summary>VCP code for Brightness (0x10)</summary>
            public const byte VcpBrightness = 0x10;

            /// <summary>VCP code for Contrast (0x12)</summary>
            public const byte VcpContrast = 0x12;

            /// <summary>VCP code for Select Color Preset / Color Temperature (0x14)</summary>
            public const byte VcpColorTemperature = 0x14;

            /// <summary>VCP code for Input Source (0x60)</summary>
            public const byte VcpInputSource = 0x60;
        }

        /// <summary>
        /// Process synchronization constants
        /// </summary>
        public static class Process
        {
            /// <summary>
            /// Timeout for waiting for process ready signal in milliseconds
            /// </summary>
            public const int StartupTimeoutMs = 5000;

            /// <summary>
            /// Polling interval when waiting for process ready in milliseconds
            /// </summary>
            public const int ReadyPollIntervalMs = 100;

            /// <summary>
            /// Fallback delay when event-based synchronization fails in milliseconds
            /// </summary>
            public const int FallbackDelayMs = 500;
        }
    }
}
