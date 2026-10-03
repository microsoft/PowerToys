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

/// <summary>Adapts the global keyboard hook to Win32 input and message-loop APIs.</summary>
/// <remarks>Periodic renewal is a failsafe because Windows does not report silent hook removal.</remarks>
/// <param name="renewalIntervalMilliseconds">Milliseconds between renewal attempts; defaults to 60 seconds.</param>
internal sealed class GlobalKeyboardHook(uint renewalIntervalMilliseconds = 60_000) : IGlobalKeyboardHook
{
    /// <inheritdoc/>
    public IDisposable Install(HOOKPROC callback)
    {
        var handle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, callback, PInvoke.GetModuleHandle(null), 0);
        if (handle.IsInvalid)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw error;
        }

        return handle;
    }

    /// <inheritdoc/>
    public unsafe void RunMessageLoop(WaitHandle stop, Action renewHook)
    {
        var stopHandle = new HANDLE(stop.SafeWaitHandle.DangerousGetHandle());
        var nextRenewal = Environment.TickCount64 + renewalIntervalMilliseconds;
        while (true)
        {
            var timeout = (uint)Math.Clamp(nextRenewal - Environment.TickCount64, 0, uint.MaxValue);
            var result = PInvoke.MsgWaitForMultipleObjectsEx(
                1,
                &stopHandle,
                timeout,
                QUEUE_STATUS_FLAGS.QS_ALLINPUT,
                MSG_WAIT_FOR_MULTIPLE_OBJECTS_EX_FLAGS.MWMO_INPUTAVAILABLE);
            if (result == WAIT_EVENT.WAIT_OBJECT_0)
            {
                return;
            }

            if (result == WAIT_EVENT.WAIT_FAILED)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (Environment.TickCount64 >= nextRenewal)
            {
                // Renew infrequently as a failsafe for silent hook removal.
                renewHook();
                nextRenewal = Environment.TickCount64 + renewalIntervalMilliseconds;
            }

            while (PInvoke.PeekMessage(out var message, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_REMOVE))
            {
                PInvoke.TranslateMessage(message);
                PInvoke.DispatchMessage(message);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsKeyDown(VIRTUAL_KEY key) => (PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0;

    /// <inheritdoc/>
    public unsafe void SendDummyKeyUp()
    {
        INPUT input = new()
        {
            type = INPUT_TYPE.INPUT_KEYBOARD,
            Anonymous = new()
            {
                ki = new()
                {
                    wVk = (VIRTUAL_KEY)0xFF,
                    dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                },
            },
        };
        _ = PInvoke.SendInput(1, &input, sizeof(INPUT));
    }

    /// <inheritdoc/>
    public LRESULT CallNext(int code, WPARAM wParam, LPARAM lParam) => PInvoke.CallNextHookEx(null, code, wParam, lParam);
}
