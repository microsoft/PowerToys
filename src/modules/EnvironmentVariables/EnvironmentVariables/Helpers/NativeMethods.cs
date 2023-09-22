// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace EnvironmentVariables.Helpers.Win32
{
    public static class NativeMethods
    {
        internal const int HWND_BROADCAST = 0xffff;

        internal const int WM_SETTINGCHANGE = 0x001A;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int SendNotifyMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
