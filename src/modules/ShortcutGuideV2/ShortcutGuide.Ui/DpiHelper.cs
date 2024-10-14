// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ShortcutGuide
{
    // This class is rewritten from C++ to C# from the measure tool project
    internal static class DpiHelper
    {
#pragma warning disable SA1310 // Field names should not contain underscore
        private const int DEFAULT_DPI = 96;
        private const int MONITOR_DEFAULTTONEAREST = 2;
        private const int MDT_EFFECTIVE_DPI = 0;
#pragma warning restore SA1310 // Field names should not contain underscore

        public static float GetDPIScaleForWindow(int hwnd)
        {
            int dpi = DEFAULT_DPI;
            GetScreenDPIForWindow(hwnd, ref dpi);
            return (float)dpi / DEFAULT_DPI;
        }

        public static long GetScreenDPIForWindow(int hwnd, ref int dpi)
        {
            var targetMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            return GetScreenDPIForMonitor(targetMonitor.ToInt32(), ref dpi);
        }

        public static long GetScreenDPIForMonitor(int targetMonitor, ref int dpi)
        {
            if (targetMonitor != 0)
            {
                int dummy = 0;
                return GetDpiForMonitor(targetMonitor, MDT_EFFECTIVE_DPI, ref dpi, ref dummy);
            }
            else
            {
                dpi = DEFAULT_DPI;
                return 0x80004005L;
            }
        }

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromWindow(int hwnd, int dwFlags);

        [DllImport("Shcore.dll")]
        private static extern long GetDpiForMonitor(int hmonitor, int dpiType, ref int dpiX, ref int dpiY);
    }
}
