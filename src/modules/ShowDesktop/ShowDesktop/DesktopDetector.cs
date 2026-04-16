// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;

namespace ShowDesktop
{
    /// <summary>
    /// Determines whether a screen point corresponds to the desktop wallpaper
    /// (not desktop icons, taskbar, or other windows).
    /// </summary>
    internal static class DesktopDetector
    {
        private const string ProgmanClassName = "Progman";
        private const string WorkerWClassName = "WorkerW";
        private const string ShellDllDefViewClassName = "SHELLDLL_DefView";
        private const string SysListView32ClassName = "SysListView32";
        private const string TaskbarClassName = "Shell_TrayWnd";
        private const string SecondaryTaskbarClassName = "Shell_SecondaryTrayWnd";

        /// <summary>
        /// Returns true if the click at (x, y) lands on the desktop wallpaper surface.
        /// </summary>
        public static bool IsDesktopClick(int x, int y)
        {
            try
            {
                var point = new NativeMethods.POINT(x, y);
                IntPtr hwnd = NativeMethods.WindowFromPoint(point);

                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                return IsDesktopWindow(hwnd);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error detecting desktop click: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns true if the click at (x, y) lands on empty taskbar space.
        /// </summary>
        public static bool IsTaskbarClick(int x, int y)
        {
            try
            {
                var point = new NativeMethods.POINT(x, y);
                IntPtr hwnd = NativeMethods.WindowFromPoint(point);

                if (hwnd == IntPtr.Zero)
                {
                    return false;
                }

                string className = GetClassName(hwnd);

                // Direct hit on the taskbar window or secondary taskbar
                if (className == TaskbarClassName || className == SecondaryTaskbarClassName)
                {
                    return true;
                }

                // Walk parents to check if any ancestor is the taskbar
                IntPtr parent = hwnd;
                while (parent != IntPtr.Zero)
                {
                    string parentClass = GetClassName(parent);
                    if (parentClass == TaskbarClassName || parentClass == SecondaryTaskbarClassName)
                    {
                        return true;
                    }

                    IntPtr nextParent = NativeMethods.GetParent(parent);
                    if (nextParent == parent)
                    {
                        break;
                    }

                    parent = nextParent;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error detecting taskbar click: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if the given window handle is part of the desktop surface.
        /// The desktop can be either Progman or a WorkerW that hosts SHELLDLL_DefView.
        /// </summary>
        public static bool IsDesktopWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
            {
                return false;
            }

            string className = GetClassName(hwnd);

            // Direct Progman hit (the base desktop window)
            if (className == ProgmanClassName)
            {
                return true;
            }

            // WorkerW hosts the desktop when wallpaper slideshow or certain effects are active
            if (className == WorkerWClassName)
            {
                // Check if this WorkerW contains SHELLDLL_DefView (the desktop icon container)
                IntPtr shellView = NativeMethods.FindWindowEx(hwnd, IntPtr.Zero, ShellDllDefViewClassName, null);
                if (shellView != IntPtr.Zero)
                {
                    return true;
                }

                // Also check if it's a sibling WorkerW to the one hosting SHELLDLL_DefView
                // (Windows creates multiple WorkerW windows for the desktop)
                return HasDesktopSiblingWorkerW(hwnd);
            }

            // SHELLDLL_DefView is the icon container on the desktop
            if (className == ShellDllDefViewClassName)
            {
                return true;
            }

            // SysListView32 hosts the actual desktop icons inside SHELLDLL_DefView.
            // We consider this a desktop click even though it's on the icon area,
            // because the user clicked the desktop surface behind the icons.
            if (className == SysListView32ClassName)
            {
                IntPtr parent = NativeMethods.GetParent(hwnd);
                if (parent != IntPtr.Zero && GetClassName(parent) == ShellDllDefViewClassName)
                {
                    return true;
                }
            }

            // Walk up the parent chain
            IntPtr current = NativeMethods.GetParent(hwnd);
            while (current != IntPtr.Zero)
            {
                string parentClass = GetClassName(current);
                if (parentClass == ProgmanClassName || parentClass == WorkerWClassName)
                {
                    return true;
                }

                IntPtr next = NativeMethods.GetParent(current);
                if (next == current)
                {
                    break;
                }

                current = next;
            }

            return false;
        }

        /// <summary>
        /// When wallpaper slideshow is active, Windows creates the desktop using WorkerW windows.
        /// One WorkerW has SHELLDLL_DefView, and a sibling WorkerW renders behind it.
        /// This checks if any sibling WorkerW of <paramref name="hwnd"/> hosts the desktop.
        /// </summary>
        private static bool HasDesktopSiblingWorkerW(IntPtr hwnd)
        {
            // Find Progman first
            IntPtr progman = NativeMethods.FindWindow(ProgmanClassName, null);
            if (progman == IntPtr.Zero)
            {
                return false;
            }

            // Enumerate top-level WorkerW windows looking for one that has SHELLDLL_DefView
            bool foundDesktopWorkerW = false;
            NativeMethods.EnumWindows(
                (enumHwnd, _) =>
                {
                    if (GetClassName(enumHwnd) == WorkerWClassName)
                    {
                        IntPtr shellView = NativeMethods.FindWindowEx(enumHwnd, IntPtr.Zero, ShellDllDefViewClassName, null);
                        if (shellView != IntPtr.Zero)
                        {
                            // Found the WorkerW that hosts the desktop icons.
                            // The clicked WorkerW is a sibling used for the wallpaper surface.
                            foundDesktopWorkerW = true;
                            return false; // stop enumeration
                        }
                    }

                    return true;
                },
                IntPtr.Zero);

            return foundDesktopWorkerW;
        }

        private static string GetClassName(IntPtr hwnd)
        {
            var buffer = new char[256];
            int length = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
            return length > 0 ? new string(buffer, 0, length) : string.Empty;
        }
    }
}
