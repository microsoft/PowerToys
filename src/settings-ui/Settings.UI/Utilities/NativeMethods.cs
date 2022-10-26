// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.Utilities
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        public const int SWRESTORE = 9;

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        public static extern bool SetForegroundWindow(IntPtr handle);

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        public static extern bool ShowWindow(IntPtr handle, int nCmdShow);

        [System.Runtime.InteropServices.DllImport("User32.dll")]
        public static extern bool IsIconic(IntPtr handle);
    }
}
