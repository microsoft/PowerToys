// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerDisplay.Core.Models
{
    /// <summary>
    /// Monitor type enumeration
    /// </summary>
    public enum MonitorType
    {
        /// <summary>
        /// Unknown type
        /// </summary>
        Unknown,

        /// <summary>
        /// Internal display (laptop screen, controlled via WMI)
        /// </summary>
        Internal,

        /// <summary>
        /// External display (controlled via DDC/CI)
        /// </summary>
        External,

        /// <summary>
        /// HDR display (controlled via Display Config API)
        /// </summary>
        HDR,
    }
}
