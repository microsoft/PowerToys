// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace PowerOCR.Helpers;

internal static partial class NativeSelectionInterop
{
    internal const uint CommandMessage = 0x8001;
    internal const uint CopyPixels = 0x00CC0020;

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WindowProcedure(nint hwnd, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowClass
    {
        internal uint Size;
        internal uint Style;
        internal nint Procedure;
        internal int ClassExtraBytes;
        internal int WindowExtraBytes;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint BackgroundBrush;
        internal nint MenuName;
        internal nint ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Message
    {
        internal nint Hwnd;
        internal uint Id;
        internal nuint WParam;
        internal nint LParam;
        internal uint Time;
        internal Point Position;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Paint
    {
        internal nint Dc;
        internal int Erase;
        internal Rect Rect;
        internal int Restore;
        internal int IncrementalUpdate;
        internal uint Reserved0;
        internal uint Reserved1;
        internal uint Reserved2;
        internal uint Reserved3;
        internal uint Reserved4;
        internal uint Reserved5;
        internal uint Reserved6;
        internal uint Reserved7;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? moduleName);

    [LibraryImport("kernel32.dll")]
    internal static partial uint GetCurrentThreadId();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetThreadDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClassEx(in WindowClass windowClass);

    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW")]
    internal static partial int UnregisterClass(nint className, nint instance);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial nint CreateWindowEx(uint extendedStyle, nint className, string title, uint style, int x, int y, int width, int height, nint owner, nint menu, nint instance, nint parameter);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    internal static partial int PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    internal static partial int GetMessage(out Message message, nint hwnd, uint minimum, uint maximum);

    [LibraryImport("user32.dll")]
    internal static partial int TranslateMessage(in Message message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    internal static partial nint DispatchMessage(in Message message);

    [LibraryImport("user32.dll")]
    internal static partial void PostQuitMessage(int exitCode);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int ShowWindow(nint hwnd, int command);

    [LibraryImport("user32.dll")]
    internal static partial nint GetWindow(nint hwnd, uint command);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial int UpdateWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int InvalidateRect(nint hwnd, nint rect, int erase);

    [LibraryImport("user32.dll")]
    internal static partial nint BeginPaint(nint hwnd, out Paint paint);

    [LibraryImport("user32.dll")]
    internal static partial int EndPaint(nint hwnd, in Paint paint);

    [LibraryImport("user32.dll", EntryPoint = "LoadCursorW", SetLastError = true)]
    internal static partial nint LoadCursor(nint instance, nint name);

    [LibraryImport("user32.dll")]
    internal static partial nint SetCursor(nint cursor);

    [LibraryImport("user32.dll")]
    internal static partial nint SetCapture(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial nint GetCapture();

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseCapture();

    [LibraryImport("user32.dll")]
    internal static partial nint SetFocus(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial short GetKeyState(int virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int ClientToScreen(nint hwnd, ref Point point);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int GetClientRect(nint hwnd, out Rect rect);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int SetWindowRgn(nint hwnd, nint region, int redraw);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    internal static partial int CombineRgn(nint destination, nint source, nint other, int mode);

    [LibraryImport("gdi32.dll")]
    internal static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    internal static partial int DeleteDC(nint dc);

    [LibraryImport("gdi32.dll")]
    internal static partial nint SelectObject(nint dc, nint value);

    [LibraryImport("gdi32.dll")]
    internal static partial int DeleteObject(nint value);

    [LibraryImport("gdi32.dll")]
    internal static partial int BitBlt(nint target, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint operation);

    [LibraryImport("user32.dll")]
    internal static partial int FrameRect(nint dc, in Rect rect, nint brush);

    [LibraryImport("gdi32.dll")]
    internal static partial nint GetStockObject(int index);
}
