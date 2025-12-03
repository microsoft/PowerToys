// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace RunnerV2
{
    internal static partial class NativeMethods
    {
        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [LibraryImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetTokenInformation(IntPtr tokenHandle, TOKEN_INFORMATION_CLASS tokenInformationClass, ref TokenElevation tokenInformation, uint tokenInformationLength, out uint returnLength);

        [LibraryImport("Kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseHandle(IntPtr hObject);

        internal enum TOKEN_INFORMATION_CLASS
        {
            TOKEN_ELEVATION = 20,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TokenElevation
        {
            public uint TokenIsElevated;
        }

        internal const int TOKENQUERY = 0x0008;

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool UnregisterHotKey(IntPtr hWnd, int id);

        [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [LibraryImport("user32.dll", SetLastError = true)]
        internal static partial IntPtr CreatePopupMenu();

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetForegroundWindow(IntPtr hWnd);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

        internal const uint NIMADD = 0x00000000;
        internal const uint NIMDELETE = 0x00000002;

        internal struct NOTIFYICONDATA
        {
            public uint CbSize;
            public IntPtr HWnd;
            public uint UId;
            public uint UFlags;
            public uint UCallbackMessage;
            public IntPtr HIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SzTip;
            public uint DwState;
            public uint DwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SzInfo;
            public uint UTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string SzInfoTitle;
            public uint DwInfoFlags;
            public Guid GuidItem;
            public IntPtr HBalloonIcon;
        }

        [DllImport("shell32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpdata);

        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);

        internal const uint CSVREDRAW = 0x0001;
        internal const uint CSHREDRAW = 0x0002;

        internal const uint WSOVERLAPPEDWINDOW = 0x00CF0000;
        internal const uint WSPOPUP = 0x80000000;

        internal const int CWUSEDEFAULT = unchecked((int)0x80000000);

        internal static readonly IntPtr IDCARROW = new(32512);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DispatchMessageW(ref MSG lpMsg);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("kernel32.dll")]
        internal static partial ushort AddAtomW([MarshalAs(UnmanagedType.LPWStr)] string lpString);

        internal struct MSG
        {
            public IntPtr HWnd;
            public uint Message;
            public UIntPtr WParam;
            public long LParam;
            public ulong Time;
            public Point Pt;
        }

        internal enum WindowMessages : uint
        {
            COMMAND = 0x0111,
            HOTKEY = 0x0312,
            ICON_NOTIFY = 0x0800,
            WINDOWPOSCHANGING = 0x0046,
            DESTROY = 0x0002,
            REFRESH_SETTINGS = 0x0400 + 2,
        }

        [DllImport("user32.dll")]
        internal static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [LibraryImport("user32.dll", SetLastError = false)]
        internal static partial IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll", SetLastError = true)]
        internal static partial uint RegisterWindowMessageW([MarshalAs(UnmanagedType.LPWStr)] string lpString);

        [LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial nint CreateWindowExW(
            uint dwExStyle,
            string lpClassName,
            string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct WNDCLASS
        {
            public uint Style;
            public WndProc LpfnWndProc;
            public int CbClsExtra;
            public int CbWndExtra;
            public IntPtr HInstance;
            public IntPtr HIcon;
            public IntPtr HCursor;
            public IntPtr HbrBackground;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LpszMenuName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LpszClassName;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        internal delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("Advapi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
            [MarshalAs(UnmanagedType.LPWStr)] string StringSecurityDescriptor,
            uint StringSDRevision,
            out IntPtr SecurityDescriptor,
            out uint SecurityDescriptorSize);

        [DllImport("Advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MakeAbsoluteSD(
            IntPtr pSelfRelativeSD,
            IntPtr pAbsoluteSD,
            ref uint lpdwAbsoluteSDSize,
            IntPtr pDacl,
            ref uint lpdwDaclSize,
            IntPtr pSacl,
            ref uint lpdwSaclSize,
            IntPtr pOwner,
            ref uint lpdwOwnerSize,
            IntPtr pPrimaryGroup,
            ref uint lpdwPrimaryGroupSize);

        [DllImport("ole32.dll", SetLastError = true)]
        internal static extern int CoInitializeSecurity(
            IntPtr pSecDesc,
            int cAuthSvc,
            IntPtr asAuthSvc,
            IntPtr pReserved1,
            uint dwAuthnLevel,
            uint dwImpLevel,
            IntPtr pAuthList,
            uint dwCapabilities,
            IntPtr pReserved3);

        [DllImport("user32.dll")]
        internal static extern uint SendInput(uint nInputs, NativeKeyboardHelper.INPUT[] pInputs, int cbSize);
    }
}
