// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>Provides native operations for the global low-level keyboard hook.</summary>
/// <remarks>Install, pump, and dispose the hook on the same thread. Callback operations must not block.</remarks>
internal interface IGlobalKeyboardHook
{
    /// <summary>Installs the callback on the current thread.</summary>
    /// <param name="callback">Callback that the caller must keep alive until the registration is disposed.</param>
    /// <returns>A registration whose disposal removes the hook.</returns>
    IDisposable Install(HOOKPROC callback);

    /// <summary>Processes hook messages until the stop signal is set.</summary>
    /// <param name="stop">Signal that ends the message loop.</param>
    /// <param name="renewHook">Periodic action that replaces the hook, retaining the current registration on failure.</param>
    /// <remarks>Renewal runs on the hook thread. Unhandled exceptions terminate the loop.</remarks>
    void RunMessageLoop(WaitHandle stop, Action renewHook);

    /// <summary>Checks the current asynchronous state of a virtual key.</summary>
    /// <param name="key">Virtual-key code to query.</param>
    /// <returns>Whether the key is down.</returns>
    bool IsKeyDown(VIRTUAL_KEY key);

    /// <summary>Injects a dummy key-up to mask modifier-only actions and preserve foreground activation.</summary>
    /// <remarks>Required for every matched shortcut, regardless of its modifiers.</remarks>
    void SendDummyKeyUp();

    /// <summary>Forwards an unhandled event to the next hook.</summary>
    /// <param name="code">Hook code supplied by Windows.</param>
    /// <param name="wParam">Keyboard message identifier.</param>
    /// <param name="lParam">Pointer to the keyboard event data.</param>
    /// <returns>The result from the next hook.</returns>
    LRESULT CallNext(int code, WPARAM wParam, LPARAM lParam);
}
