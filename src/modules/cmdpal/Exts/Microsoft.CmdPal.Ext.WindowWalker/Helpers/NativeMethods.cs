// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

using SuppressMessageAttribute = System.Diagnostics.CodeAnalysis.SuppressMessageAttribute;

#pragma warning disable SA1649, CA1051, CA1707, CA1028, CA1714, CA1069, SA1402

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

[SuppressMessage("Interoperability", "CA1401:P/Invokes should not be visible", Justification = "We want plugins to share this NativeMethods class, instead of each one creating its own.")]
public static class NativeMethods
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int EnumWindows(EnumWindowsProc callPtr, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, GetWindowCmd uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int EnumChildWindows(IntPtr hWnd, EnumWindowsProc callPtr, int lPar);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hwnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommand nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("psapi.dll", BestFitMapping = false, CharSet = CharSet.Unicode)]
    public static extern uint GetProcessImageFileName(IntPtr hProcess, [Out] StringBuilder lpImageFileName, [In][MarshalAs(UnmanagedType.U4)] int nSize);

    [DllImport("user32.dll", SetLastError = true, BestFitMapping = false, CharSet = CharSet.Unicode)]
    public static extern IntPtr GetProp(IntPtr hWnd, string lpString);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("dwmapi.dll", EntryPoint = "#113", CallingConvention = CallingConvention.StdCall)]
    public static extern int DwmpActivateLivePreview([MarshalAs(UnmanagedType.Bool)] bool fActivate, IntPtr hWndExclude, IntPtr hWndInsertBefore, LivePreviewTrigger lpt, IntPtr prcFinalRect);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref uint attrValue, int attrSize);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll", BestFitMapping = false, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, out WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    public static extern int SendMessage(IntPtr hWnd, int msg, int wParam);

    [DllImport("user32.dll")]
    public static extern int SendMessageTimeout(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam, int fuFlags, int uTimeout, out int lpdwResult);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumThreadWindows(uint threadId, ShellCommand.EnumThreadDelegate lpfn, IntPtr lParam);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFirmwareType(ref FirmwareType FirmwareType);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    [DllImport("user32")]
    public static extern void LockWorkStation();

    [DllImport("Powrprof.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    public static extern uint SHEmptyRecycleBin(IntPtr hWnd, uint dwFlags);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern HRESULT SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, uint cchOutBuf, IntPtr ppvReserved);

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    public static extern HRESULT SHCreateStreamOnFileEx(string fileName, STGM grfMode, uint attributes, bool create, System.Runtime.InteropServices.ComTypes.IStream reserved, out System.Runtime.InteropServices.ComTypes.IStream stream);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int SHCreateItemFromParsingName([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr pbc, ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IShellItem shellItem);

    [DllImport("rpcrt4.dll")]
    public static extern int UuidCreateSequential(out GUIDDATA Uuid);
}

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "These are the names used by win32.")]
public static class Win32Constants
{
    /// <summary>
    /// GetWindowLong index to retrieves the extended window styles.
    /// </summary>
    public const int GWL_EXSTYLE = -20;

    /// <summary>
    /// A window receives this message when the user chooses a command from the Window menu (formerly known as the system or control menu)
    /// or when the user chooses the maximize button, minimize button, restore button, or close button.
    /// </summary>
    public const int WM_SYSCOMMAND = 0x0112;

    /// <summary>
    /// Restores the window to its normal position and size.
    /// </summary>
    public const int SC_RESTORE = 0xf120;

    /// <summary>
    /// Closes the window
    /// </summary>
    public const int SC_CLOSE = 0xF060;

    /// <summary>
    /// RPC call succeeded
    /// </summary>
    public const int RPC_S_OK = 0;

    /// <summary>
    /// The UUID is guaranteed to be unique to this computer only.
    /// </summary>
    public const int RPC_S_UUID_LOCAL_ONLY = 0x720;
}

public static class ShellItemTypeConstants
{
    /// <summary>
    /// Guid for type IShellItem.
    /// </summary>
    public static readonly Guid ShellItemGuid = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");

    /// <summary>
    /// Guid for type IShellItem2.
    /// </summary>
    public static readonly Guid ShellItem2Guid = new("7E9FB0D3-919F-4307-AB2E-9B1860310C93");
}

public enum HRESULT : uint
{
    /// <summary>
    /// Operation successful.
    /// </summary>
    S_OK = 0x00000000,

