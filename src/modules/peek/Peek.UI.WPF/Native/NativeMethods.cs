// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Peek.UI.Models;
using static Peek.UI.Native.NativeModels;

namespace Peek.UI.Native
{
    public static class NativeMethods
    {
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern long DwmSetWindowAttribute(
            IntPtr hwnd,
            DwmWindowAttributed attribute,
            ref DwmWindowCornerPreference pvAttribute,
            uint cbAttribute);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("Shlwapi.dll", ExactSpelling = true, PreserveSig = false)]
        internal static extern HResult AssocGetPerceivedType(
            [MarshalAs(UnmanagedType.LPWStr)] string extension,
            out PerceivedType perceivedType,
            out Perceived perceivedFlags,
            IntPtr ptrType);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr pbc,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);
    }
}
