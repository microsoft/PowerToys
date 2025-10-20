// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace PowerDisplay.Core.Models
{
    /// <summary>
    /// Monitor control capabilities flags
    /// </summary>
    [Flags]
    public enum MonitorCapabilities
    {
        None = 0,

        /// <summary>
        /// Supports brightness control
        /// </summary>
        Brightness = 1 << 0,

        /// <summary>
        /// Supports contrast control
        /// </summary>
        Contrast = 1 << 1,

        /// <summary>
        /// Supports DDC/CI protocol
        /// </summary>
        DdcCi = 1 << 2,

        /// <summary>
        /// Supports WMI control
        /// </summary>
        Wmi = 1 << 3,

        /// <summary>
        /// Supports HDR
        /// </summary>
        Hdr = 1 << 4,

        /// <summary>
        /// Supports high-level monitor API
        /// </summary>
        HighLevel = 1 << 5,

        /// <summary>
        /// Supports volume control
        /// </summary>
        Volume = 1 << 6,
    }
}
