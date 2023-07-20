// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using MouseJumpUI.HotKeys;
using MouseJumpUI.NativeMethods;
using static MouseJumpUI.NativeMethods.Core;
using static MouseJumpUI.NativeMethods.User32;

namespace MouseJumpUI.HotKeys;

/// <remarks>
/// See https://stackoverflow.com/a/3654821/3156906
///     https://learn.microsoft.com/en-us/archive/msdn-magazine/2007/june/net-matters-handling-messages-in-console-apps
///     https://www.codeproject.com/Articles/5274425/Understanding-Windows-Message-Queues-for-the-Cshar
/// </remarks>
public sealed class HotKeyManager
{
    private int _id;

    public event EventHandler<HotKeyEventArgs>? HotKeyPressed;

    public HotKeyManager(Keystroke hotkey)
    {
        this.HotKey = hotkey ?? throw new ArgumentNullException(nameof(hotkey));

        // cache the window proc delegate so it doesn't get garbage-collected
        this.WndProc = this.WindowProc;
        this.HWnd = HWND.Null;
    }

    public Keystroke HotKey
    {
        get;
    }

    private WNDPROC WndProc
    {
        get;
    }

    private HWND HWnd
    {
        get;
        set;
    }

    private MessageLoop? MessageLoop
    {
        get;
        set;
    }

    private LRESULT WindowProc(HWND hWnd, MESSAGE_TYPE msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case MESSAGE_TYPE.WM_HOTKEY:
                // https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey
                // https://stackoverflow.com/a/47831305/3156906
                var param = (uint)lParam.Value.ToInt64();
                var key = (Keys)((param & 0xffff0000) >> 16);
                var modifiers = (KeyModifiers)(param & 0x0000ffff);
                var e = new HotKeyEventArgs(key, modifiers);
                this.OnHotKeyPressed(e);
                break;
        }

        return User32.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    public void Start()
    {
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/using-messages-and-message-queues
        var hInstance = (HINSTANCE)Process.GetCurrentProcess().Handle;

        // see https://stackoverflow.com/a/30992796/3156906
        var wndClass = new WNDCLASSEXW(
            cbSize: (uint)Marshal.SizeOf(typeof(WNDCLASSEXW)),
            style: 0,
            lpfnWndProc: this.WndProc,
            cbClsExtra: 0,
            cbWndExtra: 0,
            hInstance: hInstance,
            hIcon: HICON.Null,
            hCursor: HCURSOR.Null,
            hbrBackground: HBRUSH.Null,
            lpszMenuName: PCWSTR.Null,
            lpszClassName: "MouseJumpMessageClass",
            hIconSm: HICON.Null);

        // wndClassAtom
        var atom = User32.RegisterClassExW(
            unnamedParam1: wndClass);
        if (atom.Value == 0)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.RegisterClassExW)} failed with result {atom}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }

        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#message-only-windows
        //     https://devblogs.microsoft.com/oldnewthing/20171218-00/?p=97595
        //     https://stackoverflow.com/a/30992796/3156906
        this.HWnd = User32.CreateWindowExW(
            dwExStyle: 0,
            lpClassName: "MouseJumpMessageClass",
            lpWindowName: "MouseJumpMessageWindow",
            dwStyle: 0,
            x: 0,
            y: 0,
            nWidth: 300,
            nHeight: 400,
            hWndParent: HWND.HWND_MESSAGE, // message-only window
            hMenu: HMENU.Null,
            hInstance: hInstance,
            lpParam: LPVOID.Null);
        if (this.HWnd.IsNull)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.CreateWindowExW)} failed with result {this.HWnd}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }

        this.MessageLoop = new MessageLoop(
            name: "MouseJumpMessageLoop");

        this.MessageLoop.Start();

        var result = User32.RegisterHotKey(
            hWnd: this.HWnd,
            id: Interlocked.Increment(ref _id),
            fsModifiers: (HOT_KEY_MODIFIERS)this.HotKey.Modifiers,
            vk: (uint)this.HotKey.Key);
        if (!result)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.RegisterHotKey)} failed with result {result}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }
    }

    public void Stop()
    {
        var result = User32.UnregisterHotKey(
            hWnd: this.HWnd,
            id: this._id);
        if (!result)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.UnregisterHotKey)} failed with result {result}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }

        this.MessageLoop?.Stop();
    }

    private void OnHotKeyPressed(HotKeyEventArgs e)
    {
        this.HotKeyPressed?.Invoke(null, e);
    }
}
