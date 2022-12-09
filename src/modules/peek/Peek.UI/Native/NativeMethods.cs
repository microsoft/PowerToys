// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Native
{
    using System;
    using System.Runtime.InteropServices;
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

    public enum AccessibleObjectID : uint
    {
        OBJID_WINDOW = 0x00000000,
        OBJID_SYSMENU = 0xFFFFFFFF,
        OBJID_TITLEBAR = 0xFFFFFFFE,
        OBJID_MENU = 0xFFFFFFFD,
        OBJID_CLIENT = 0xFFFFFFFC,
        OBJID_VSCROLL = 0xFFFFFFFB,
        OBJID_HSCROLL = 0xFFFFFFFA,
        OBJID_SIZEGRIP = 0xFFFFFFF9,
        OBJID_CARET = 0xFFFFFFF8,
        OBJID_CURSOR = 0xFFFFFFF7,
        OBJID_ALERT = 0xFFFFFFF6,
        OBJID_SOUND = 0xFFFFFFF5,
    }

    public enum WindowEvent : uint
    {
        EVENT_MIN = 0x00000001,
        EVENT_SYSTEM_START = 0x0001,
        EVENT_SYSTEM_SOUND = 0x0001,
        EVENT_SYSTEM_ALERT = 0x0002,
        EVENT_SYSTEM_FOREGROUND = 0x0003,
        EVENT_SYSTEM_MENUSTART = 0x0004,
        EVENT_SYSTEM_MENUEND = 0x0005,
        EVENT_SYSTEM_MENUPOPUPSTART = 0x0006,
        EVENT_SYSTEM_MENUPOPUPEND = 0x0007,
        EVENT_SYSTEM_CAPTURESTART = 0x0008,
        EVENT_SYSTEM_CAPTUREEND = 0x0009,
        EVENT_SYSTEM_MOVESIZESTART = 0x000A,
        EVENT_SYSTEM_MOVESIZEEND = 0x000B,
        EVENT_SYSTEM_CONTEXTHELPSTART = 0x000C,
        EVENT_SYSTEM_CONTEXTHELPEND = 0x000D,
        EVENT_SYSTEM_DRAGDROPSTART = 0x000E,
        EVENT_SYSTEM_DRAGDROPEND = 0x000F,
        EVENT_SYSTEM_DIALOGSTART = 0x0010,
        EVENT_SYSTEM_DIALOGEND = 0x0011,
        EVENT_SYSTEM_SCROLLINGSTART = 0x0012,
        EVENT_SYSTEM_SCROLLINGEND = 0x0013,
        EVENT_SYSTEM_SWITCHSTART = 0x0014,
        EVENT_SYSTEM_SWITCHEND = 0x0015,
        EVENT_SYSTEM_MINIMIZESTART = 0x0016,
        EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_SYSTEM_DESKTOPSWITCH = 0x0020,
        EVENT_SYSTEM_END = 0x00FF,
        EVENT_OEM_DEFINED_START = 0x0101,
        EVENT_OEM_DEFINED_END = 0x01FF,
        EVENT_CONSOLE_START = 0x4001,
        EVENT_CONSOLE_CARET = 0x4001,
        EVENT_CONSOLE_UPDATE_REGION = 0x4002,
        EVENT_CONSOLE_UPDATE_SIMPLE = 0x4003,
        EVENT_CONSOLE_UPDATE_SCROLL = 0x4004,
        EVENT_CONSOLE_LAYOUT = 0x4005,
        EVENT_CONSOLE_START_APPLICATION = 0x4006,
        EVENT_CONSOLE_END_APPLICATION = 0x4007,
        EVENT_CONSOLE_END = 0x40FF,
        EVENT_UIA_EVENTID_START = 0x4E00,
        EVENT_UIA_EVENTID_END = 0x4EFF,
        EVENT_UIA_PROPID_START = 0x7500,
        EVENT_UIA_PROPID_END = 0x75FF,
        EVENT_OBJECT_START = 0x8000,
        EVENT_OBJECT_CREATE = 0x8000,
        EVENT_OBJECT_DESTROY = 0x8001,
        EVENT_OBJECT_SHOW = 0x8002,
        EVENT_OBJECT_HIDE = 0x8003,
        EVENT_OBJECT_REORDER = 0x8004,
        EVENT_OBJECT_FOCUS = 0x8005,
        EVENT_OBJECT_SELECTION = 0x8006,
        EVENT_OBJECT_SELECTIONADD = 0x8007,
        EVENT_OBJECT_SELECTIONREMOVE = 0x8008,
        EVENT_OBJECT_SELECTIONWITHIN = 0x8009,
        EVENT_OBJECT_STATECHANGE = 0x800A,
        EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
        EVENT_OBJECT_NAMECHANGE = 0x800C,
        EVENT_OBJECT_DESCRIPTIONCHANGE = 0x800D,
        EVENT_OBJECT_VALUECHANGE = 0x800E,
        EVENT_OBJECT_PARENTCHANGE = 0x800F,
        EVENT_OBJECT_HELPCHANGE = 0x8010,
        EVENT_OBJECT_DEFACTIONCHANGE = 0x8011,
        EVENT_OBJECT_ACCELERATORCHANGE = 0x8012,
        EVENT_OBJECT_INVOKED = 0x8013,
        EVENT_OBJECT_TEXTSELECTIONCHANGED = 0x8014,
        EVENT_OBJECT_CONTENTSCROLLED = 0x8015,
        EVENT_SYSTEM_ARRANGMENTPREVIEW = 0x8016,
        EVENT_OBJECT_CLOAKED = 0x8017,
        EVENT_OBJECT_UNCLOAKED = 0x8018,
        EVENT_OBJECT_LIVEREGIONCHANGED = 0x8019,
        EVENT_OBJECT_HOSTEDOBJECTSINVALIDATED = 0x8020,
        EVENT_OBJECT_DRAGSTART = 0x8021,
        EVENT_OBJECT_DRAGCANCEL = 0x8022,
        EVENT_OBJECT_DRAGCOMPLETE = 0x8023,
        EVENT_OBJECT_DRAGENTER = 0x8024,
        EVENT_OBJECT_DRAGLEAVE = 0x8025,
        EVENT_OBJECT_DRAGDROPPED = 0x8026,
        EVENT_OBJECT_IME_SHOW = 0x8027,
        EVENT_OBJECT_IME_HIDE = 0x8028,
        EVENT_OBJECT_IME_CHANGE = 0x8029,
        EVENT_OBJECT_TEXTEDIT_CONVERSIONTARGETCHANGED = 0x8030,
        EVENT_OBJECT_END = 0x80FF,
        EVENT_ATOM_START = 0xC000,
        EVENT_AIA_START = 0xA000,
        EVENT_AIA_END = 0xAFFF,
        EVENT_ATOM_END = 0xFFFF,
        EVENT_MAX = 0x7FFFFFFF,
    }

    [Flags]
    public enum WinEventHookFlags : uint
    {
        WINEVENT_OUTOFCONTEXT = 0x0000,
        WINEVENT_SKIPOWNTHREAD = 0x0001,
        WINEVENT_SKIPOWNPROCESS = 0x0002,
        WINEVENT_INCONTEXT = 0x0004,
    }
}
