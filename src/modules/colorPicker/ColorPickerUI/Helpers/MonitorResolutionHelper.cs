// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Foundation;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Helpers
{
    public class MonitorResolutionHelper
    {
        public static readonly HandleRef NullHandleRef = new HandleRef(null, IntPtr.Zero);

        private MonitorResolutionHelper(IntPtr monitor, IntPtr hdc)
        {
            var info = new MonitorInfoEx();
            GetMonitorInfo(new HandleRef(null, monitor), info);
            Bounds = new Windows.Foundation.Rect(
                info.rcMonitor.left,
                info.rcMonitor.top,
                info.rcMonitor.right - info.rcMonitor.left,
                info.rcMonitor.bottom - info.rcMonitor.top);
            WorkingArea = new Windows.Foundation.Rect(
                info.rcWork.left,
                info.rcWork.top,
                info.rcWork.right - info.rcWork.left,
                info.rcWork.bottom - info.rcWork.top);
            IsPrimary = (info.dwFlags & MonitorinfofPrimary) != 0;
            Name = new string(info.szDevice).TrimEnd((char)0);
        }

        public static IEnumerable<MonitorResolutionHelper> AllMonitors
        {
            get
            {
                var closure = new MonitorEnumCallback();
                var proc = new MonitorEnumProc(closure.Callback);
                EnumDisplayMonitors(NullHandleRef, IntPtr.Zero, proc, IntPtr.Zero);
                return closure.Monitors.Cast<MonitorResolutionHelper>();
            }
        }

        public Windows.Foundation.Rect Bounds { get; private set; }

        public Windows.Foundation.Rect WorkingArea { get; private set; }

        public string Name { get; private set; }

        public bool IsPrimary { get; private set; }

        public static bool HasMultipleMonitors()
        {
            return AllMonitors.Count() > 1;
        }

        private class MonitorEnumCallback
        {
            public MonitorEnumCallback()
            {
                Monitors = new ArrayList();
            }

            public ArrayList Monitors { get; private set; }

            public bool Callback(
                IntPtr monitor,
                IntPtr hdc,
                IntPtr lprcMonitor,
                IntPtr lparam)
            {
                Monitors.Add(new MonitorResolutionHelper(monitor, hdc));
                return true;
            }
        }
    }
}
