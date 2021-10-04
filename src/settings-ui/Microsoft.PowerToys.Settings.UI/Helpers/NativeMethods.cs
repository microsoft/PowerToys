// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.Helpers
{
    public static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, NativeKeyboardHelper.INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern short GetAsyncKeyState(int vKey);

#pragma warning disable CA1401 // P/Invokes should not be visible
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern bool FreeLibrary(IntPtr hModule);
#pragma warning restore CA1401 // P/Invokes should not be visible
    }
}
