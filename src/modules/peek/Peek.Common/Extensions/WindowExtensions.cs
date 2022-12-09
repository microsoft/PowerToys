// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.Common.Extensions
{
    using Windows.Foundation;
    using Windows.Win32;
    using Windows.Win32.Graphics.Gdi;

    public static class WindowExtensions
    {
        public static Size GetMainMonitorSize()
        {
            System.Drawing.Point zero = new (0, 0);
            HMONITOR monitor = PInvoke.MonitorFromPoint(zero, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);

            MONITORINFO info = new ();
            info.cbSize = 40;
            PInvoke.GetMonitorInfo(monitor, ref info);

            double monitorWidth = info.rcMonitor.left + info.rcMonitor.right;
            double monitorHeight = info.rcMonitor.bottom + info.rcMonitor.top;

            return new Size(monitorWidth, monitorHeight);
        }
    }
}
