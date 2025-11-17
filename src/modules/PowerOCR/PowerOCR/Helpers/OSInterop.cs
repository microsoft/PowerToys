// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerOCR;

public static class OSInterop
{
#pragma warning disable SA1310 // Field names should not contain underscore
    public const int WH_KEYBOARD_LL = 13;
    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_ESCAPE = 0x1B;
    public const int WM_HOTKEY = 0x0312;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;

#pragma warning disable CA1401 // P/Invokes should not be visible
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int smIndex);

    public const int SM_CMONITORS = 80;

#pragma warning restore SA1310 // Field names should not contain underscore

    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(int nAction, int nParam, ref RECT rc, int nUpdate);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern bool GetMonitorInfo(HandleRef hmonitor, [In, Out] MONITORINFOEX info);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(HandleRef handle, int flags);

    [DllImport("user32.dll")]
    public static extern bool ClipCursor(ref RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool ClipCursor([In] IntPtr lpRect);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    internal static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("user32.dll")]
    internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern bool UnhookWindowsHookEx(IntPtr idHook);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    internal static extern short GetAsyncKeyState(int vKey);

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr processId);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

#pragma warning restore CA1401 // P/Invokes should not be visible
    [StructLayout(LayoutKind.Sequential)]
    internal struct LowLevelKeyboardInputEvent
    {
        /// <summary>
        /// A virtual-key code. The code must be a value in the range 1 to 254.
        /// </summary>
        public int VirtualCode;

        /// <summary>
        /// A hardware scan code for the key.
        /// </summary>
        public int HardwareScanCode;

        /// <summary>
        /// The extended-key flag, event-injected Flags, context code, and transition-state flag. This member is specified as follows. An application can use the following values to test the keystroke Flags. Testing LLKHF_INJECTED (bit 4) will tell you whether the event was injected. If it was, then testing LLKHF_LOWER_IL_INJECTED (bit 1) will tell you whether or not the event was injected from a process running at lower integrity level.
        /// </summary>
        public int Flags;

        /// <summary>
        /// The time stamp for this message, equivalent to what GetMessageTime would return for this message.
        /// </summary>
        public int TimeStamp;

        /// <summary>
        /// Additional information associated with the message.
        /// </summary>
        public IntPtr AdditionalInformation;
    }

    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;

        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Auto)]
    public class MONITORINFOEX
    {
        public int CbSize { get; set; } = Marshal.SizeOf(typeof(MONITORINFOEX));

        public RECT RcMonitor { get; set; }

        public RECT RcWork { get; set; }

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        private char[] szDevice = new char[32];

        public int DwFlags { get; set; }
    }
}
