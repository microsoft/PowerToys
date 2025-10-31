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
        /// State management configuration
        /// </summary>
        public static class State
        {
            /// <summary>
            /// Interval in milliseconds to check for pending state changes to save
            /// </summary>
            public const int SaveIntervalMs = 2000;

            /// <summary>
            /// Name of the state file for monitor parameters
            /// </summary>
            public const string StateFileName = "monitor_state.json";
        }

        /// <summary>
        /// Monitor parameter defaults and ranges
        /// </summary>
        public static class MonitorDefaults
        {
            // Brightness
            public const int MinBrightness = 0;
            public const int MaxBrightness = 100;
            public const int DefaultBrightness = 50;

            // Color Temperature (Kelvin)
            public const int MinColorTemp = 2000;  // Warm
            public const int MaxColorTemp = 10000; // Cool
            public const int DefaultColorTemp = 6500; // Neutral

            // Contrast
            public const int MinContrast = 0;
            public const int MaxContrast = 100;
            public const int DefaultContrast = 50;

            // Volume
            public const int MinVolume = 0;
            public const int MaxVolume = 100;
            public const int DefaultVolume = 50;
        }

        /// <summary>
        /// UI layout and timing constants
        /// </summary>
        public static class UI
        {
            // Window dimensions
            public const int WindowWidth = 640;
            public const int MaxWindowHeight = 650;
            public const int WindowRightMargin = 10;

            // Animation and layout update delays (milliseconds)
            public const int AnimationDelayMs = 100;
            public const int LayoutUpdateDelayMs = 50;
            public const int MonitorDiscoveryDelayMs = 200;
        }

        /// <summary>
        /// Application lifecycle timing constants
        /// </summary>
        public static class Lifetime
        {
            /// <summary>
            /// Normal shutdown timeout in milliseconds
            /// </summary>
            public const int NormalShutdownTimeoutMs = 1000;

            /// <summary>
            /// Emergency shutdown timeout in milliseconds
            /// </summary>
            public const int EmergencyShutdownTimeoutMs = 500;
        }
    }
}
