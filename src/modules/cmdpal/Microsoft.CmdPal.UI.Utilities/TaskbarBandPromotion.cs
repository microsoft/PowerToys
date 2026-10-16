// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>
/// Temporarily joins the taskbar's window tree while Explorer promotes it into the shell band.
/// All operations must run on the window's owning thread.
/// </summary>
/// <remarks>
/// Shell foreground events can precede promotion, but this ordering is not guaranteed.
/// A missed transition is left to the normal z-order fallback; cross-band parenting is not retried.
/// The caller must poll <see cref="ShouldRestore"/> and dispose on cancellation or shutdown.
/// The 100 ms budget is checked on the UI thread, not a guarantee if that thread stops pumping.
/// </remarks>
public sealed partial class TaskbarBandPromotion : IDisposable
{
    private const uint DesktopBand = 1;
    private const WINDOW_STYLE ParentStyles = WINDOW_STYLE.WS_CHILD | WINDOW_STYLE.WS_POPUP;

    private readonly HWND _window;
    private readonly HWND _taskbar;
    private readonly uint _threadId;
    private readonly WINDOW_STYLE _style;
    private readonly WINDOW_EX_STYLE _extendedStyle;
    private readonly RECT _bounds;
    private readonly long _started;
    private bool _disposed;

    internal TaskbarBandPromotion(nint windowHandle, nint taskbarHandle)
    {
        _window = new HWND(windowHandle);
        _taskbar = new HWND(taskbarHandle);
        _threadId = PInvoke.GetWindowThreadProcessId(_window, out _);
        VerifyThread();

        if (_window == _taskbar || !PInvoke.IsWindow(_taskbar) || !PInvoke.GetParent(_window).IsNull)
        {
            throw new InvalidOperationException("Taskbar promotion requires an independent window and a valid taskbar.");
        }

        if (!PInvoke.AreDpiAwarenessContextsEqual(
            PInvoke.GetWindowDpiAwarenessContext(_window),
            PInvoke.GetWindowDpiAwarenessContext(_taskbar)))
        {
            throw new InvalidOperationException("Cannot temporarily parent windows with different DPI awareness contexts.");
        }

        if (!PInvoke.GetWindowRect(_window, out _bounds))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to save taskbar window bounds.");
        }

