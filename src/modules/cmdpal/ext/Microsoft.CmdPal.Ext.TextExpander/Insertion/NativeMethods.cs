// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.TextExpander.Insertion;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Win32 names.")]
internal static partial class NativeMethods
{
    internal const uint GMEM_MOVEABLE = 0x0002;
    internal const uint INPUT_KEYBOARD = 1;
    internal const uint KEYEVENTF_KEYUP = 0x0002;
    internal const uint KEYEVENTF_UNICODE = 0x0004;
    internal const uint WM_NULL = 0x0000;
    internal const uint SMTO_BLOCK = 0x0001;
    internal const uint SMTO_ABORTIFHUNG = 0x0002;
    internal const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    internal const uint TOKEN_QUERY = 0x0008;
    internal const int TokenIntegrityLevel = 25;
    internal const int ERROR_SUCCESS = 0;

    /// <summary>The parent that makes a window message-only.</summary>
    internal const nint HWND_MESSAGE = -3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint Type;
        public InputUnion U;
    }

    // MOUSEINPUT is the largest member, so it sets the union's size SendInput expects.
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mi;

        [FieldOffset(0)]
        public KEYBDINPUT Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public nuint DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort WVk;
        public ushort WScan;
        public uint DwFlags;
        public uint Time;
        public nuint DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int PtX;
        public int PtY;
        public uint LPrivate;
    }

    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    internal static partial nint SendMessageTimeout(nint hWnd, uint msg, nuint wParam, nint lParam, uint flags, uint timeout, out nuint result);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint count, INPUT[] inputs, int size);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenClipboard(nint hWndNewOwner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EmptyClipboard();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint EnumClipboardFormats(uint format);

    [LibraryImport("user32.dll")]
    internal static partial nint GetClipboardData(uint format);

    [LibraryImport("user32.dll")]
    internal static partial nint SetClipboardData(uint format, nint hMem);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterClipboardFormat(string format);

    [LibraryImport("user32.dll")]
    internal static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll")]
    internal static partial nint GetOpenClipboardWindow();

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowEx(uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW")]
    internal static partial int GetMessage(out MSG msg, nint hWnd, uint filterMin, uint filterMax);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool TranslateMessage(in MSG msg);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(in MSG msg);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GlobalLock(nint hMem);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalUnlock(nint hMem);

    [LibraryImport("kernel32.dll")]
    internal static partial nuint GlobalSize(nint hMem);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GlobalFree(nint hMem);

    [LibraryImport("kernel32.dll")]
    internal static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    internal static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool CloseHandle(nint handle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool OpenProcessToken(nint process, uint access, out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetTokenInformation(nint token, int infoClass, nint info, uint length, out uint returnLength);

    [LibraryImport("advapi32.dll")]
    internal static partial nint GetSidSubAuthorityCount(nint sid);

    [LibraryImport("advapi32.dll")]
    internal static partial nint GetSidSubAuthority(nint sid, uint index);
}
