// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;
using Windows.Graphics;

namespace ShortcutGuide.Helpers
{
    internal static class DisplayHelper
    {
        private static readonly RectInt32 DefaultDisplayArea = new(0, 0, 1920, 1080);

        /// <summary>
        /// Gets the layout for the monitor containing the cursor, or the default layout
        /// if the monitor cannot be determined.
        /// </summary>
        /// <returns>The monitor layout and taskbar state used to position the Shortcut
        /// Guide overlay.</returns>
        public static ScreenLayout GetScreenLayoutForCursor()
        {
            if (!NativeMethods.GetCursorPos(out NativeMethods.POINT cursor))
            {
                return GetDefaultScreenLayout();
            }

            IntPtr monitor = NativeMethods.MonitorFromPoint(
                cursor, (int)NativeMethods.MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);

            return monitor == IntPtr.Zero
                ? GetDefaultScreenLayout()
                : GetScreenLayoutForMonitor(monitor);
        }

        /// <summary>
        /// Gets the layout and taskbar state for the specified monitor.
        /// </summary>
        /// <param name="monitor">The handle of the monitor to query.</param>
        /// <returns>The monitor layout and taskbar state, or the default layout if
        /// monitor information cannot be retrieved.</returns>
        public static ScreenLayout GetScreenLayoutForMonitor(IntPtr monitor)
        {
            var monitorInfo = new NativeMethods.MONITORINFO
            {
                CbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>(),
            };
            if (!NativeMethods.GetMonitorInfoW(monitor, ref monitorInfo))
            {
                return GetDefaultScreenLayout();
            }

            // Core monitor properties.
            var monitorRect = monitorInfo.RcMonitor;
            var monitorBounds = ToRectInt32(monitorRect);
            var workBounds = ToRectInt32(monitorInfo.RcWork);
            float dpiScale = DpiHelper.GetDPIScaleForMonitor(monitor);

            // Global taskbar state.
            var edge = TasklistPositions.GetEdge();
            bool isAutoHide = IsTaskbarAutoHideEnabled();

            // Locate taskbar for this monitor.
            bool isPrimary = (monitorInfo.DwFlags & 1) != 0;
            IntPtr taskbar = FindTaskbarForMonitor(monitor, monitorRect, isPrimary);

            bool isTaskbarVisible;
            RectInt32 usableArea;

            // Calculate final bounds.
            if (taskbar != IntPtr.Zero && TryGetTaskbarBounds(taskbar, out var taskbarRect))
            {
                int overlapWidth =
                    Math.Max(0, Math.Min(taskbarRect.Right, monitorRect.Right) - Math.Max(taskbarRect.Left, monitorRect.Left));
                int overlapHeight =
                    Math.Max(0, Math.Min(taskbarRect.Bottom, monitorRect.Bottom) - Math.Max(taskbarRect.Top, monitorRect.Top));

                if (!isAutoHide)
                {
                    isTaskbarVisible = true;
                    usableArea = workBounds;
                }
                else
                {
                    var layout = TaskbarLayoutPolicy.CalculateAutoHideLayout(
                        monitorBounds, edge, dpiScale, overlapWidth, overlapHeight);
                    isTaskbarVisible = layout.IsTaskbarVisible;
                    usableArea = layout.UsableArea;
                }
            }
            else
            {
                Logger.LogWarning(
                    "Could not retrieve taskbar window bounds. Falling back to OS work area.");
                isTaskbarVisible = !isAutoHide;
                usableArea = workBounds;
            }

            return new ScreenLayout(
                monitor, monitorBounds, usableArea, dpiScale, edge, isAutoHide, isTaskbarVisible);
        }

        private static bool IsTaskbarAutoHideEnabled()
        {
            var data = new NativeMethods.APPBARDATA
            {
                CbSize = (uint)Marshal.SizeOf<NativeMethods.APPBARDATA>(),
            };
            IntPtr state = NativeMethods.SHAppBarMessage(NativeMethods.ABM_GETSTATE, ref data);
            return ((uint)state & NativeMethods.ABS_AUTOHIDE) != 0;
        }

        private static IntPtr FindTaskbarForMonitor(
            IntPtr monitor, NativeMethods.RECT monitorRect, bool isPrimary)
        {
            if (isPrimary)
            {
                IntPtr primaryTaskbar = NativeMethods.FindWindowW("Shell_TrayWnd", null);
                if (primaryTaskbar != IntPtr.Zero)
                {
                    return primaryTaskbar;
                }
            }

            IntPtr secondaryTaskbar = IntPtr.Zero;
            while ((secondaryTaskbar = NativeMethods.FindWindowExW(
                IntPtr.Zero, secondaryTaskbar, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                IntPtr taskbarMonitor = NativeMethods.MonitorFromWindow(
                    secondaryTaskbar,
                    (int)NativeMethods.MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);

                if (taskbarMonitor == monitor ||
                    (NativeMethods.GetWindowRect(secondaryTaskbar, out NativeMethods.RECT secondaryRect) && HasOverlap(secondaryRect, monitorRect)))
                {
                    return secondaryTaskbar;
                }
            }

            if (!isPrimary)
            {
                IntPtr primaryTaskbar = NativeMethods.FindWindowW("Shell_TrayWnd", null);

                if (primaryTaskbar != IntPtr.Zero &&
                    NativeMethods.MonitorFromWindow(primaryTaskbar, (int)NativeMethods.MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST) == monitor)
                {
                    return primaryTaskbar;
                }
            }

            return IntPtr.Zero;
        }

        private static bool TryGetTaskbarBounds(IntPtr taskbar, out NativeMethods.RECT bounds)
        {
            // DWM reports the visible frame, while GetWindowRect can include the
            // invisible resize border. Use the visible frame for overlap detection so
            // that border/resize pixels don't affect auto-hide classification.
            if (NativeMethods.DwmGetWindowAttribute(
                taskbar,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out bounds,
                Marshal.SizeOf<NativeMethods.RECT>()) == 0 && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
            {
                return true;
            }

            // Fallback.
            if (NativeMethods.GetWindowRect(taskbar, out bounds) && bounds.Right > bounds.Left && bounds.Bottom > bounds.Top)
            {
                return true;
            }

            bounds = default;
            return false;
        }

        private static bool HasOverlap(NativeMethods.RECT first, NativeMethods.RECT second) =>
            first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;

        private static RectInt32 ToRectInt32(NativeMethods.RECT rect) =>
            new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);

        private static ScreenLayout GetDefaultScreenLayout() =>
            new(IntPtr.Zero, DefaultDisplayArea, DefaultDisplayArea, 1.0f, TaskbarEdge.Bottom, false, true);
    }
}
