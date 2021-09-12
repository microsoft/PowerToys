// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using static Wox.Plugin.SharedCommands.ShellCommand;

namespace Wox.Plugin.SharedCommands
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool EnumThreadWindows(uint threadId, EnumThreadDelegate lpfn, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr hwnd);
    }
}
