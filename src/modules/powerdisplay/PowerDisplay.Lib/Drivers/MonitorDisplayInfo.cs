// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// Monitor display information structure produced by QueryDisplayConfig.
    /// Used by MonitorManager Phase 0 classification and by both controllers
    /// during discovery. Immutable value type — populated once by
    /// <see cref="DisplayConfigInventory"/> and read-only thereafter.
    /// </summary>
    public readonly record struct MonitorDisplayInfo
    {
        /// <summary>
        /// Gets the monitor device path (e.g., "\\?\DISPLAY#DELA1D8#...").
        /// This is unique per target and used as the primary key.
        /// </summary>
        public string DevicePath { get; init; }

        /// <summary>
        /// Gets the GDI device name (e.g., "\\.\DISPLAY1").
        /// This is used to match with GetMonitorInfo results from HMONITOR.
        /// In mirror mode, multiple targets may share the same GDI name.
        /// </summary>
        public string GdiDeviceName { get; init; }

        /// <summary>
        /// Gets the friendly display name from EDID.
        /// </summary>
        public string FriendlyName { get; init; }

        public LUID AdapterId { get; init; }

        public uint TargetId { get; init; }

        /// <summary>
        /// Gets the monitor number based on QueryDisplayConfig path index.
        /// This matches the number shown in Windows Display Settings "Identify" feature.
        /// 1-based index (paths[0] = 1, paths[1] = 2, etc.)
        /// </summary>
        public int MonitorNumber { get; init; }

        /// <summary>
        /// Gets the raw DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY value reported
        /// by QueryDisplayConfig. Preserved for diagnostic logging.
        /// </summary>
        public uint OutputTechnology { get; init; }

        /// <summary>
        /// Gets a value indicating whether this display is classified as internal (built-in).
        /// Computed from OutputTechnology by DisplayClassifier.IsInternal during Phase 0.
        /// </summary>
        public bool IsInternal { get; init; }
    }
}
