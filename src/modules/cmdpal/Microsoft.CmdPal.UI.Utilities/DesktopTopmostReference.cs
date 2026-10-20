// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Utilities;

/// <summary>
/// An invisible, independent desktop window used to test whether ordinary topmost
/// positioning can overtake another window, without querying private window bands.
/// </summary>
internal sealed partial class DesktopTopmostReference : IDisposable
{
    private readonly uint _threadId = PInvoke.GetCurrentThreadId();
    private HWND _window;

    internal nint Handle => (nint)_window;

    internal unsafe DesktopTopmostReference()
    {
        _window = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_NOACTIVATE | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW,
            "STATIC",
            "CmdPal topmost reference",
            WINDOW_STYLE.WS_POPUP,
            0,
            0,
            0,
            0,
            HWND.Null,
            default,
            default,
            null);
        if (_window.IsNull)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create the taskbar z-order reference.");
        }
    }

    internal bool? IsBelowAfterRaise(nint windowHandle)
    {
        VerifyThread();
        ObjectDisposedException.ThrowIf(_window.IsNull, this);
        if (!PInvoke.SetWindowPos(
            _window,
            HWND.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to position the taskbar z-order reference.");
        }

        // Raising never shows the reference or changes the target's position.
        // Null means the two HWNDs could not both be found, not a layer change.
        return TaskbarZOrder.IsAbove(new HWND(windowHandle), _window);
    }

    public void Dispose()
    {
        VerifyThread();
        if (!_window.IsNull)
        {
            if (!PInvoke.DestroyWindow(_window))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to destroy the taskbar z-order reference.");
            }

            _window = HWND.Null;
        }
    }

    private void VerifyThread()
    {
        if (_threadId != PInvoke.GetCurrentThreadId())
        {
            throw new InvalidOperationException("The taskbar z-order reference must be used on its owning thread.");
        }
    }
}
