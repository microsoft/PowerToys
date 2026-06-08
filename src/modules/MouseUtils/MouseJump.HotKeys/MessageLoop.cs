// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Common.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.HotKeys;

internal sealed class MessageLoop
{
    public MessageLoop(string name, Func<Win32Window> windowFactory)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.WindowFactory = windowFactory ?? throw new ArgumentNullException(nameof(windowFactory));

        this.RunningSemaphore = new SemaphoreSlim(1);
        this.CancellationTokenSource = new CancellationTokenSource();
    }

    private string Name
    {
        get;
    }

    /// <summary>
    /// Gets the callback to use to retrieve the Win32Window to run the
    /// message loop against. This callback is run in the context
    /// of the message loop thread and can be used to create a window
    /// which will be owned by the message loop thread.
    /// </summary>
    private Func<Win32Window> WindowFactory
    {
        get;
    }

    public Win32Window? Window
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

    private uint? NativeThreadId
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
            this.NativeThreadId = PInvoke.GetCurrentThreadId();
            this.Window = this.WindowFactory.Invoke();
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
        var msg = new MSG
        {
            hwnd = HWND.Null,
            message = PInvoke.WM_NULL,
            wParam = new(0),
            lParam = new(0),
            time = 0,
            pt = new(0, 0),
        };

        var hwnd = (HWND)(this.Window?.Hwnd ?? throw new InvalidOperationException());

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

            var result = PInvoke.GetMessage(
                lpMsg: out msg,
                hWnd: hwnd,
                wMsgFilterMin: 0,
                wMsgFilterMax: 0);

            if (result.Value == -1)
            {
                continue;
            }

            if (msg.message == PInvoke.WM_QUIT)
            {
                break;
            }

            _ = PInvoke.TranslateMessage(msg);
            _ = PInvoke.DispatchMessage(msg);
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
        var hwnd = (HWND)(this.Window?.Hwnd ?? throw new InvalidOperationException());
        PInvoke.PostMessage(
            hWnd: hwnd,
            Msg: PInvoke.WM_NULL,
            wParam: default,
            lParam: default);

        // wait for the internal message loop to actually stop before we exit
        this.RunningSemaphore.Wait();
    }
}
