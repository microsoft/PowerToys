// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Windows APIs.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

// We are sure we dont have managed resource in KEYBDINPUT, IntPtr just holds a value
[module: SuppressMessage("Microsoft.Design", "CA1049:TypesThatOwnNativeResourcesShouldBeDisposable", Scope = "type", Target = "MouseWithoutBorders.NativeMethods+KEYBDINPUT", Justification = "Dotnet port with style preservation")]

// Some other minor issues
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#ConvertStringSidToSid(System.String,System.IntPtr&)", MessageId = "0", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#DrawText(System.IntPtr,System.String,System.Int32,MouseWithoutBorders.NativeMethods+RECT&,System.UInt32)", MessageId = "1", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#SetWindowText(System.IntPtr,System.String)", MessageId = "1", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#FindWindow(System.String,System.String)", MessageId = "0", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#FindWindow(System.String,System.String)", MessageId = "1", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#GetWindowText(System.IntPtr,System.Text.StringBuilder,System.Int32)", MessageId = "1", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#keybd_event(System.Byte,System.Byte,System.UInt32,System.Int32)", MessageId = "3", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#SendMessage(System.IntPtr,System.Int32,System.IntPtr,System.IntPtr)", MessageId = "return", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources", Scope = "member", Target = "MouseWithoutBorders.NativeMethods+KEYBDINPUT.#dwExtraInfo", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields", Scope = "member", Target = "MouseWithoutBorders.NativeMethods+INPUT64.#type", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#TerminateProcess(System.IntPtr,System.IntPtr)", MessageId = "1", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#GetClassName(System.IntPtr,System.Text.StringBuilder,System.Int32)", MessageId = "1", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#GetClassName(System.IntPtr,System.Text.StringBuilder,System.Int32)", MessageId = "return", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#GetAsyncKeyState(System.IntPtr)", MessageId = "0", Justification = "Dotnet port with style preservation")]
[module: SuppressMessage("Microsoft.Portability", "CA1901:PInvokeDeclarationsShouldBePortable", Scope = "member", Target = "MouseWithoutBorders.NativeMethods.#GetAsyncKeyState(System.IntPtr)", MessageId = "return", Justification = "Dotnet port with style preservation")]

