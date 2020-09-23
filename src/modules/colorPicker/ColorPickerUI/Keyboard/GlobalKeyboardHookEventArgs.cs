// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using static ColorPicker.NativeMethods;

namespace ColorPicker.Keyboard
{
    internal class GlobalKeyboardHookEventArgs : HandledEventArgs
    {
        internal GlobalKeyboardHook.KeyboardState KeyboardState { get; private set; }

        internal LowLevelKeyboardInputEvent KeyboardData { get; private set; }

        internal GlobalKeyboardHookEventArgs(
            LowLevelKeyboardInputEvent keyboardData,
            GlobalKeyboardHook.KeyboardState keyboardState)
        {
            KeyboardData = keyboardData;
            KeyboardState = keyboardState;
        }
    }
}
