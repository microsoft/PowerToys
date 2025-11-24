// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Event arguments for theme change notifications from LightSwitch
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Whether the system is currently in light mode
        /// </summary>
        public bool IsLightMode { get; }

        /// <summary>
        /// Profile name to apply (null if no profile configured for current theme)
        /// </summary>
        public string? ProfileToApply { get; }

        public ThemeChangedEventArgs(bool isLightMode, string? profileToApply)
        {
            IsLightMode = isLightMode;
            ProfileToApply = profileToApply;
        }
    }
}
