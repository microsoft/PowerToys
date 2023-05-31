// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Peek.UI.Native;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Peek.UI.Extensions
{
    public static class HWNDExtensions
    {
        internal static HWND GetActiveTab(this HWND windowHandle)
        {
            var activeTab = windowHandle.FindChildWindow("ShellTabWindowClass");
            if (activeTab == HWND.Null)
            {
                activeTab = windowHandle.FindChildWindow("TabWindowClass");
            }

            return activeTab;
        }

        // Keep logic synced with the similar function in the C++ module interface.
        // TODO: Refactor into same C++ class consumed by both.
        internal static bool IsDesktopWindow(this HWND windowHandle)
        {
            StringBuilder strClassName = new StringBuilder(256);
            var result = NativeMethods.GetClassName(windowHandle, strClassName, 256);
            if (result == 0)
            {
                return false;
            }

            var className = strClassName.ToString();

            if (className != "Progman" && className != "WorkerW")
            {
                return false;
            }

            return windowHandle.FindChildWindow("SHELLDLL_DefView") != HWND.Null;
        }

        internal static HWND FindChildWindow(this HWND windowHandle, string className)
        {
            return PInvoke.FindWindowEx(windowHandle, HWND.Null, className, null);
        }

        internal static Size GetMonitorSize(this HWND hwnd)
        {
            var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            MONITORINFO info = default(MONITORINFO);
            info.cbSize = 40;
            PInvoke.GetMonitorInfo(monitor, ref info);
            return new Size(info.rcMonitor.Size.Width, info.rcMonitor.Size.Height);
        }

        internal static double GetMonitorScale(this HWND hwnd)
        {
            var dpi = PInvoke.GetDpiForWindow(hwnd);
            var scalingFactor = dpi / 96d;
            return scalingFactor;
        }
    }
}