    /// <summary>
    /// Operation successful. (negative condition/no operation)
    /// </summary>
    S_FALSE = 0x00000001,

    /// <summary>
    /// Not implemented.
    /// </summary>
    E_NOTIMPL = 0x80004001,

    /// <summary>
    /// No such interface supported.
    /// </summary>
    E_NOINTERFACE = 0x80004002,

    /// <summary>
    /// Pointer that is not valid.
    /// </summary>
    E_POINTER = 0x80004003,

    /// <summary>
    /// Operation aborted.
    /// </summary>
    E_ABORT = 0x80004004,

    /// <summary>
    /// Unspecified failure.
    /// </summary>
    E_FAIL = 0x80004005,

    /// <summary>
    /// Unexpected failure.
    /// </summary>
    E_UNEXPECTED = 0x8000FFFF,

    /// <summary>
    /// General access denied error.
    /// </summary>
    E_ACCESSDENIED = 0x80070005,

    /// <summary>
    /// Handle that is not valid.
    /// </summary>
    E_HANDLE = 0x80070006,

    /// <summary>
    /// Failed to allocate necessary memory.
    /// </summary>
    E_OUTOFMEMORY = 0x8007000E,

    /// <summary>
    /// One or more arguments are not valid.
    /// </summary>
    E_INVALIDARG = 0x80070057,

    /// <summary>
    /// The operation was canceled by the user. (Error source 7 means Win32.)
    /// </summary>
    /// <SeeAlso href="https://learn.microsoft.com/windows/win32/debug/system-error-codes--1000-1299-"/>
    /// <SeeAlso href="https://en.wikipedia.org/wiki/HRESULT"/>
    E_CANCELLED = 0x800704C7,
}

/// <remarks>
/// <see href="https://learn.microsoft.com/windows/win32/api/winnt/ne-winnt-firmware_type">see learn.microsoft.com</see>
/// </remarks>
public enum FirmwareType
{
    Unknown = 0,
    Bios = 1,
    Uefi = 2,
    Max = 3,
}

/// <summary>
/// <see href="https://learn.microsoft.com/windows/win32/stg/stgm-constants">see all STGM values</see>
/// </summary>
[Flags]
public enum STGM : long
{
    READ = 0x00000000L,
    WRITE = 0x00000001L,
    READWRITE = 0x00000002L,
    CREATE = 0x00001000L,
}

public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

/// <summary>
/// Some flags for interop calls to SetWindowPosition
/// </summary>
[Flags]
public enum SetWindowPosFlags : uint
{
    /// <summary>
    ///     If the calling thread and the thread that owns the window are attached to different input queues, the system posts the request to the thread that owns the window. This prevents the calling thread from blocking its execution while other threads process the request.
    /// </summary>
    SWP_ASYNCWINDOWPOS = 0x4000,

    /// <summary>
    ///     Prevents generation of the WM_SYNCPAINT message.
    /// </summary>
    SWP_DEFERERASE = 0x2000,

    /// <summary>
    ///     Draws a frame (defined in the window's class description) around the window.
    /// </summary>
    SWP_DRAWFRAME = 0x0020,

    /// <summary>
    ///     Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE is sent only when the window's size is being changed.
    /// </summary>
    SWP_FRAMECHANGED = 0x0020,

    /// <summary>
    ///     Hides the window.
    /// </summary>
    SWP_HIDEWINDOW = 0x0080,

    /// <summary>
    ///     Does not activate the window. If this flag is not set, the window is activated and moved to the top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter parameter).
    /// </summary>
    SWP_NOACTIVATE = 0x0010,

    /// <summary>
    ///     Discards the entire contents of the client area. If this flag is not specified, the valid contents of the client area are saved and copied back into the client area after the window is sized or repositioned.
    /// </summary>
    SWP_NOCOPYBITS = 0x0100,

    /// <summary>
    ///     Retains the current position (ignores X and Y parameters).
    /// </summary>
    SWP_NOMOVE = 0x0002,

    /// <summary>
    ///     Does not change the owner window's position in the Z order.
    /// </summary>
    SWP_NOOWNERZORDER = 0x0200,

    /// <summary>
    ///     Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent window uncovered as a result of the window being moved. When this flag is set, the application must explicitly invalidate or redraw any parts of the window and parent window that need redrawing.
    /// </summary>
    SWP_NOREDRAW = 0x0008,

