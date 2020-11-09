// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace FancyZonesEditor.Utils
{
    public class MonitorInfoUtils
    {
        /// <summary>
        /// Rectangle
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width
            {
                get
                {
                    return Right - Left;
                }
            }

            public int Height
            {
                get
                {
                    return Bottom - Top;
                }
            }
        }

        /// <summary>
        /// Monitor information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint Size;
            public RECT Monitor;
            public RECT Work;
            public uint Flags;
        }

        /// <summary>
        /// Monitor Enum Delegate
        /// </summary>
        /// <param name="hMonitor">A handle to the display monitor.</param>
        /// <param name="hdcMonitor">A handle to a device context.</param>
        /// <param name="lprcMonitor">A pointer to a RECT structure.</param>
        /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the enumeration function.</param>
        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        /// <summary>
        /// Enumerates through the display monitors.
        /// </summary>
        /// <param name="hdc">A handle to a display device context that defines the visible region of interest.</param>
        /// <param name="lprcClip">A pointer to a RECT structure that specifies a clipping rectangle.</param>
        /// <param name="lpfnEnum">A pointer to a MonitorEnumProc application-defined callback function.</param>
        /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the MonitorEnumProc function.</param>
        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        /// <summary>
        /// Gets the monitor information.
        /// </summary>
        /// <param name="hmon">A handle to the display monitor of interest.</param>
        /// <param name="mi">A pointer to a MONITORINFO instance created by this method.</param>
        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);

        /// <summary>
        /// MonitorInfo class contains necessary information about the monitor
        /// </summary>
        public class MonitorInfo
        {
            /// <summary>
            /// Gets a handle to the monitor.
            /// </summary>
            public IntPtr MonitorHandle { get; private set; }

            /// <summary>
            /// Gets a value indicating whether the monitor is primary.
            /// </summary>
            public bool Primary { get; private set; }

            /// <summary>
            /// Gets monitor size excluding taskbar.
            /// </summary>
            public Rect WorkArea { get; private set; }

            /// <summary>
            /// Gets monitor size.
            /// </summary>
            public Rect Bounds { get; private set; }

            public MonitorInfo(IntPtr monitorHandle, MONITORINFO monitorInfo)
            {
                MonitorHandle = monitorHandle;
                Primary = monitorInfo.Flags == 1;

                RECT monitorRect = monitorInfo.Monitor;
                WorkArea = new Rect(monitorRect.Left, monitorRect.Top, monitorRect.Width, monitorRect.Height);

                RECT boundsRect = monitorInfo.Work;
                Bounds = new Rect(boundsRect.Left, boundsRect.Top, boundsRect.Width, boundsRect.Height);
            }
        }

        /// <summary>
        /// Monitor Enum Delegate
        /// </summary>
        /// <param name="hMonitor">A handle to the display monitor.</param>
        /// <param name="hdcMonitor">A handle to a device context.</param>
        /// <param name="lprcMonitor">A pointer to a RECT structure.</param>
        /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the enumeration function.</param>
        public static bool MonitorEnum(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var mi = default(MONITORINFO);
            mi.Size = (uint)Marshal.SizeOf(mi);
            GetMonitorInfo(hMonitor, ref mi);

            // Add to monitor info
            _monitorInfos.Add(new MonitorInfo(hMonitor, mi));
            return true;
        }

        /// <summary>
        /// Gets the monitors.
        /// </summary>
        public static MonitorInfo[] GetMonitors()
        {
            _monitorInfos = new List<MonitorInfo>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);
            return _monitorInfos.ToArray();
        }

        private static List<MonitorInfo> _monitorInfos;
    }
}
