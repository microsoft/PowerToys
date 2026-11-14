// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Default values shared between the Awake module and the Settings UI.
    /// </summary>
    public static class AwakeDefaults
    {
        /// <summary>
        /// Gets the durations (in seconds) shown in the Awake tray menu when no custom tray times are configured.
        /// </summary>
        public static IReadOnlyList<uint> TrayTimeSeconds { get; } =
        [
            30 * 60,
            1 * 60 * 60,
            2 * 60 * 60,
            4 * 60 * 60,
            8 * 60 * 60,
            12 * 60 * 60,
        ];
    }
}
