// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesMonitorIdentifier
{
    private const string WindowClassName = "PowerToys_FancyZones_MonitorIdentify";

    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsPopup = 0x80000000;

    private const uint WmDestroy = 0x0002;
    private const uint WmPaint = 0x000F;
    private const uint WmTimer = 0x0113;

    private const uint CsVRedraw = 0x0001;
    private const uint CsHRedraw = 0x0002;

    private const uint LwaAlpha = 0x02;

    private const int SwShowNoActivate = 4;

    private const int Transparent = 1;

    private const uint DtCenter = 0x00000001;
    private const uint DtVCenter = 0x00000004;
    private const uint DtSingleLine = 0x00000020;

    private static readonly object Sync = new();
    private static bool classRegistered;

    private static GCHandle? currentPinnedTextHandle;

    public static void Show(int left, int top, int width, int height, string text, int durationMs = 1200)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            text = "Monitor";
        }

        var thread = new Thread(() => RunWindow(left, top, width, height, text, durationMs))
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static unsafe void RunWindow(int left, int top, int width, int height, string text, int durationMs)
    {
        EnsureClassRegistered();

        var overlayWidth = Math.Clamp(width / 4, 220, 420);
        var overlayHeight = Math.Clamp(height / 6, 120, 240);

        var x = left + ((width - overlayWidth) / 2);
        var y = top + ((height - overlayHeight) / 2);

        lock (Sync)
        {
            currentPinnedTextHandle?.Free();
            currentPinnedTextHandle = GCHandle.Alloc(text, GCHandleType.Pinned);
        }

        var hwnd = CreateWindowExW(
            WsExToolWindow | WsExTopmost | WsExLayered | WsExTransparent,
            WindowClassName,
            "MonitorIdentify",
            WsPopup,
            x,
            y,
            overlayWidth,
            overlayHeight,
            nint.Zero,
            nint.Zero,
            GetModuleHandleW(null),
            nint.Zero);

        if (hwnd == nint.Zero)
        {
            return;
        }

        _ = SetLayeredWindowAttributes(hwnd, 0, 235, LwaAlpha);
        _ = ShowWindow(hwnd, SwShowNoActivate);
        _ = UpdateWindow(hwnd);

        _ = SetTimer(hwnd, 1, (uint)durationMs, nint.Zero);

        MSG msg;
        while (GetMessageW(out msg, nint.Zero, 0, 0) != 0)
        {
            _ = TranslateMessage(in msg);
            _ = DispatchMessageW(in msg);
        }

        lock (Sync)
        {
            currentPinnedTextHandle?.Free();
            currentPinnedTextHandle = null;
        }
    }

    private static unsafe void EnsureClassRegistered()
    {
        lock (Sync)
        {
            if (classRegistered)
            {
                return;
            }

            fixed (char* className = WindowClassName ?? string.Empty)
            {
                var wc = new WNDCLASSEXW
                {
                    CbSize = (uint)sizeof(WNDCLASSEXW),
                    Style = CsHRedraw | CsVRedraw,
                    LpfnWndProc = &WndProc,
                    HInstance = GetModuleHandleW(null),
                    HCursor = LoadCursorW(nint.Zero, new IntPtr(32512)), // IDC_ARROW
                    LpszClassName = className,
                };

                _ = RegisterClassExW(in wc);
                classRegistered = true;
            }
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe nint WndProc(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case WmTimer:
                _ = KillTimer(hwnd, 1);
                _ = DestroyWindow(hwnd);
                return nint.Zero;

            case WmDestroy:
                PostQuitMessage(0);
                return nint.Zero;

            case WmPaint:
                {
                    var hdc = BeginPaint(hwnd, out var ps);

                    _ = GetClientRect(hwnd, out var rect);

                    var bgBrush = CreateSolidBrush(0x202020);
                    _ = FillRect(hdc, in rect, bgBrush);

                    _ = SetBkMode(hdc, Transparent);
                    _ = SetTextColor(hdc, 0xFFFFFF);

                    var font = CreateFontW(
                        52,
                        0,
                        0,
                        0,
                        700,
                        0,
                        0,
                        0,
                        1, // DEFAULT_CHARSET
                        0, // OUT_DEFAULT_PRECIS
                        0, // CLIP_DEFAULT_PRECIS
                        5, // CLEARTYPE_QUALITY
                        0x20, // FF_SWISS
                        "Segoe UI");

                    var oldFont = SelectObject(hdc, font);

                    var textPtr = GetPinnedTextPointer();
                    if (textPtr is not null)
                    {
                        var textNint = (nint)textPtr;
                        _ = DrawTextW(hdc, textNint, -1, ref rect, DtCenter | DtVCenter | DtSingleLine);
                    }

                    _ = SelectObject(hdc, oldFont);
                    _ = DeleteObject(font);
                    _ = DeleteObject(bgBrush);

                    _ = EndPaint(hwnd, ref ps);
                    return nint.Zero;
                }
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static unsafe char* GetPinnedTextPointer()
    {
        lock (Sync)
        {
            if (!currentPinnedTextHandle.HasValue || !currentPinnedTextHandle.Value.IsAllocated)
            {
                return null;
            }

            return (char*)currentPinnedTextHandle.Value.AddrOfPinnedObject();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct WNDCLASSEXW
    {
        public uint CbSize;
        public uint Style;
        public delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint> LpfnWndProc;
        public int CbClsExtra;
        public int CbWndExtra;
        public nint HInstance;
        public nint HIcon;
        public nint HCursor;
        public nint HbrBackground;
        public char* LpszMenuName;
        public char* LpszClassName;
        public nint HIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public POINT Pt;
        public uint LPrivate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct PAINTSTRUCT
    {
        public nint Hdc;
        public int FErase;
        public RECT RcPaint;
        public int FRestore;
        public int FIncUpdate;
        public fixed byte RgbReserved[32];
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UpdateWindow(nint hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool KillTimer(nint hWnd, nuint uIDEvent);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nuint SetTimer(nint hWnd, nuint nIDEvent, uint uElapse, nint lpTimerFunc);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(in MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessageW(in MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(in WNDCLASSEXW lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint LoadCursorW(nint hInstance, nint lpCursorName);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern int FillRect(nint hDC, in RECT lprc, nint hbr);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawTextW(nint hdc, nint lpchText, int cchText, ref RECT lprc, uint format);

    [DllImport("gdi32.dll")]
    private static extern nint CreateSolidBrush(uint colorRef);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(nint hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(nint hdc, uint colorRef);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateFontW(
        int nHeight,
        int nWidth,
        int nEscapement,
        int nOrientation,
        int fnWeight,
        uint fdwItalic,
        uint fdwUnderline,
        uint fdwStrikeOut,
        uint fdwCharSet,
        uint fdwOutputPrecision,
        uint fdwClipPrecision,
        uint fdwQuality,
        uint fdwPitchAndFamily,
        string lpszFace);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint hdc, nint hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}
