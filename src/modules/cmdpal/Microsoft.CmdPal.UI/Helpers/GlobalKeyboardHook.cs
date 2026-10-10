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

internal sealed class GlobalKeyboardHook : IGlobalKeyboardHook
{
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

    public bool IsKeyDown(VIRTUAL_KEY key) => (PInvoke.GetAsyncKeyState((int)key) & 0x8000) != 0;

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

    public LRESULT CallNext(int code, WPARAM wParam, LPARAM lParam) => PInvoke.CallNextHookEx(null, code, wParam, lParam);
}
