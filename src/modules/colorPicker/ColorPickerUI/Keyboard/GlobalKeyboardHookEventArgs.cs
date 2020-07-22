using System.ComponentModel;
using static ColorPicker.Win32Apis;

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