// Disable the warning to preserve original code
#pragma warning disable CA1716
namespace MouseWithoutBorders.Class
#pragma warning restore CA1716
{
    internal partial class NativeMethods
    {
#if !MM_HELPER

        [DllImport("user32.dll")]
        internal static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        internal static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetProcessDPIAware();

        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int SetProcessDpiAwareness(uint type); // Win 8.1 and up, DPI can be per monitor.

        [DllImport("kernel32.dll")]
        internal static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        internal static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTSInfoClass infoClass, out IntPtr ppBuffer, out int pBytesReturned);

        [DllImport("Wtsapi32.dll")]
        internal static extern void WTSFreeMemory(IntPtr pointer);

        internal enum WTSInfoClass
        {
            WTSInitialProgram,
            WTSApplicationName,
            WTSWorkingDirectory,
            WTSOEMId,
            WTSSessionId,
            WTSUserName,
            WTSWinStationName,
            WTSDomainName,
            WTSConnectState,
            WTSClientBuildNumber,
            WTSClientName,
            WTSClientDirectory,
            WTSClientProductId,
            WTSClientHardwareId,
            WTSClientAddress,
            WTSClientDisplay,
            WTSClientProtocolType,
            WTSIdleTime,
            WTSLogonTime,
            WTSIncomingBytes,
            WTSOutgoingBytes,
            WTSIncomingFrames,
            WTSOutgoingFrames,
            WTSClientInfo,
            WTSSessionInfo,
        }

#endif

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, int action, IntPtr changeInfo);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PostMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = false)]
        internal static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        internal static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern int DrawText(IntPtr hDC, string lpString, int nCount, ref RECT lpRect, uint uFormat);

        [DllImport("gdi32.dll")]
        internal static extern uint SetTextColor(IntPtr hdc, int crColor);

        [DllImport("gdi32.dll")]
        internal static extern uint SetBkColor(IntPtr hdc, int crColor);

        [DllImport("user32.dll")]
        internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern IntPtr GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        /*
        internal const int SW_MAXIMIZE = 3;

        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;

            public static WINDOWPLACEMENT Default
            {
                get
                {
                    WINDOWPLACEMENT result = new WINDOWPLACEMENT();
                    result.length = Marshal.SizeOf(result);
                    return result;
                }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
         * */

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr handle);

        // [DllImport("user32.dll")]
        // [return: MarshalAs(UnmanagedType.Bool)]
        // internal static extern bool IsWindowVisible(IntPtr hWnd);

        // [DllImport("user32")]
        // internal static extern int GetKeyboardState(byte[] pbKeyState);

        // [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        // internal static extern short GetKeyState(int vKey);
        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Point p);

        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, ref IntPtr TokenHandle);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateToken(IntPtr ExistingTokenHandle, int SECURITY_IMPERSONATION_LEVEL, ref IntPtr DuplicateTokenHandle);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DuplicateTokenEx(
            IntPtr ExistingTokenHandle,
            uint dwDesiredAccess,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            int TokenType,
            int ImpersonationLevel,
            ref IntPtr DuplicateTokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSidToSid(string StringSid, out IntPtr ptrSid);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, ref TOKEN_MANDATORY_LABEL TokenInformation, uint TokenInformationLength);

        // [DllImport("advapi32.dll", SetLastError = true)]
        // [return: MarshalAs(UnmanagedType.Bool)]
        // internal static extern bool SetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, UInt32 TokenInformationLength);
        [SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", Justification = "Dotnet port with style preservation")]
        [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUser", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            ref SECURITY_ATTRIBUTES lpProcessAttributes,
            ref SECURITY_ATTRIBUTES lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            EnumMonitorsDelegate lpfnEnum,
            IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        [DllImport("User32.dll", CharSet = CharSet.Unicode)]
        internal static extern int FindWindow(string ClassName, string WindowName);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr GetThreadDesktop(uint dwThreadId);

        [DllImport("user32.dll")]
        internal static extern short GetAsyncKeyState(IntPtr vKey); // Keys vKey

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int x;
            internal int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll")]
        internal static extern bool GetCursorInfo(out CURSORINFO ci);

#if CUSTOMIZE_LOGON_SCREEN
        [DllImport("kernel32", SetLastError = true)]
        internal static extern uint WaitForSingleObject(IntPtr handle, int milliseconds);

        internal const uint WAIT_OBJECT_0 = 0x00000000;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_BASIC_INFORMATION
        {
            public int ExitStatus;
            public int PebBaseAddress;
            public int AffinityMask;
            public int BasePriority;
            public uint UniqueProcessId;
            public uint InheritedFromUniqueProcessId;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool TerminateProcess(IntPtr hProcess, IntPtr exitCode);

        [DllImport("ntdll.dll")]
        internal static extern int NtQueryInformationProcess(
           IntPtr hProcess,
           int processInformationClass /* 0 */,
           ref PROCESS_BASIC_INFORMATION processBasicInformation,
           uint processInformationLength,
           out uint returnLength);
#endif

#if USE_GetSecurityDescriptorSacl
        internal enum SE_OBJECT_TYPE
        {
            SE_UNKNOWN_OBJECT_TYPE = 0,
            SE_FILE_OBJECT,
            SE_SERVICE,
            SE_PRINTER,
            SE_REGISTRY_KEY,
            SE_LMSHARE,
            SE_KERNEL_OBJECT,
            SE_WINDOW_OBJECT,
            SE_DS_OBJECT,
            SE_DS_OBJECT_ALL,
            SE_PROVIDER_DEFINED_OBJECT,
            SE_WMIGUID_OBJECT,
            SE_REGISTRY_WOW64_32KEY
        }

        [Flags]
        internal enum SECURITY_INFORMATION : uint
        {
            LABEL_SECURITY_INFORMATION = 0x00000010
        }

        [StructLayoutAttribute(LayoutKind.Explicit)]
        internal struct SECURITY_DESCRIPTOR
        {
            [FieldOffset(0)]
            public byte revision;

            [FieldOffset(1)]
            public byte size;

            [FieldOffset(2)]
            public short control;

            [FieldOffset(4)]
            public IntPtr owner;

            [FieldOffset(8)]
            public IntPtr group;

            [FieldOffset(12)]
            public IntPtr sacl;

            [FieldOffset(16)]
            public IntPtr dacl;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ACL { public byte AclRevision; public byte Sbz1; public int AclSize; public int AceCount; public int Sbz2; }

        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string StringSecurityDescriptor,
        UInt32 StringSDRevision, out SECURITY_DESCRIPTOR SecurityDescriptor, out UInt64 SecurityDescriptorSize);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern int GetSecurityDescriptorSacl([MarshalAs(UnmanagedType.Struct)] ref SECURITY_DESCRIPTOR pSecurityDescriptor, int lpbSaclPresent, [MarshalAs(UnmanagedType.Struct)] ref ACL pSacl, int lpbSaclDefaulted);

        [DllImport("advapi32.dll", CharSet = CharSet.Auto)]
        internal static extern uint SetNamedSecurityInfo(
            string pObjectName,
            SE_OBJECT_TYPE ObjectType,
            SECURITY_INFORMATION SecurityInfo,
            IntPtr psidOwner,
            IntPtr psidGroup,
            IntPtr pDacl,
            IntPtr pSacl);
#endif

#if SINGLE_PROCESS
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetThreadDesktop(IntPtr hDesktop);
#endif

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr OpenInputDesktop(uint dwFlags, [MarshalAs(UnmanagedType.Bool)] bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, [Out] byte[] pvInfo, int nLength, out uint lpnLengthNeeded);

        // [DllImport("user32.dll")]
        // [return: MarshalAs(UnmanagedType.Bool)]
        // internal static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);

        // [DllImport("gdi32.dll")]
        // internal static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);
        [DllImport("gdi32.dll")]
        internal static extern uint SetPixel(IntPtr hdc, int X, int Y, uint crColor);

        // internal const int WM_CLOSE = 16;
        internal const int WM_SHOW_DRAG_DROP = 0x400;

        internal const int WM_HIDE_DRAG_DROP = 0x401;
        internal const int WM_CHECK_EXPLORER_DRAG_DROP = 0x402;
        internal const int WM_QUIT = 0x403;
        internal const int WM_SWITCH = 0x404;
        internal const int WM_HIDE_DD_HELPER = 0x405;
        internal const int WM_SHOW_SETTINGS_FORM = 0x406;

        internal static readonly IntPtr HWND_TOPMOST = new(-1);

        // internal static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        // internal static readonly IntPtr HWND_TOP = new IntPtr(0);
        // internal static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        internal const uint SWP_NOSIZE = 0x0001;

        internal const uint SWP_NOMOVE = 0x0002;
        internal const uint SWP_NOZORDER = 0x0004;
        internal const uint SWP_NOREDRAW = 0x0008;
        internal const uint SWP_SHOWWINDOW = 0x0040;
        internal const uint SWP_HIDEWINDOW = 0x0080;

        internal const int UOI_FLAGS = 1;
        internal const int UOI_NAME = 2;
        internal const int UOI_TYPE = 3;
        internal const int UOI_USER_SID = 4;
        internal const uint DESKTOP_WRITEOBJECTS = 0x0080;
        internal const uint DESKTOP_READOBJECTS = 0x0001;
        internal const uint DF_ALLOWOTHERACCOUNTHOOK = 0x0001;

        // internal const UInt32 GENERIC_READ                     = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;

        // internal const UInt32 GENERIC_EXECUTE                  = 0x20000000;
        internal const uint GENERIC_ALL = 0x10000000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct RECT
        {
            internal int Left;
            internal int Top;
            internal int Right;
            internal int Bottom;
        }

        // size of a device name string
        internal const int CCHDEVICENAME = 32;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MonitorInfoEx
        {
            internal int cbSize;
            internal RECT rcMonitor;
            internal RECT rcWork;
            internal uint dwFlags;

            // [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            // internal string szDeviceName;
        }

        // We are WOW
        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall,
            SetLastError = true)]
        internal static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall,
            SetLastError = true)]
        internal static extern int UnhookWindowsHookEx(int idHook);

        // In X64, we are running WOW
        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto,
            CallingConvention = CallingConvention.StdCall)]
        internal static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        // [DllImport("user32")]
        // internal static extern int ToAscii(int uVirtKey, int uScanCode, byte[] lpbKeyState, byte[] lpwTransKey, int fuState);
        private enum InputType
        {
            INPUT_MOUSE = 0,
            INPUT_KEYBOARD = 1,
            INPUT_HARDWARE = 2,
        }

        [Flags]
        internal enum MOUSEEVENTF
        {
            MOVE = 0x0001,
            LEFTDOWN = 0x0002,
            LEFTUP = 0x0004,
            RIGHTDOWN = 0x0008,
            RIGHTUP = 0x0010,
            MIDDLEDOWN = 0x0020,
            MIDDLEUP = 0x0040,
            XDOWN = 0x0080,
            XUP = 0x0100,
            WHEEL = 0x0800,
            VIRTUALDESK = 0x4000,
            ABSOLUTE = 0x8000,
        }

        [Flags]
        internal enum KEYEVENTF
        {
            KEYDOWN = 0x0000,
            EXTENDEDKEY = 0x0001,
            KEYUP = 0x0002,
            UNICODE = 0x0004,
            SCANCODE = 0x0008,
        }

        // http://msdn.microsoft.com/en-us/library/ms646273(VS.85).aspx
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            internal int dx;
            internal int dy;
            internal int mouseData;
            internal int dwFlags;
            internal int time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            internal short wVk;
            internal short wScan;
            internal int dwFlags;
            internal int time;
            internal IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HARDWAREINPUT
        {
            internal int uMsg;
            internal short wParamL;
            internal short wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUT
        {
            [FieldOffset(0)]
            internal int type;

            [FieldOffset(4)]
            internal MOUSEINPUT mi;

            [FieldOffset(4)]
            internal KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUT64
        {
            [FieldOffset(0)]
            internal int type;

            [FieldOffset(8)]
            internal MOUSEINPUT mi;

            [FieldOffset(8)]
            internal KEYBDINPUT ki;
        }

        [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
        internal static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll", EntryPoint = "SendInput", SetLastError = true)]
        internal static extern uint SendInput64(uint nInputs, INPUT64[] pInputs, int cbSize);

        internal static bool InjectMouseInputAvailable { get; set; }

        [DllImport("user32.dll", EntryPoint = "GetMessageExtraInfo", SetLastError = true)]
        internal static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll", EntryPoint = "LockWorkStation", SetLastError = true)]
        internal static extern uint LockWorkStation();

        // [DllImport("user32.dll")]
        // internal static extern void keybd_event(byte bVk, byte bScan, UInt32 dwFlags, int dwExtraInfo);
        [DllImport("user32.dll")]
        internal static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID
        {
            internal int LowPart;
            internal int HighPart;
        }// end struct

        [StructLayout(LayoutKind.Sequential)]
        internal struct LUID_AND_ATTRIBUTES
        {
            internal LUID Luid;
            internal int Attributes;
        }// end struct

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_PRIVILEGES
        {
            internal int PrivilegeCount;

            // LUID_AND_ATTRIBUTES
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            internal int[] Privileges;
        }

        internal const int READ_CONTROL = 0x00020000;

        internal const int STANDARD_RIGHTS_REQUIRED = 0x000F0000;

        internal const int STANDARD_RIGHTS_READ = READ_CONTROL;
        internal const int STANDARD_RIGHTS_WRITE = READ_CONTROL;
        internal const int STANDARD_RIGHTS_EXECUTE = READ_CONTROL;

        internal const int STANDARD_RIGHTS_ALL = 0x001F0000;

        internal const int SPECIFIC_RIGHTS_ALL = 0x0000FFFF;

        internal const int TOKEN_IMPERSONATE = 0x0004;
        internal const int TOKEN_QUERY_SOURCE = 0x0010;
        internal const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
        internal const int TOKEN_ADJUST_GROUPS = 0x0040;
        internal const int TOKEN_ADJUST_SESSIONID = 0x0100;

        internal const int TOKEN_ALL_ACCESS_P = STANDARD_RIGHTS_REQUIRED |
                                      TOKEN_ASSIGN_PRIMARY |
                                      TOKEN_DUPLICATE |
                                      TOKEN_IMPERSONATE |
                                      TOKEN_QUERY |
                                      TOKEN_QUERY_SOURCE |
                                      TOKEN_ADJUST_PRIVILEGES |
                                      TOKEN_ADJUST_GROUPS |
                                      TOKEN_ADJUST_DEFAULT;

        internal const int TOKEN_ALL_ACCESS = TOKEN_ALL_ACCESS_P | TOKEN_ADJUST_SESSIONID;

        internal const int TOKEN_READ = STANDARD_RIGHTS_READ | TOKEN_QUERY;

        internal const int TOKEN_WRITE = STANDARD_RIGHTS_WRITE |
                                      TOKEN_ADJUST_PRIVILEGES |
                                      TOKEN_ADJUST_GROUPS |
                                      TOKEN_ADJUST_DEFAULT;

        internal const int TOKEN_EXECUTE = STANDARD_RIGHTS_EXECUTE;

        internal const int CREATE_NEW_PROCESS_GROUP = 0x00000200;
        internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;

        internal const int IDLE_PRIORITY_CLASS = 0x40;
        internal const int NORMAL_PRIORITY_CLASS = 0x20;
        internal const int HIGH_PRIORITY_CLASS = 0x80;
        internal const int REALTIME_PRIORITY_CLASS = 0x100;

        internal const int CREATE_NEW_CONSOLE = 0x00000010;

        internal const string SE_DEBUG_NAME = "SeDebugPrivilege";
        internal const string SE_RESTORE_NAME = "SeRestorePrivilege";
        internal const string SE_BACKUP_NAME = "SeBackupPrivilege";

        internal const int SE_PRIVILEGE_ENABLED = 0x0002;

        internal const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESSENTRY32
        {
            internal uint dwSize;
            internal uint cntUsage;
            internal uint th32ProcessID;
            internal IntPtr th32DefaultHeapID;
            internal uint th32ModuleID;
            internal uint cntThreads;
            internal uint th32ParentProcessID;
            internal int pcPriClassBase;
            internal uint dwFlags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string szExeFile;
        }

        internal const uint TH32CS_SNAPPROCESS = 0x00000002;

        // internal static int INVALID_HANDLE_VALUE = -1;
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr hSnapshot);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SECURITY_ATTRIBUTES
        {
            internal int Length;
            internal IntPtr lpSecurityDescriptor;
            internal bool bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PROCESS_INFORMATION
        {
            internal IntPtr hProcess;
            internal IntPtr hThread;
            internal uint dwProcessId;
            internal uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct STARTUPINFO
        {
            internal int cb;
            internal string lpReserved;
            internal string lpDesktop;
            internal string lpTitle;
            internal uint dwX;
            internal uint dwY;
            internal uint dwXSize;
            internal uint dwYSize;
            internal uint dwXCountChars;
            internal uint dwYCountChars;
            internal uint dwFillAttribute;
            internal uint dwFlags;
            internal short wShowWindow;
            internal short cbReserved2;
            internal IntPtr lpReserved2;
            internal IntPtr hStdInput;
            internal IntPtr hStdOutput;
            internal IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SID_AND_ATTRIBUTES
        {
            internal IntPtr Sid;
            internal int Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_MANDATORY_LABEL
        {
            internal SID_AND_ATTRIBUTES Label;
        }

        internal const int TOKEN_DUPLICATE = 0x0002;
        internal const int TOKEN_QUERY = 0x0008;
        internal const int TOKEN_ADJUST_DEFAULT = 0x0080;
        internal const int TOKEN_ASSIGN_PRIMARY = 0x0001;
        internal const uint MAXIMUM_ALLOWED = 0x2000000;
        internal const int SE_GROUP_INTEGRITY = 0x00000020;

        internal enum SECURITY_IMPERSONATION_LEVEL : int
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }

        internal enum TOKEN_TYPE : int
        {
            TokenPrimary = 1,
            TokenImpersonation = 2,
        }

        internal enum TOKEN_INFORMATION_CLASS : int
        {
            TokenUser = 1,
            TokenGroups,
            TokenPrivileges,
            TokenOwner,
            TokenPrimaryGroup,
            TokenDefaultDacl,
            TokenSource,
            TokenType,
            TokenImpersonationLevel,
            TokenStatistics,
            TokenRestrictedSids,
            TokenSessionId,
            TokenGroupsAndPrivileges,
            TokenSessionReference,
            TokenSandBoxInert,
            TokenAuditPolicy,
            TokenOrigin,
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            MaxTokenInfoClass,
        }

        // [DllImport("kernel32.dll")]
        // internal static extern int Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        // [DllImport("kernel32.dll")]
        // internal static extern int Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

        // [DllImport("kernel32.dll", SetLastError = true)]
        // internal static extern IntPtr CreateToolhelp32Snapshot(UInt32 dwFlags, UInt32 th32ProcessID);
        [DllImport("Wtsapi32.dll", SetLastError = true)]
        internal static extern uint WTSQueryUserToken(uint SessionId, ref IntPtr phToken);

        [SuppressMessage("Microsoft.Globalization", "CA2101:SpecifyMarshalingForPInvokeStringArguments", MessageId = "1", Justification = "Dotnet port with style preservation")]
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LookupPrivilegeValue(IntPtr lpSystemName, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        // [DllImport("kernel32.dll")]
        // [return: MarshalAs(UnmanagedType.Bool)]
        // static extern bool ProcessIdToSessionId(UInt32 dwProcessId, ref UInt32 pSessionId);
        [DllImport("kernel32.dll")]
        internal static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreateEnvironmentBlock(ref IntPtr lpEnvironment, IntPtr hToken, [MarshalAs(UnmanagedType.Bool)] bool bInherit);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RevertToSelf();

        internal delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        internal delegate int HookProc(int nCode, int wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        /*
        [DllImport("Netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        internal extern static int NetUserGetInfo([MarshalAs(UnmanagedType.LPWStr)] string ServerName,
            [MarshalAs(UnmanagedType.LPWStr)] string UserName, int level,out IntPtr BufPtr);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct USER_INFO_10
        {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string usri10_name;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string usri10_comment;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string usri10_usr_comment;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string usri10_full_name;
        }

        [DllImport("Netapi32.dll", SetLastError = true)]
        internal static extern int NetApiBufferFree(IntPtr Buffer);
        */

        internal enum EXTENDED_NAME_FORMAT
        {
            NameUnknown = 0,
            NameFullyQualifiedDN = 1,
            NameSamCompatible = 2,
            NameDisplay = 3,
            NameUniqueId = 6,
            NameCanonical = 7,
            NameUserPrincipal = 8,
            NameCanonicalEx = 9,
            NameServicePrincipal = 10,
            NameDnsDomain = 12,
        }

        [DllImport("secur32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool GetUserNameEx(int nameFormat, StringBuilder userName, ref uint userNameSize);

        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hMonitor, uint dpiType, out uint dpiX, out uint dpiY);

        private static string GetDNSDomain()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return null;
            }

            StringBuilder userName = new(1024);
            uint userNameSize = (uint)userName.Capacity;

            if (GetUserNameEx((int)EXTENDED_NAME_FORMAT.NameDnsDomain, userName, ref userNameSize))
            {
                string[] nameParts = userName.ToString()
                    .Split('\\');
                return nameParts.Length != 2 ? null : nameParts[0];
            }

            return null;
        }

        /// <summary>
        /// Use this method to figure out if your code is running on a Microsoft computer.
        /// </summary>
        /// <returns>True if running on a Microsoft computer, otherwise false.</returns>
        internal static bool IsRunningAtMicrosoft()
        {
            string domain = GetDNSDomain();

            return !string.IsNullOrEmpty(domain) && domain.EndsWith("microsoft.com", true, System.Globalization.CultureInfo.CurrentCulture);
        }

        private NativeMethods()
        {
        }
    }
}
