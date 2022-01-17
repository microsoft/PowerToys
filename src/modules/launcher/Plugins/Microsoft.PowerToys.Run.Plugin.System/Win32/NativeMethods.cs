// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Run.Plugin.System.Win32
{
    internal class NativeMethods
    {
        [DllImport("user32")]
        internal static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        [DllImport("user32")]
        internal static extern void LockWorkStation();

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint SHEmptyRecycleBin(IntPtr hWnd, uint dwFlags);

        [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        // http://www.pinvoke.net/default.aspx/Enums/HRESULT.html
        public enum HRESULT : uint
        {
            S_FALSE = 0x0001,
            S_OK = 0x0000,
        }
    }
}
