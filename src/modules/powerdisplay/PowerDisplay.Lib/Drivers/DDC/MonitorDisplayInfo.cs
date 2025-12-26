// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;

namespace PowerDisplay.Common.Drivers.DDC
{
    /// <summary>
    /// Monitor display information structure
    /// </summary>
    public struct MonitorDisplayInfo
    {
        /// <summary>
        /// Gets or sets the monitor device path (e.g., "\\?\DISPLAY#DELA1D8#...").
        /// This is unique per target and used as the primary key.
        /// </summary>
        public string DevicePath { get; set; }

        /// <summary>
        /// Gets or sets the GDI device name (e.g., "\\.\DISPLAY1").
        /// This is used to match with GetMonitorInfo results from HMONITOR.
        /// In mirror mode, multiple targets may share the same GDI name.
        /// </summary>
        public string GdiDeviceName { get; set; }

        /// <summary>
        /// Gets or sets the friendly display name from EDID.
        /// </summary>
        public string FriendlyName { get; set; }

        /// <summary>
        /// Gets or sets the hardware ID derived from EDID manufacturer and product code.
        /// </summary>
        public string HardwareId { get; set; }

        public LUID AdapterId { get; set; }

        public uint TargetId { get; set; }

        /// <summary>
        /// Gets or sets the monitor number based on QueryDisplayConfig path index.
        /// This matches the number shown in Windows Display Settings "Identify" feature.
        /// 1-based index (paths[0] = 1, paths[1] = 2, etc.)
        /// </summary>
        public int MonitorNumber { get; set; }
    }
}