        _style = (WINDOW_STYLE)PInvoke.GetWindowLong(_window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        _extendedStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(_window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        _started = Stopwatch.GetTimestamp();

        try
        {
            SetStyle(WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, (uint)(_extendedStyle | WINDOW_EX_STYLE.WS_EX_NOACTIVATE));
            SetStyle(WINDOW_LONG_PTR_INDEX.GWL_STYLE, (uint)((_style & ~WINDOW_STYLE.WS_POPUP) | WINDOW_STYLE.WS_CHILD));
            SetParent(_taskbar);

            var position = new System.Drawing.Point(_bounds.left, _bounds.top);
            if (!PInvoke.ScreenToClient(_taskbar, ref position))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to map taskbar window bounds.");
            }

            Position(position.X, position.Y);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public bool ShouldRestore
    {
        get
        {
            VerifyThread();
            return _disposed ||
                !PInvoke.IsWindowVisible(_window) ||
                !PInvoke.IsWindowVisible(_taskbar) ||
                ShouldRestoreForBand(ReadBand(_window), Stopwatch.GetElapsedTime(_started));
        }
    }

    public static TaskbarBandPromotion? TryBegin(nint windowHandle, nint taskbarHandle, nint foregroundHandle)
    {
        var window = new HWND(windowHandle);
        var taskbar = new HWND(taskbarHandle);
        var foreground = new HWND(foregroundHandle);
        if (foreground != PInvoke.GetForegroundWindow() ||
            !PInvoke.IsWindowVisible(window) ||
            !PInvoke.IsWindowVisible(taskbar) ||
            !PInvoke.IsWindowVisible(foreground) ||
            !PInvoke.GetParent(window).IsNull ||
            !IsShellSurface(foreground) ||
            ReadBand(window) != DesktopBand ||
            ReadBand(taskbar) != DesktopBand)
        {
            return null;
        }

        try
        {
            return new TaskbarBandPromotion(windowHandle, taskbarHandle);
        }
        catch (Win32Exception ex) when (
            ex.NativeErrorCode == 87 &&
            PInvoke.IsWindow(taskbar) &&
            PInvoke.GetParent(window).IsNull &&
            ReadBand(taskbar) != DesktopBand)
        {
            // Explorer won the race. Construction rolled the window back; do not
            // retry against the already-promoted taskbar.
            return null;
        }
    }

    internal static bool ShouldRestoreForBand(uint band, TimeSpan elapsed) =>
        band != DesktopBand || elapsed >= TimeSpan.FromMilliseconds(100);

    // Notification, app-chrome and Start bands; the shell surface's band need
    // not be the band Explorer subsequently assigns to the taskbar.
    internal static bool IsShellSurface(ReadOnlySpan<char> className, uint band) =>
        band is 4 or 5 or 6 &&
        (className.SequenceEqual("Windows.UI.Core.CoreWindow") ||
         className.SequenceEqual("XamlExplorerHostIslandWindow") ||
         className.SequenceEqual("ControlCenterWindow"));

    private static bool IsShellSurface(HWND window)
    {
        Span<char> name = stackalloc char[128];
        var length = PInvoke.GetClassName(window, name);
        var className = name[..length];
        return length > 0 && IsShellSurface(className, ReadBand(window));
    }

    private static uint ReadBand(HWND window)
    {
        if (!GetWindowBand((nint)window, out var band))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to read taskbar window band.");
        }

        return band;
    }

    public void Dispose()
    {
        VerifyThread();
        if (_disposed)
        {
            return;
        }

        if (PInvoke.IsWindow(_window))
        {
            if (!PInvoke.GetParent(_window).IsNull)
            {
                SetParent(HWND.Null);
            }

            // Preserve visibility and any unrelated style changes made while attached.
            var style = (WINDOW_STYLE)PInvoke.GetWindowLong(_window, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            SetStyle(WINDOW_LONG_PTR_INDEX.GWL_STYLE, (uint)((style & ~ParentStyles) | (_style & ParentStyles)));
            var extendedStyle = (WINDOW_EX_STYLE)PInvoke.GetWindowLong(_window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            SetStyle(
                WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
                (uint)((extendedStyle & ~WINDOW_EX_STYLE.WS_EX_NOACTIVATE) | (_extendedStyle & WINDOW_EX_STYLE.WS_EX_NOACTIVATE)));
            Position(_bounds.left, _bounds.top);
        }

        _disposed = true;
    }

    private void SetParent(HWND parent)
    {
        PInvoke.SetLastError(0);
        var previous = PInvoke.SetParent(_window, parent);
        var error = Marshal.GetLastWin32Error();
        if (previous.IsNull && error != 0)
        {
            throw new Win32Exception(error, "Failed to change the taskbar window parent.");
        }
    }

    private void SetStyle(WINDOW_LONG_PTR_INDEX index, uint style)
    {
        PInvoke.SetLastError(0);
        var previous = PInvoke.SetWindowLong(_window, index, unchecked((int)style));
        var error = Marshal.GetLastWin32Error();
        if (previous == 0 && error != 0)
        {
            throw new Win32Exception(error, "Failed to change taskbar window styles.");
        }
    }

    private void Position(int x, int y)
    {
        if (!PInvoke.SetWindowPos(
            _window,
            HWND.Null,
            x,
            y,
            _bounds.Width,
            _bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to restore taskbar window geometry.");
        }
    }

    private void VerifyThread()
    {
        if (_threadId == 0 || _threadId != PInvoke.GetCurrentThreadId())
        {
            throw new InvalidOperationException("Taskbar promotion must run on the window's owning thread.");
        }
    }

    // Read-only, undocumented user32 API. No privileged SetWindowBand call is used.
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowBand(nint hwnd, out uint band);
}
