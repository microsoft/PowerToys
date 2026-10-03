// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

internal interface IGlobalKeyboardHook
{
    IDisposable Install(HOOKPROC callback);

    bool IsKeyDown(VIRTUAL_KEY key);

    void SendDummyKeyUp();

    LRESULT CallNext(int code, WPARAM wParam, LPARAM lParam);
}
