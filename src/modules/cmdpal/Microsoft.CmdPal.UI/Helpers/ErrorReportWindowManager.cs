// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using WinRT.Interop;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Manages a single instance of ErrorReportWindow.
/// You can create multiple managers for different error contexts.
/// Thread-safe and designed for reliability.
/// </summary>
internal sealed class ErrorReportWindowManager
{
    private readonly Lock _lock = new();
    private readonly DispatcherQueue? _dispatcher;
    private readonly Action<ErrorReportWindow>? _onWindowClosed;
    private readonly string _managerId;
    private ErrorReportWindow? _window;

    /// <summary>
    /// Creates a new manager instance
    /// </summary>
    /// <param name="managerId">Optional ID for debugging/logging</param>
    /// <param name="dispatcher">Optional dispatcher for UI thread marshaling</param>
    /// <param name="onWindowClosed">Optional callback when window closes</param>
    public ErrorReportWindowManager(
        string? managerId = null,
        DispatcherQueue? dispatcher = null,
        Action<ErrorReportWindow>? onWindowClosed = null)
    {
        _managerId = managerId ?? Guid.NewGuid().ToString();
        _dispatcher = dispatcher ?? DispatcherQueue.GetForCurrentThread();
        _onWindowClosed = onWindowClosed;
    }

    /// <summary>
    /// Shows the error window or activates existing one.
    /// Thread-safe, can be called from any thread.
    /// </summary>
    public bool Show(ErrorReportWindow.Options options)
    {
        try
        {
            // Check if we need to marshal to UI thread
            var currentDispatcher = DispatcherQueue.GetForCurrentThread();

            if (currentDispatcher == null && _dispatcher != null)
            {
                // We're on a background thread, marshal to UI thread
                return ShowOnUIThread(options);
            }

            // We're on a UI thread, show directly
            return ShowDirectly(options);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the current window if it exists and is alive
    /// </summary>
    public ErrorReportWindow? CurrentWindow
    {
        get
        {
            lock (_lock)
            {
                return _window != null && IsAlive(_window) ? _window : null;
            }
        }
    }

    private bool ShowOnUIThread(ErrorReportWindow.Options options)
    {
        var resetEvent = new ManualResetEventSlim(false);
        var result = false;

        _dispatcher!.TryEnqueue(() =>
        {
            try
            {
                result = ShowDirectly(options);
            }
            finally
            {
                resetEvent.Set();
            }
        });

        // Wait up to 5 seconds for UI thread
        resetEvent.Wait(5000);
        return result;
    }

    private bool ShowDirectly(ErrorReportWindow.Options options)
    {
        lock (_lock)
        {
            try
            {
                if (_window != null && IsAlive(_window))
                {
                    BringToFront(_window);
                    return true;
                }

                _window = null;

                var window = new ErrorReportWindow(options);
                _window = window;

                window.Closed += OnWindowClosed;

                window.Activate();
                return true;
            }
            catch
            {
                _window = null;
                return false;
            }
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args)
    {
        var window = sender as ErrorReportWindow;

        lock (_lock)
        {
            if (_window == window)
            {
                _window = null;
            }
        }

        if (window != null)
        {
            window.Closed -= OnWindowClosed;
            _onWindowClosed?.Invoke(window);
        }
    }

    /// <summary>
    /// Brings window to front and activates it
    /// </summary>
    private static void BringToFront(Window window)
    {
        try
        {
            window.Activate();

            var hwnd = new HWND(WindowNative.GetWindowHandle(window));
            if (hwnd != IntPtr.Zero)
            {
                PInvoke.SetForegroundWindow(hwnd);
            }
        }
        catch
        {
            // Ignore any errors while bringing to front
            // This can happen if the window is already closed or invalid
        }
    }

    /// <summary>
    /// Check if this window is still valid/alive
    /// </summary>
    private static bool IsAlive(Window window)
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(window);
            return hwnd != IntPtr.Zero && PInvoke.IsWindow(new HWND(hwnd));
        }
        catch
        {
            return false;
        }
    }

    public override string ToString() => $"{nameof(_managerId)}: {_managerId}, {nameof(CurrentWindow)}: {CurrentWindow}";
}
