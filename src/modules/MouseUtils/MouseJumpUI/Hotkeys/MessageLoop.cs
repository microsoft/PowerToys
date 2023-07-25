// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using MouseJumpUI.NativeMethods;
using static MouseJumpUI.NativeMethods.Core;
using static MouseJumpUI.NativeMethods.User32;

namespace MouseJumpUI.HotKeys;

internal sealed class MessageLoop
{
    public MessageLoop(string name, Func<HWND> hwndCallback)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.HwndCallback = hwndCallback ?? throw new ArgumentNullException(nameof(hwndCallback));

        this.RunningSemaphore = new SemaphoreSlim(1);
        this.CancellationTokenSource = new CancellationTokenSource();
    }

    private string Name
    {
        get;
    }

    /// <summary>
    /// Gets the callback to use to retrieve the hwnd to run the
    /// message loop against. This callback is run in the context
    /// of the message loop thread and can be used to create a hwnd
    /// which will be owned by the message loop thread.
    /// </summary>
    private Func<HWND> HwndCallback
    {
        get;
    }

    public HWND? Hwnd
    {
        get;
        private set;
    }

    /// <summary>
    /// Gets a semaphore that can be waited on until the message loop has stopped.
    /// </summary>
    private SemaphoreSlim RunningSemaphore
    {
        get;
    }

    /// <summary>
    /// Gets a cancellation token that can be used to signal the internal message loop thread to stop.
    /// </summary>
    private CancellationTokenSource CancellationTokenSource
    {
        get;
    }

    private Thread? MessageLoopThread
    {
        get;
        set;
    }

    private DWORD? NativeThreadId
    {
        get;
        set;
    }

    public void Start()
    {
        // make sure we're not already running the internal message loop
        if (!this.RunningSemaphore.Wait(0))
        {
            throw new InvalidOperationException();
        }

        // reset the internal message loop cancellation token
        if (!this.CancellationTokenSource.TryReset())
        {
            throw new InvalidOperationException();
        }

        // start a new internal message loop thread
        this.MessageLoopThread = new Thread(() =>
        {
            this.NativeThreadId = Kernel32.GetCurrentThreadId();
            this.Hwnd = this.HwndCallback.Invoke();
            this.RunMessageLoop();
        })
        {
            Name = this.Name,
            IsBackground = true,
        };
        this.MessageLoopThread.Start();
    }

    private void RunMessageLoop()
    {
        var lpMsg = new LPMSG(
            new MSG(
                hwnd: HWND.Null,
                message: MESSAGE_TYPE.WM_NULL,
                wParam: new(0),
                lParam: new(0),
                time: new(0),
                pt: new(0, 0),
                lPrivate: new(0)));

        // see https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessage
        //     https://learn.microsoft.com/en-us/windows/win32/winmsg/about-messages-and-message-queues
        //     https://devblogs.microsoft.com/oldnewthing/20050405-46/?p=35973
        //     https://devblogs.microsoft.com/oldnewthing/20050406-57/?p=35963
        while (true)
        {
            // check if the cancellation token is signalling that we should stop the message loop
            if (this.CancellationTokenSource.IsCancellationRequested)
            {
                break;
            }

            var hwnd = this.Hwnd ?? throw new InvalidOperationException();
            var result = User32.GetMessageW(
                lpMsg: lpMsg,
                hWnd: hwnd,
                wMsgFilterMin: 0,
                wMsgFilterMax: 0);

            if (result.Value == -1)
            {
                continue;
            }

            var msg = lpMsg.ToStructure();
            if (msg.message == MESSAGE_TYPE.WM_QUIT)
            {
                break;
            }

            _ = User32.TranslateMessage(msg);
            _ = User32.DispatchMessageW(msg);
        }

        // clean up
        this.MessageLoopThread = null;
        this.NativeThreadId = null;

        // the message loop is no longer running
        this.RunningSemaphore.Release(1);
    }

    public void Stop()
    {
        // make sure we're actually running the internal message loop
        if (this.RunningSemaphore.CurrentCount != 0)
        {
            throw new InvalidOperationException();
        }

        // signal to the internal message loop that it should stop
        this.CancellationTokenSource.Cancel();

        // post a null message just in case GetMessageW needs a nudge to stop blocking the
        // message loop - the loop will then notice that we've set the cancellation token,
        // and exit the loop...
        // (see https://devblogs.microsoft.com/oldnewthing/20050405-46/?p=35973)
        var hwnd = this.Hwnd ?? throw new InvalidOperationException();
        var result = User32.PostMessageW(
            hWnd: hwnd,
            Msg: MESSAGE_TYPE.WM_NULL,
            wParam: WPARAM.Null,
            lParam: LPARAM.Null);
        if (result == 0)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.PostThreadMessageW)} failed with result {result}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }

        // wait for the internal message loop to actually stop before we exit
        this.RunningSemaphore.Wait();
    }
}
