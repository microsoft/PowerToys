// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using WinUIEx;

namespace Microsoft.PowerToys.Common.UI.Controls.Window;

/// <summary>
/// Subclasses a window's WndProc and invokes a preprocessor callback for every
/// message before the default window procedure runs. Useful for routing low-level
/// Win32 messages (e.g. <c>WM_HOTKEY</c>) into managed handlers without depending
/// on the WinUI XAML message loop.
/// </summary>
/// <remarks>
/// Usage:
/// <code>
///   _hook = new WindowMessageHook(window, (uMsg, wParam, lParam) =>
///       _hotkeyService.HandleMessage(uMsg, wParam));
/// </code>
/// Dispose to restore the original WndProc.
/// </remarks>
public sealed partial class WindowMessageHook : IDisposable
{
    // Called for every message before default processing. Return true to swallow.
    private readonly Func<uint, nuint, nint, bool> _preProcessor;

    private const int GwlWndProc = -4;

    private readonly nint _hwnd;
    private nint _originalWndProc;
    private WndProcDelegate? _wndProcDelegate;
    private bool _disposed;

    private delegate nint WndProcDelegate(nint hwnd, uint uMsg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowMessageHook"/> class
    /// and subclasses the supplied window's WndProc.
    /// </summary>
    /// <param name="window">Window to subclass.</param>
    /// <param name="preProcessor">Callback invoked for every message before the
    /// default WndProc. Receives <c>(uMsg, wParam, lParam)</c>. Return
    /// <see langword="true"/> to swallow the message.</param>
    public WindowMessageHook(WindowEx window, Func<uint, nuint, nint, bool> preProcessor)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(preProcessor);

        _hwnd = window.GetWindowHandle();
        _preProcessor = preProcessor;
        _wndProcDelegate = WndProc;
        var ptr = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        _originalWndProc = SetWindowLongPtr(_hwnd, GwlWndProc, ptr);
    }

    private nint WndProc(nint hwnd, uint uMsg, nuint wParam, nint lParam)
    {
        if (_preProcessor(uMsg, wParam, lParam))
        {
            return 0;
        }

        return CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_originalWndProc != 0)
        {
            SetWindowLongPtr(_hwnd, GwlWndProc, _originalWndProc);
            _originalWndProc = 0;
        }

        _wndProcDelegate = null;
    }
}
