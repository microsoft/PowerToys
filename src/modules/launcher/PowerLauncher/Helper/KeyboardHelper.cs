// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Input;
using Wox.Plugin;

namespace PowerLauncher.Helper
{
    internal sealed class KeyboardHelper
    {
        public static SpecialKeyState CheckModifiers()
        {
            SpecialKeyState state = new SpecialKeyState();
            if ((Keyboard.GetKeyStates(Key.LeftShift) & KeyStates.Down) > 0 ||
                (Keyboard.GetKeyStates(Key.RightShift) & KeyStates.Down) > 0)
            {
                state.ShiftPressed = true;
            }

            if ((Keyboard.GetKeyStates(Key.LWin) & KeyStates.Down) > 0 ||
                (Keyboard.GetKeyStates(Key.RWin) & KeyStates.Down) > 0)
            {
                state.WinPressed = true;
            }

            if ((Keyboard.GetKeyStates(Key.LeftCtrl) & KeyStates.Down) > 0 ||
                (Keyboard.GetKeyStates(Key.RightCtrl) & KeyStates.Down) > 0)
            {
                state.CtrlPressed = true;
            }

            if ((Keyboard.GetKeyStates(Key.LeftAlt) & KeyStates.Down) > 0 ||
                (Keyboard.GetKeyStates(Key.RightAlt) & KeyStates.Down) > 0)
            {
                state.AltPressed = true;
            }

            return state;
        }
    }
}
