// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace ColorPicker
{
    // https://learn.microsoft.com/visualstudio/code-quality/ca1060?view=vs-2019
    // will have to rename
    public static class NativeMethods
    {
        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WH_KEYBOARD_LL = 13;
        public const int VkSnapshot = 0x2c;
        public const int KfAltdown = 0x2000;
        public const int LlkhfAltdown = KfAltdown >> 8;
        public const int MonitorinfofPrimary = 0x00000001;
        public const int CCHDEVICENAME = 32;
        public const int CCHFORMNAME = 32;
        public const uint ENUM_CURRENT_SETTINGS = 4294967295;
        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;
        public const int VK_LWIN = 0x5B;
        public const int VK_RWIN = 0x5C;
        public const int VK_ESCAPE = 0x1B;
        public const int WM_HOTKEY = 0x0312;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;
        public const uint MOD_NOREPEAT = 0x4000;

        public delegate bool MonitorEnumProc(
            IntPtr monitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr lParam);

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
        internal static extern IntPtr GetModuleHandle(string name);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool GetMonitorInfo(
            HandleRef hmonitor, [In, Out] MonitorInfoEx info);

        [DllImport("user32.dll", ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern bool EnumDisplayMonitors(
            HandleRef hdc, IntPtr rcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", SetLastError = false, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplaySettingsW(
            string lpszDeviceName,
            uint iModeNum,
            out DEVMODEW lpDevMode);

        [DllImport("user32.dll")]
        internal static extern bool GetCursorPos(out PointInter lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern bool UnhookWindowsHookEx(IntPtr idHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern IntPtr CallNextHookEx(IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
        internal static extern bool SystemParametersInfo(int uiAction, int uiParam, IntPtr pvParam, int fWinIni);

        [DllImport("user32.dll")]
        internal static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        internal static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PointInter
        {
            public int X;
            public int Y;

            public static explicit operator System.Windows.Point(PointInter point) => new System.Windows.Point(point.X, point.Y);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "false positive, used in MonitorResolutionHelper")]
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        internal class MonitorInfoEx
        {
            public int cbSize = Marshal.SizeOf(typeof(MonitorInfoEx));
            public Rect rcMonitor;
            public Rect rcWork;
            public int dwFlags;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public char[] szDevice = new char[32];
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct DEVMODEW
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;

            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;

            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;

            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;

            public short dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;

            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;

            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

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

        [DllImport("user32.dll")]
        internal static extern IntPtr GetOpenClipboardWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowText(int hwnd, StringBuilder text, int count);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        internal static void SetToolWindowStyle(Window win)
        {
            var hwnd = new WindowInteropHelper(win).Handle;
            _ = SetWindowLong(hwnd, GWL_EX_STYLE, GetWindowLong(hwnd, GWL_EX_STYLE) | WS_EX_TOOLWINDOW);
        }
    }
}
