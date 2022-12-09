// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Native
{
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Text;
    using Peek.Common.Models;

    public static class NativeMethods
    {
        internal const uint PROCESS_ALL_ACCESS = 0x1f0fff;
        internal const IntPtr HWND_TOP = 0;
        internal const uint SWP_DRAWFRAME = 0x0020;
        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOSIZE = 0x0001;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const int WM_SYSCOMMAND = 0x0112;
        internal const int SC_RESTORE = 0xF120;

        [Flags]
        public enum AssocF
        {
            None = 0,
            Init_NoRemapCLSID = 0x1,
            Init_ByExeName = 0x2,
            Open_ByExeName = 0x2,
            Init_DefaultToStar = 0x4,
            Init_DefaultToFolder = 0x8,
            NoUserSettings = 0x10,
            NoTruncate = 0x20,
            Verify = 0x40,
            RemapRunDll = 0x80,
            NoFixUps = 0x100,
            IgnoreBaseClass = 0x200,
        }

        public enum AssocStr
        {
            Command = 1,
            Executable,
            FriendlyDocName,
            FriendlyAppName,
            NoOpen,
            ShellNewValue,
            DDECommand,
            DDEIfExec,
            DDEApplication,
            DDETopic,
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("Shlwapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern HResult AssocQueryString(AssocF flags, AssocStr str, string pszAssoc, string? pszExtra, [Out] StringBuilder? pszOut, [In][Out] ref uint pcchOut);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindowThreadProcessId(IntPtr hWnd, ref IntPtr ProcessId);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr OpenProcess(uint fdwAccess, bool fInherit, IntPtr IDProcess);

        [DllImport("kernel32.dll")]
        internal static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LoadLibrary(string lpLibName);

        [DllImport("kernel32.dll")]
        internal static extern bool FreeLibrary(IntPtr lib);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr bogusAttributes, int dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, int dwCreationFlags, IntPtr lpThreadId);

        [DllImport("kernel32.dll")]
        internal static extern uint WaitForSingleObject(IntPtr hObject, int dwMilliseconds);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetShellWindow();

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        internal static extern int GetCurrentProcessId();

        [DllImport("user32.dll")]
        internal static extern int SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);

        [DllImport("user32.dll")]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        internal static extern int SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern int GetWindowText(Windows.Win32.Foundation.HWND hWnd, StringBuilder lpString, int nMaxCount);
    }
}
