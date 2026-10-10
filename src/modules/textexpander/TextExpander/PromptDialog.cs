// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// A minimal Win32 input box for {{prompt:Label}}. Hand-rolled so the app avoids a WinForms
/// dependency, which would block trimming and inflate the published binary roughly sixfold.
/// </summary>
internal static class PromptDialog
{
    private const string ClassName = "PowerToys.TextExpander.Prompt";
    private const int IdEdit = 1000;

    private static bool _classRegistered;
    private static Native.WndProc? _wndProcDelegate; // must outlive the class registration

    private static IntPtr _editHandle;
    private static string? _result;
    private static bool _closed;

    /// <summary>Shows the dialog modally. Returns null when cancelled.</summary>
    public static string? Show(string label)
    {
        EnsureClassRegistered();

        _editHandle = IntPtr.Zero;
        _result = null;
        _closed = false;

        IntPtr instance = Native.GetModuleHandleW(null);

        const int clientWidth = 400;
        const int clientHeight = 132;
        const int style = Native.WS_OVERLAPPED | Native.WS_CAPTION | Native.WS_SYSMENU;
        const int exStyle = Native.WS_EX_TOPMOST | Native.WS_EX_DLGMODALFRAME
                          | Native.WS_EX_CONTROLPARENT | Native.WS_EX_TOOLWINDOW;

        var rect = new Native.RECT { Left = 0, Top = 0, Right = clientWidth, Bottom = clientHeight };
        Native.AdjustWindowRectEx(ref rect, style, false, exStyle);
        int windowWidth = rect.Right - rect.Left;
        int windowHeight = rect.Bottom - rect.Top;

        int x = (Native.GetSystemMetrics(Native.SM_CXSCREEN) - windowWidth) / 2;
        int y = (Native.GetSystemMetrics(Native.SM_CYSCREEN) - windowHeight) / 3;

        IntPtr hwnd = Native.CreateWindowExW(exStyle, ClassName, "PowerToys Text Expander", style, x, y, windowWidth, windowHeight, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);

        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        IntPtr font = Native.GetStockObject(Native.DEFAULT_GUI_FONT);

        IntPtr labelHandle = Native.CreateWindowExW(0, "STATIC", label, Native.WS_CHILD | Native.WS_VISIBLE | Native.SS_LEFT, 14, 14, clientWidth - 28, 20, hwnd, IntPtr.Zero, instance, IntPtr.Zero);

        _editHandle = Native.CreateWindowExW(Native.WS_EX_CLIENTEDGE, "EDIT", string.Empty, Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_TABSTOP | Native.ES_AUTOHSCROLL, 14, 40, clientWidth - 28, 26, hwnd, new IntPtr(IdEdit), instance, IntPtr.Zero);

        IntPtr okHandle = Native.CreateWindowExW(0, "BUTTON", "OK", Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_TABSTOP | Native.BS_DEFPUSHBUTTON, clientWidth - 202, 84, 90, 30, hwnd, new IntPtr(Native.IDOK), instance, IntPtr.Zero);

        IntPtr cancelHandle = Native.CreateWindowExW(0, "BUTTON", "Cancel", Native.WS_CHILD | Native.WS_VISIBLE | Native.WS_TABSTOP, clientWidth - 104, 84, 90, 30, hwnd, new IntPtr(Native.IDCANCEL), instance, IntPtr.Zero);

        foreach (IntPtr control in new[] { labelHandle, _editHandle, okHandle, cancelHandle })
        {
            Native.SendMessageW(control, Native.WM_SETFONT, font, new IntPtr(1));
        }

        Native.ShowWindow(hwnd, Native.SW_SHOW);
        WindowFocus.Force(hwnd);
        Native.SetFocus(_editHandle);

        RunModalLoop(hwnd);

        return _result;
    }

    /// <summary>
    /// Pumps messages until the window closes. IsDialogMessageW gives us Tab navigation plus
    /// Enter/Escape mapping to the default and cancel buttons without a dialog template.
    /// </summary>
    private static void RunModalLoop(IntPtr hwnd)
    {
        while (!_closed)
        {
            int result = Native.GetMessageW(out Native.MSG msg, IntPtr.Zero, 0, 0);

            if (result == 0)
            {
                // WM_QUIT. This nested loop must not swallow it, or whichever outer loop is
                // waiting to shut down would spin forever with the hook still installed.
                Native.PostQuitMessage(msg.WParam.ToInt32());
                break;
            }

            if (result < 0)
            {
                break;
            }

            if (Native.IsDialogMessageW(hwnd, ref msg))
            {
                continue;
            }

            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);

            if (!Native.IsWindow(hwnd))
            {
                break;
            }
        }

        _closed = true;

        // Every early exit above bypasses Close(), so the window would otherwise be left
        // on screen with its child controls never destroyed.
        if (Native.IsWindow(hwnd))
        {
            Native.DestroyWindow(hwnd);
        }
    }

    private static void EnsureClassRegistered()
    {
        if (_classRegistered)
        {
            return;
        }

        _wndProcDelegate = PromptWndProc;

        var wc = new Native.WNDCLASSEXW
        {
            CbSize = (uint)Marshal.SizeOf<Native.WNDCLASSEXW>(),
            Style = 0,
            LpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            HInstance = Native.GetModuleHandleW(null),
            HCursor = Native.LoadCursorW(IntPtr.Zero, new IntPtr(Native.IDC_ARROW)),
            HbrBackground = new IntPtr(16), // COLOR_BTNFACE + 1
            LpszClassName = ClassName,
        };

        Native.RegisterClassExW(ref wc);
        _classRegistered = true;
    }

    private static IntPtr PromptWndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Native.WM_COMMAND:
                int id = (int)(wParam.ToInt64() & 0xFFFF);
                if (id == Native.IDOK)
                {
                    _result = ReadEditText();
                    Close(hwnd);
                    return IntPtr.Zero;
                }

                if (id == Native.IDCANCEL)
                {
                    _result = null;
                    Close(hwnd);
                    return IntPtr.Zero;
                }

                break;

            case Native.WM_CLOSE:
                _result = null;
                Close(hwnd);
                return IntPtr.Zero;

            case Native.WM_DESTROY:
                _closed = true;
                return IntPtr.Zero;
        }

        return Native.DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static string ReadEditText()
    {
        if (_editHandle == IntPtr.Zero)
        {
            return string.Empty;
        }

        int length = Native.GetWindowTextLengthW(_editHandle);
        if (length <= 0)
        {
            return string.Empty;
        }

        char[] buffer = new char[length + 1];
        int copied = Native.GetWindowTextW(_editHandle, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(0, copied));
    }

    private static void Close(IntPtr hwnd)
    {
        _closed = true;
        Native.DestroyWindow(hwnd);
    }
}
