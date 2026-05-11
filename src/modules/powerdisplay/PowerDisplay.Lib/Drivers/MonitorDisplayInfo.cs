// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Monitor display information structure produced by QueryDisplayConfig.
    /// Used by MonitorManager Phase 0 classification and by both controllers
    /// during discovery.
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

        public LUID AdapterId { get; set; }

        public uint TargetId { get; set; }

        /// <summary>
        /// Gets or sets the monitor number based on QueryDisplayConfig path index.
        /// This matches the number shown in Windows Display Settings "Identify" feature.
        /// 1-based index (paths[0] = 1, paths[1] = 2, etc.)
        /// </summary>
        public int MonitorNumber { get; set; }

        /// <summary>
        /// Gets or sets the raw DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY value reported
        /// by QueryDisplayConfig. Preserved for diagnostic logging.
        /// </summary>
        public uint OutputTechnology { get; set; }

        /// <summary>
        /// Gets or sets whether this display is classified as internal (built-in).
        /// Computed from OutputTechnology by DisplayClassifier.IsInternal during Phase 0.
        /// </summary>
        public bool IsInternal { get; set; }
    }
}
