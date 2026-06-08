// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Common.Helpers;
using MouseJump.Common.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MouseJump.HotKeys;

/// <remarks>
/// See https://stackoverflow.com/a/3654821/3156906
///     https://learn.microsoft.com/en-us/archive/msdn-magazine/2007/june/net-matters-handling-messages-in-console-apps
///     https://www.codeproject.com/Articles/5274425/Understanding-Windows-Message-Queues-for-the-Cshar
/// </remarks>
public sealed class HotKeyManager
{
    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public HotKeyManager()
    {
        this.MessageSemaphore = new(0, 1);
        this.MessageLoop = new MessageLoop(
            name: "FancyMouseHotKeyLoop",
            windowFactory: () =>
            {
                this.Window ??= Win32Helper.CreateMessageOnlyWindow(
                    "FancyMouseHotKeyClass", "FancyMouseHotKeyWindow", this.WindowProc);
                return this.Window;
            });
        this.MessageLoop.Start();
    }

    private Win32Window? Window
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

    private HWND GetHwndOrThrow()
    {
        return (HWND)(this.Window?.Hwnd ?? throw new InvalidOperationException());
    }

    public void SetHoKey(Keystroke? hotKey)
    {
        var hwnd = new Lazy<HWND>(() => this.GetHwndOrThrow());

        // do we need to unregister the existing hotkey first?
        if (this.HotKey is not null)
        {
            var result = PInvoke.PostMessage(hwnd.Value, HotKeyHelper.WM_PRIV_UNREGISTER_HOTKEY, default, default);
            ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.PostMessage));
            this.MessageSemaphore.Wait();
        }

        this.HotKey = hotKey;

        // register the new hotkey
        if (this.HotKey is not null)
        {
            var result = PInvoke.PostMessage(hwnd.Value, HotKeyHelper.WM_PRIV_REGISTER_HOTKEY, default, default);
            ResultHandler.ThrowIfZero(result, getLastError: true, nameof(PInvoke.PostMessage));
            this.MessageSemaphore.Wait();
        }
    }

    private nint WindowProc(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case PInvoke.WM_HOTKEY:
            {
                // https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
                // https://stackoverflow.com/a/47831305/3156906
                var key = (Keys)((lParam & 0xffff0000) >> 16);
                var modifiers = (KeyModifiers)(lParam & 0x0000ffff);
                var e = new HotKeyEventArgs(key, modifiers);
                this.OnHotKeyPressed(e);
                break;
            }

            case HotKeyHelper.WM_PRIV_REGISTER_HOTKEY:
            {
                var result1 = PInvoke.RegisterHotKey(
                    hWnd: (HWND)hWnd,
                    id: 1,
                    fsModifiers: (HOT_KEY_MODIFIERS)(this.HotKey ?? throw new InvalidOperationException()).Modifiers,
                    vk: (uint)this.HotKey.Key);
                ResultHandler.ThrowIfZero(result1, getLastError: true, nameof(PInvoke.RegisterHotKey));
                this.MessageSemaphore.Release();
                break;
            }

            case HotKeyHelper.WM_PRIV_UNREGISTER_HOTKEY:
            {
                var result1 = PInvoke.UnregisterHotKey(
                    hWnd: (HWND)hWnd,
                    id: 1);
                ResultHandler.ThrowIfZero(result1, getLastError: true, nameof(PInvoke.UnregisterHotKey));
                this.MessageSemaphore.Release();
                break;
            }
        }

        var result = PInvoke.DefWindowProc((HWND)hWnd, msg, wParam, lParam);
        return result;
    }

    private void OnHotKeyPressed(HotKeyEventArgs e)
    {
        this.HotKeyPressed?.Invoke(null, e);
    }
}