    /// <summary>
    ///     Same as the SWP_NOOWNERZORDER flag.
    /// </summary>
    SWP_NOREPOSITION = 0x0200,

    /// <summary>
    ///     Prevents the window from receiving the WM_WINDOWPOSCHANGING message.
    /// </summary>
    SWP_NOSENDCHANGING = 0x0400,

    /// <summary>
    ///     Retains the current size (ignores the cx and cy parameters).
    /// </summary>
    SWP_NOSIZE = 0x0001,

    /// <summary>
    ///     Retains the current Z order (ignores the hWndInsertAfter parameter).
    /// </summary>
    SWP_NOZORDER = 0x0004,

    /// <summary>
    ///     Displays the window.
    /// </summary>
    SWP_SHOWWINDOW = 0x0040,
}

/// <summary>
/// Options for DwmpActivateLivePreview
/// </summary>
public enum LivePreviewTrigger
{
    /// <summary>
    /// Show Desktop button
    /// </summary>
    ShowDesktop = 1,

    /// <summary>
    /// WIN+SPACE hotkey
    /// </summary>
    WinSpace,

    /// <summary>
    /// Hover-over Superbar thumbnails
    /// </summary>
    Superbar,

    /// <summary>
    /// Alt-Tab
    /// </summary>
    AltTab,

    /// <summary>
    /// Press and hold on Superbar thumbnails
    /// </summary>
    SuperbarTouch,

    /// <summary>
    /// Press and hold on Show desktop
    /// </summary>
    ShowDesktopTouch,
}

/// <summary>
/// Show Window Enums
/// </summary>
public enum ShowWindowCommand
{
    /// <summary>
    /// Hides the window and activates another window.
    /// </summary>
    Hide = 0,

    /// <summary>
    /// Activates and displays a window. If the window is minimized or
    /// maximized, the system restores it to its original size and position.
    /// An application should specify this flag when displaying the window
    /// for the first time.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Activates the window and displays it as a minimized window.
    /// </summary>
    ShowMinimized = 2,

    /// <summary>
    /// Maximizes the specified window.
    /// </summary>
    Maximize = 3, // is this the right value?

    /// <summary>
    /// Activates the window and displays it as a maximized window.
    /// </summary>
    ShowMaximized = 3,

    /// <summary>
    /// Displays a window in its most recent size and position. This value
    /// is similar to <see cref="Win32.ShowWindowCommand.Normal"/>, except
    /// the window is not activated.
    /// </summary>
    ShowNoActivate = 4,

    /// <summary>
    /// Activates the window and displays it in its current size and position.
    /// </summary>
    Show = 5,

    /// <summary>
    /// Minimizes the specified window and activates the next top-level
    /// window in the Z order.
    /// </summary>
    Minimize = 6,

    /// <summary>
    /// Displays the window as a minimized window. This value is similar to
    /// <see cref="Win32.ShowWindowCommand.ShowMinimized"/>, except the
    /// window is not activated.
    /// </summary>
    ShowMinNoActive = 7,

    /// <summary>
    /// Displays the window in its current size and position. This value is
    /// similar to <see cref="Win32.ShowWindowCommand.Show"/>, except the
    /// window is not activated.
    /// </summary>
    ShowNA = 8,

    /// <summary>
    /// Activates and displays the window. If the window is minimized or
    /// maximized, the system restores it to its original size and position.
    /// An application should specify this flag when restoring a minimized window.
    /// </summary>
    Restore = 9,

    /// <summary>
    /// Sets the show state based on the SW_* value specified in the
    /// STARTUPINFO structure passed to the CreateProcess function by the
    /// program that started the application.
    /// </summary>
    ShowDefault = 10,

    /// <summary>
    ///  <b>Windows 2000/XP:</b> Minimizes a window, even if the thread
    /// that owns the window is not responding. This flag should only be
    /// used when minimizing windows from a different thread.
    /// </summary>
    ForceMinimize = 11,
}

/// <summary>
/// The rendering policy to use for set window attribute
/// </summary>
[Flags]
public enum DwmNCRenderingPolicies
{
    UseWindowStyle,
    Disabled,
    Enabled,
    Last,
}

