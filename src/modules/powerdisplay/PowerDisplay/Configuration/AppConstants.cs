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
            public const int MinWindowHeight = 100;
            public const int MaxWindowHeight = 650;
            public const int WindowRightMargin = 12;

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
