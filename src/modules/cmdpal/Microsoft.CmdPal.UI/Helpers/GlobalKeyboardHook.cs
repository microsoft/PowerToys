// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class GlobalKeyboardHook : IGlobalKeyboardHook
{
    public IDisposable Install(HOOKPROC callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return HookThread.Start(callback);
    }

    public bool IsKeyDown(VIRTUAL_KEY key) => (PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0;

    public unsafe void SendDummyKeyUp()
    {
        var input = CreateKeyUpInput((VIRTUAL_KEY)0xFF);
        _ = PInvoke.SendInput(1, &input, sizeof(INPUT));
    }

    public LRESULT CallNext(int code, WPARAM wParam, LPARAM lParam) => PInvoke.CallNextHookEx(null, code, wParam, lParam);

    public unsafe void SendWindowsKeyUp(VIRTUAL_KEY key)
    {
        // Replace the physical release with a masked release. Swallowing it alone
        // would leave Windows believing the modifier is still held.
        INPUT* inputs = stackalloc INPUT[2]
        {
            CreateKeyUpInput((VIRTUAL_KEY)0xFF),
            CreateKeyUpInput(key),
        };
        if (PInvoke.SendInput(2, inputs, sizeof(INPUT)) != 2)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    internal static INPUT CreateKeyUpInput(VIRTUAL_KEY key) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new()
        {
            ki = new()
            {
                wVk = key,
                dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP |
                    (key is VIRTUAL_KEY.VK_LWIN or VIRTUAL_KEY.VK_RWIN ? KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY : 0),
            },
        },
    };

    // Windows calls a low-level hook on the thread that installed it, and stalls all keyboard
    // input until that thread returns. A thread that does nothing but service the hook keeps
    // input responsive while the UI thread is busy.
    private sealed partial class HookThread : IDisposable
    {
        private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(2);

        private readonly HOOKPROC _callback;
        private readonly Thread _thread;
        private readonly TaskCompletionSource<Exception?> _installed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private uint _threadId;
        private int _disposed;

        private HookThread(HOOKPROC callback)
        {
            _callback = callback;
            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "CmdPal keyboard hook",
                Priority = ThreadPriority.Highest,
            };
        }

        public static HookThread Start(HOOKPROC callback)
        {
            var hookThread = new HookThread(callback);
            hookThread._thread.Start();
            if (hookThread._installed.Task.GetAwaiter().GetResult() is { } error)
            {
                hookThread._thread.Join();
                throw error;
            }

            return hookThread;
        }

        private void Run()
        {
            // Create the message queue before reporting readiness so Dispose can post WM_QUIT.
            _ = PInvoke.PeekMessage(out _, HWND.Null, PInvoke.WM_USER, PInvoke.WM_USER, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
            _threadId = PInvoke.GetCurrentThreadId();

            var handle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _callback, PInvoke.GetModuleHandle(null), 0);
            if (handle.IsInvalid)
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                handle.Dispose();
                _installed.SetResult(error);
                return;
            }

            _installed.SetResult(null);
            using (handle)
            {
                while (PInvoke.GetMessage(out var msg, HWND.Null, 0, 0).Value > 0)
                {
                    PInvoke.DispatchMessage(msg);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _ = PInvoke.PostThreadMessage(_threadId, PInvoke.WM_QUIT, default, default);
            if (Thread.CurrentThread != _thread)
            {
                _thread.Join(ExitTimeout);
            }
        }
    }
}