/// <summary>
/// DWM window attribute (Windows 7 and earlier: The values between ExcludedFromPeek and Last aren't supported.)
/// </summary>
[Flags]
public enum DwmWindowAttributes
{
    NCRenderingEnabled = 1,
    NCRenderingPolicy = 2,
    TransitionsForceDisabled = 3,
    AllowNCPaint = 4,
    CaptionButtonBounds = 5,
    NonClientRtlLayout = 6,
    ForceIconicRepresentation = 7,
    Flip3DPolicy = 8,
    ExtendedFrameBounds = 9,
    HasIconicBitmap = 10,
    DisallowPeek = 11,
    ExcludedFromPeek = 12,
    Cloak = 13,
    Cloaked = 14,
    FreezeRepresentation = 15,
    PassiveUpdateMode = 16,
    UseHostbackdropbrush = 17,
    UseImmersiveDarkMode = 20,
    WindowCornerPreference = 33,
    BorderColor = 34,
    CaptionColor = 35,
    TextColor = 36,
    VisibleFrameBorderThickness = 37,
    Last,
}

/// <summary>
/// Flags for describing the window cloak state (Windows 7 and earlier: This value is not supported.)
/// </summary>
[Flags]
public enum DwmWindowCloakStates
{
    None = 0,
    CloakedApp = 1,
    CloakedShell = 2,
    CloakedInherited = 4,
}

/// <summary>
/// Flags for accessing the process in trying to get icon for the process
/// </summary>
[Flags]
public enum ProcessAccessFlags
{
    /// <summary>
    /// Required to create a thread.
    /// </summary>
    CreateThread = 0x0002,

    /// <summary>
    /// Required to set the session id for a process.
    /// </summary>
    SetSessionId = 0x0004,

    /// <summary>
    /// Required to perform an operation on the address space of a process
    /// </summary>
    VmOperation = 0x0008,

    /// <summary>
    /// Required to read memory in a process using ReadProcessMemory.
    /// </summary>
    VmRead = 0x0010,

    /// <summary>
    /// Required to write to memory in a process using WriteProcessMemory.
    /// </summary>
    VmWrite = 0x0020,

    /// <summary>
    /// Required to duplicate a handle using DuplicateHandle.
    /// </summary>
    DupHandle = 0x0040,

    /// <summary>
    /// Required to create a process.
    /// </summary>
    CreateProcess = 0x0080,

    /// <summary>
    /// Required to set memory limits using SetProcessWorkingSetSize.
    /// </summary>
    SetQuota = 0x0100,

    /// <summary>
    /// Required to set certain information about a process, such as its priority class (see SetPriorityClass).
    /// </summary>
    SetInformation = 0x0200,

    /// <summary>
    /// Required to retrieve certain information about a process, such as its token, exit code, and priority class (see OpenProcessToken).
    /// </summary>
    QueryInformation = 0x0400,

    /// <summary>
    /// Required to suspend or resume a process.
    /// </summary>
    SuspendResume = 0x0800,

    /// <summary>
    /// Required to retrieve certain information about a process (see GetExitCodeProcess, GetPriorityClass, IsProcessInJob, QueryFullProcessImageName).
    /// A handle that has the PROCESS_QUERY_INFORMATION access right is automatically granted PROCESS_QUERY_LIMITED_INFORMATION.
    /// </summary>
    QueryLimitedInformation = 0x1000,

    /// <summary>
    /// Required to wait for the process to terminate using the wait functions.
    /// </summary>
    Synchronize = 0x100000,

    /// <summary>
    /// Required to delete the object.
    /// </summary>
    Delete = 0x00010000,

    /// <summary>
    /// Required to read information in the security descriptor for the object, not including the information in the SACL.
    /// To read or write the SACL, you must request the ACCESS_SYSTEM_SECURITY access right. For more information, see SACL Access Right.
    /// </summary>
    ReadControl = 0x00020000,

    /// <summary>
    /// Required to modify the DACL in the security descriptor for the object.
    /// </summary>
    WriteDac = 0x00040000,

    /// <summary>
    /// Required to change the owner in the security descriptor for the object.
    /// </summary>
    WriteOwner = 0x00080000,

    /// <summary>
    /// Combines <see cref="Delete"/>, <see cref="ReadControl"/>, <see cref="WriteDac"/>, and <see cref="WriteOwner"/>.
    /// </summary>
    StandardRightsRequired = Delete | ReadControl | WriteDac | WriteOwner, // == 0x000F0000

    /// <summary>
    /// All possible access rights for a process object.
    /// </summary>
    AllAccess = StandardRightsRequired | Synchronize | 0xFFFF,
}

