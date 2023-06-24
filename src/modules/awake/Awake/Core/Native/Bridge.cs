// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Awake.Core.Models;

namespace Awake.Core.Native
{
    internal sealed class Bridge
    {
        internal delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        [DllImport("Powrprof.dll", SetLastError = true)]
        internal static extern bool GetPwrCapabilities(out SystemPowerCapabilities lpSystemPowerCapabilities);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetConsoleCtrlHandler(ConsoleEventHandler handler, bool add);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern IntPtr CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string filename,
        [MarshalAs(UnmanagedType.U4)] uint access,
        [MarshalAs(UnmanagedType.U4)] FileShare share,
        IntPtr securityAttributes,
        [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
        [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
        IntPtr templateFile);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, uint uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool TrackPopupMenuEx(IntPtr hmenu, uint fuFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr hWndChildAfter, string? className, string? windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, uint msg, nuint wParam, nint lParam);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        internal static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern void PostQuitMessage(int nExitCode);
    }
}
