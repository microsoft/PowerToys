using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FancyZonesEditor_DPI_test
{
    class MonitorsInfo
    {
        /// <summary>
        /// Rectangle
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public int width
            {
                get
                {
                    return right - left;
                }
            }

            public int height
            {
                get
                {
                    return bottom - top;
                }
            }
        }


        /// <summary>
        /// Monitor information.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public uint size;
            public RECT monitor;
            public RECT work;
            public uint flags;
        }

        /// <summary>
        /// Monitor Enum Delegate
        /// </summary>
        /// <param name="hMonitor">A handle to the display monitor.</param>
        /// <param name="hdcMonitor">A handle to a device context.</param>
        /// <param name="lprcMonitor">A pointer to a RECT structure.</param>
        /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the enumeration function.</param>
        /// <returns></returns>
        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor,
            ref RECT lprcMonitor, IntPtr dwData);

        /// <summary>
        /// Enumerates through the display monitors.
        /// </summary>
        /// <param name="hdc">A handle to a display device context that defines the visible region of interest.</param>
        /// <param name="lprcClip">A pointer to a RECT structure that specifies a clipping rectangle.</param>
        /// <param name="lpfnEnum">A pointer to a MonitorEnumProc application-defined callback function.</param>
        /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the MonitorEnumProc function.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        /// <summary>
        /// Gets the monitor information.
        /// </summary>
        /// <param name="hmon">A handle to the display monitor of interest.</param>
        /// <param name="mi">A pointer to a MONITORINFO instance created by this method.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);

        /// <summary>
        /// Monitor information with handle interface.
        /// </summary>
        public interface IMonitorInfoWithHandle
        {
            IntPtr MonitorHandle { get; }
            MONITORINFO MonitorInfo { get; }
        }

        /// <summary>
        /// Monitor information with handle.
        /// </summary>
        public class MonitorInfoWithHandle : IMonitorInfoWithHandle
        {
            /// <summary>
            /// Gets the monitor handle.
            /// </summary>
            /// <value>
            /// The monitor handle.
            /// </value>
            public IntPtr MonitorHandle { get; private set; }

            /// <summary>
            /// Gets the monitor information.
            /// </summary>
            /// <value>
            /// The monitor information.
            /// </value>
            public MONITORINFO MonitorInfo { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="MonitorInfoWithHandle"/> class.
            /// </summary>
            /// <param name="monitorHandle">The monitor handle.</param>
            /// <param name="monitorInfo">The monitor information.</param>
            public MonitorInfoWithHandle(IntPtr monitorHandle, MONITORINFO monitorInfo)
            {
                MonitorHandle = monitorHandle;
                MonitorInfo = monitorInfo;
            }
        }

        /// <summary>
        /// Monitor Enum Delegate
        /// </summary>
        /// <param name="hMonitor">A handle to the display monitor.</param>
        /// <param name="hdcMonitor">A handle to a device context.</param>
        /// <param name="lprcMonitor">A pointer to a RECT structure.</param>
        /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the enumeration function.</param>
        /// <returns></returns>
        public static bool MonitorEnum(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            var mi = new MONITORINFO();
            mi.size = (uint)Marshal.SizeOf(mi);
            GetMonitorInfo(hMonitor, ref mi);

            // Add to monitor info
            _monitorInfos.Add(new MonitorInfoWithHandle(hMonitor, mi));
            return true;
        }

        /// <summary>
        /// Gets the monitors.
        /// </summary>
        /// <returns></returns>
        public static MonitorInfoWithHandle[] GetMonitors()
        {
            _monitorInfos = new List<MonitorInfoWithHandle>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnum, IntPtr.Zero);
            return _monitorInfos.ToArray();
        }

        private static List<MonitorInfoWithHandle> _monitorInfos;
    }
}