[StructLayout(LayoutKind.Sequential)]
public struct GUIDDATA
{
    public int Data1;
    public short Data2;
    public short Data3;
    [System.Runtime.InteropServices.MarshalAsAttribute(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Data4;
}

/// <summary>
/// Contains information about the placement of a window on the screen.
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct WINDOWPLACEMENT : IEquatable<WINDOWPLACEMENT>
{
    /// <summary>
    /// The length of the structure, in bytes. Before calling the GetWindowPlacement or SetWindowPlacement functions, set this member to sizeof(WINDOWPLACEMENT).
    /// <para>
    /// GetWindowPlacement and SetWindowPlacement fail if this member is not set correctly.
    /// </para>
    /// </summary>
    public int Length;

    /// <summary>
    /// Specifies flags that control the position of the minimized window and the method by which the window is restored.
    /// </summary>
    public int Flags;

    /// <summary>
    /// The current show state of the window.
    /// </summary>
    public ShowWindowCommand ShowCmd;

    /// <summary>
    /// The coordinates of the window's upper-left corner when the window is minimized.
    /// </summary>
    public POINT MinPosition;

    /// <summary>
    /// The coordinates of the window's upper-left corner when the window is maximized.
    /// </summary>
    public POINT MaxPosition;

    /// <summary>
    /// The window's coordinates when the window is in the restored position.
    /// </summary>
    public RECT NormalPosition;

    /// <summary>
    /// Gets the default (empty) value.
    /// </summary>
    public static WINDOWPLACEMENT Default
    {
        get
        {
            WINDOWPLACEMENT result = default;
            result.Length = Marshal.SizeOf(result);
            return result;
        }
    }

    public static bool operator ==(WINDOWPLACEMENT left, WINDOWPLACEMENT right)
    {
        return left.Length == right.Length
            && left.Flags == right.Flags
            && left.ShowCmd == right.ShowCmd
            && left.MinPosition == right.MinPosition
            && left.MaxPosition == right.MaxPosition
            && left.NormalPosition == right.NormalPosition;
    }

    public static bool operator !=(WINDOWPLACEMENT left, WINDOWPLACEMENT right)
    {
        return !(left == right);
    }

    public bool Equals(WINDOWPLACEMENT other)
    {
        return this == other;
    }

    public override bool Equals(object? obj)
    {
        if (obj is WINDOWPLACEMENT wp)
        {
            return this == wp;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Length, Flags, ShowCmd, MinPosition, MaxPosition, NormalPosition);
    }
}

/// <summary>
/// Required pointless variables that we don't use in making a windows show
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct RECT : IEquatable<RECT>
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public RECT(System.Drawing.Rectangle r)
        : this(r.Left, r.Top, r.Right, r.Bottom)
    {
    }

    public int X
    {
        get => Left;

        set
        {
            Right -= Left - value;
            Left = value;
        }
    }

    public int Y
    {
        get => Top;

        set
        {
            Bottom -= Top - value;
            Top = value;
        }
    }

    public int Height
    {
        get => Bottom - Top;
        set => Bottom = value + Top;
    }

    public int Width
    {
        get => Right - Left;
        set => Right = value + Left;
    }

    public System.Drawing.Point Location
    {
        get => new(Left, Top);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public System.Drawing.Size Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public static implicit operator System.Drawing.Rectangle(RECT r)
    {
        return new System.Drawing.Rectangle(r.Left, r.Top, r.Width, r.Height);
    }

    public static implicit operator RECT(System.Drawing.Rectangle r)
    {
        return new RECT(r);
    }

    public static bool operator ==(RECT r1, RECT r2)
    {
        return r1.Equals(r2);
    }

    public static bool operator !=(RECT r1, RECT r2)
    {
        return !r1.Equals(r2);
    }

    public bool Equals(RECT other)
    {
        return other.Left == Left && other.Top == Top && other.Right == Right && other.Bottom == Bottom;
    }

    public override bool Equals(object? obj)
    {
        if (obj is RECT rect)
        {
            return Equals(rect);
        }

        if (obj is System.Drawing.Rectangle rectangle)
        {
            return Equals(new RECT(rectangle));
        }

        return false;
    }

    public override int GetHashCode()
    {
        return ((System.Drawing.Rectangle)this).GetHashCode();
    }

    public override string ToString()
    {
        // Using CurrentCulture since this is user facing
        return string.Format(System.Globalization.CultureInfo.CurrentCulture, "{{Left={0},Top={1},Right={2},Bottom={3}}}", Left, Top, Right, Bottom);
    }
}

/// <summary>
/// Same as the RECT struct above
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct POINT : IEquatable<POINT>
{
    public int X;
    public int Y;

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }

    public POINT(System.Drawing.Point pt)
        : this(pt.X, pt.Y)
    {
    }

    public static implicit operator System.Drawing.Point(POINT p)
    {
        return new System.Drawing.Point(p.X, p.Y);
    }

    public static implicit operator POINT(System.Drawing.Point p)
    {
        return new POINT(p.X, p.Y);
    }

    public override bool Equals(object? obj)
    {
        if (obj is POINT pt)
        {
            return this.X == pt.X && this.Y == pt.X;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    public static bool operator ==(POINT left, POINT right)
    {
        return left.X == right.X && left.Y == right.Y;
    }

    public static bool operator !=(POINT left, POINT right)
    {
        return !(left == right);
    }

    public bool Equals(POINT other)
    {
        return this == other;
    }
}

/// <summary>
/// GetWindow relationship between the specified window and the window whose handle is to be retrieved.
/// </summary>
public enum GetWindowCmd : uint
{
    /// <summary>
    /// The retrieved handle identifies the window of the same type that is highest in the Z order.
    /// </summary>
    GW_HWNDFIRST = 0,

    /// <summary>
    /// The retrieved handle identifies the window of the same type that is lowest in the Z order.
    /// </summary>
    GW_HWNDLAST = 1,

    /// <summary>
    /// The retrieved handle identifies the window below the specified window in the Z order.
    /// </summary>
    GW_HWNDNEXT = 2,

    /// <summary>
    /// The retrieved handle identifies the window above the specified window in the Z order.
    /// </summary>
    GW_HWNDPREV = 3,

    /// <summary>
    /// The retrieved handle identifies the specified window's owner window, if any.
    /// </summary>
    GW_OWNER = 4,

    /// <summary>
    /// The retrieved handle identifies the child window at the top of the Z order, if the specified window
    /// is a parent window.
    /// </summary>
    GW_CHILD = 5,

    /// <summary>
    /// The retrieved handle identifies the enabled popup window owned by the specified window.
    /// </summary>
    GW_ENABLEDPOPUP = 6,
}

/// <summary>
/// The following are the extended window styles
/// </summary>
[Flags]
public enum ExtendedWindowStyles : uint
{
    /// <summary>
    /// The window has a double border; the window can, optionally, be created with a title bar by specifying
    /// the WS_CAPTION style in the dwStyle parameter.
    /// </summary>
    WS_EX_DLGMODALFRAME = 0X0001,

    /// <summary>
    /// The child window created with this style does not send the WM_PARENTNOTIFY message to its parent window
    /// when it is created or destroyed.
    /// </summary>
    WS_EX_NOPARENTNOTIFY = 0X0004,

    /// <summary>
    /// The window should be placed above all non-topmost windows and should stay above all non-topmost windows
    /// and should stay above them, even when the window is deactivated.
    /// </summary>
    WS_EX_TOPMOST = 0X0008,

    /// <summary>
    /// The window accepts drag-drop files.
    /// </summary>
    WS_EX_ACCEPTFILES = 0x0010,

    /// <summary>
    /// The window should not be painted until siblings beneath the window (that were created by the same thread)
    /// have been painted.
    /// </summary>
    WS_EX_TRANSPARENT = 0x0020,

    /// <summary>
    /// The window is a MDI child window.
    /// </summary>
    WS_EX_MDICHILD = 0x0040,

    /// <summary>
    /// The window is intended to be used as a floating toolbar. A tool window has a title bar that is shorter
    /// than a normal title bar, and the window title is drawn using a smaller font. A tool window does not
    /// appear in the taskbar or in the dialog that appears when the user presses ALT+TAB.
    /// </summary>
    WS_EX_TOOLWINDOW = 0x0080,

    /// <summary>
    /// The window has a border with a raised edge.
    /// </summary>
    WS_EX_WINDOWEDGE = 0x0100,

    /// <summary>
    /// The window has a border with a sunken edge.
    /// </summary>
    WS_EX_CLIENTEDGE = 0x0200,

    /// <summary>
    /// The title bar of the window includes a question mark.
    /// </summary>
    WS_EX_CONTEXTHELP = 0x0400,

    /// <summary>
    /// The window has generic "right-aligned" properties. This depends on the window class. This style has
    /// an effect only if the shell language supports reading-order alignment, otherwise is ignored.
    /// </summary>
    WS_EX_RIGHT = 0x1000,

    /// <summary>
    /// The window has generic left-aligned properties. This is the default.
    /// </summary>
    WS_EX_LEFT = 0x0,

    /// <summary>
    /// If the shell language supports reading-order alignment, the window text is displayed using right-to-left
    /// reading-order properties. For other languages, the styles is ignored.
    /// </summary>
    WS_EX_RTLREADING = 0x2000,

    /// <summary>
    /// The window text is displayed using left-to-right reading-order properties. This is the default.
    /// </summary>
    WS_EX_LTRREADING = 0x0,

    /// <summary>
    /// If the shell language supports reading order alignment, the vertical scroll bar (if present) is to
    /// the left of the client area. For other languages, the style is ignored.
    /// </summary>
    WS_EX_LEFTSCROLLBAR = 0x4000,

    /// <summary>
    /// The vertical scroll bar (if present) is to the right of the client area. This is the default.
    /// </summary>
    WS_EX_RIGHTSCROLLBAR = 0x0,

    /// <summary>
    /// The window itself contains child windows that should take part in dialog box, navigation. If this
    /// style is specified, the dialog manager recurses into children of this window when performing
    /// navigation operations such as handling the TAB key, an arrow key, or a keyboard mnemonic.
    /// </summary>
    WS_EX_CONTROLPARENT = 0x10000,

    /// <summary>
    /// The window has a three-dimensional border style intended to be used for items that do not accept
    /// user input.
    /// </summary>
    WS_EX_STATICEDGE = 0x20000,

    /// <summary>
    /// Forces a top-level window onto the taskbar when the window is visible.
    /// </summary>
    WS_EX_APPWINDOW = 0x40000,

    /// <summary>
    /// The window is an overlapped window.
    /// </summary>
    WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,

    /// <summary>
    /// The window is palette window, which is a modeless dialog box that presents an array of commands.
    /// </summary>
    WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,

    /// <summary>
    /// The window is a layered window. This style cannot be used if the window has a class style of either
    /// CS_OWNDC or CS_CLASSDC. Only for top level window before Windows 8, and child windows from Windows 8.
    /// </summary>
    WS_EX_LAYERED = 0x80000,

    /// <summary>
    /// The window does not pass its window layout to its child windows.
    /// </summary>
    WS_EX_NOINHERITLAYOUT = 0x100000,

    /// <summary>
    /// If the shell language supports reading order alignment, the horizontal origin of the window is on the
    /// right edge. Increasing horizontal values advance to the left.
    /// </summary>
    WS_EX_LAYOUTRTL = 0x400000,

    /// <summary>
    /// Paints all descendants of a window in bottom-to-top painting order using double-buffering.
    /// Bottom-to-top painting order allows a descendent window to have translucency (alpha) and
    /// transparency (color-key) effects, but only if the descendent window also has the WS_EX_TRANSPARENT
    /// bit set. Double-buffering allows the window and its descendents to be painted without flicker.
    /// </summary>
    WS_EX_COMPOSITED = 0x2000000,

    /// <summary>
    /// A top-level window created with this style does not become the foreground window when the user
    /// clicks it. The system does not bring this window to the foreground when the user minimizes or closes
    /// the foreground window.
    /// </summary>
    WS_EX_NOACTIVATE = 0x8000000,
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
public interface IShellItem
{
    void BindToHandler(
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv);

    void GetParent(out IShellItem ppsi);

    void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);

    void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);

    void Compare(IShellItem psi, uint hint, out int piOrder);
}

/// <summary>
/// The following are ShellItem DisplayName types.
/// </summary>
[Flags]
public enum SIGDN : uint
{
    NORMALDISPLAY = 0,
    PARENTRELATIVEPARSING = 0x80018001,
    PARENTRELATIVEFORADDRESSBAR = 0x8001c001,
    DESKTOPABSOLUTEPARSING = 0x80028000,
    PARENTRELATIVEEDITING = 0x80031001,
    DESKTOPABSOLUTEEDITING = 0x8004c000,
    FILESYSPATH = 0x80058000,
    URL = 0x80068000,
}
