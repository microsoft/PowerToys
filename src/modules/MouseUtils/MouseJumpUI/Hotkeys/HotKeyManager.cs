// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using MouseJumpUI.NativeMethods;
using static MouseJumpUI.NativeMethods.Core;
using static MouseJumpUI.NativeMethods.User32;

namespace MouseJumpUI.HotKeys;

/// <remarks>
/// See https://stackoverflow.com/a/3654821/3156906
///     https://learn.microsoft.com/en-us/archive/msdn-magazine/2007/june/net-matters-handling-messages-in-console-apps
/// </remarks>
public sealed class HotKeyManager
{
    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public HotKeyManager()
    {
        // cache the window proc delegate so it doesn't get garbage-collected
        this.WndProc = this.WindowProc;

        this.MessageSemaphore = new(0, 1);
        this.MessageLoop = new MessageLoop(
            name: "MouseJumpMessageLoop",
            hwndCallback: () =>
            {
                if (this.Hwnd.IsNull)
                {
                    (this.WndClass, this.Hwnd) = HotKeyHelper.CreateWindow(this.WndProc);
                }

                return this.Hwnd;
            });
        this.MessageLoop.Start();
    }

    private WNDPROC WndProc
    {
        get;
        set;
    }

    private ATOM? WndClass
    {
        get;
        set;
    }

    private HWND Hwnd
    {
        get;
        set;
    }

    private MessageLoop MessageLoop
    {
        get;
    }

    public Keystroke? HotKey
    {
        get;
        private set;
    }

    private SemaphoreSlim MessageSemaphore
    {
        get;
    }

    public void SetHoKey(Keystroke? hotKey)
    {
        var hwnd = this.MessageLoop.Hwnd;

        // do we need to unregister the existing hotkey first?
        if ((this.HotKey is not null) && hwnd.HasValue)
        {
            HotKeyHelper.PostPrivateMessage(hwnd.Value, HotKeyHelper.WM_PRIV_UNREGISTER_HOTKEY);
            this.MessageSemaphore.Wait();
        }

        this.HotKey = hotKey;

        // register the new hotkey
        if ((this.HotKey is not null) && hwnd.HasValue)
        {
            HotKeyHelper.PostPrivateMessage(hwnd.Value, HotKeyHelper.WM_PRIV_REGISTER_HOTKEY);
            this.MessageSemaphore.Wait();
        }
    }

    private LRESULT WindowProc(HWND hWnd, MESSAGE_TYPE msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case MESSAGE_TYPE.WM_HOTKEY:
            {
                // https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
                // https://stackoverflow.com/a/47831305/3156906
                var param = (uint)lParam.Value.ToInt64();
                var key = (Keys)((param & 0xffff0000) >> 16);
                var modifiers = (KeyModifiers)(param & 0x0000ffff);
                var e = new HotKeyEventArgs(key, modifiers);
                this.OnHotKeyPressed(e);
                break;
            }

            case (MESSAGE_TYPE)HotKeyHelper.WM_PRIV_REGISTER_HOTKEY:
            {
                var hwnd = this.MessageLoop.Hwnd ?? throw new InvalidOperationException();
                HotKeyHelper.RegisterHotKey(hwnd, this.HotKey!, 1);
                this.MessageSemaphore.Release();
                break;
            }

            case (MESSAGE_TYPE)HotKeyHelper.WM_PRIV_UNREGISTER_HOTKEY:
            {
                var hwnd = this.MessageLoop.Hwnd ?? throw new InvalidOperationException();
                HotKeyHelper.UnregisterHotKey(hwnd, 1);
                this.MessageSemaphore.Release();
                break;
            }
        }

        {
            var result = User32.DefWindowProcW(hWnd, msg, wParam, lParam);
            return result;
        }
    }

    private void OnHotKeyPressed(HotKeyEventArgs e)
    {
        this.HotKeyPressed?.Invoke(null, e);
    }
}
