using System;
using System.Collections.Generic;
using System.Text;
using interop;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public delegate void KeyEvent(int key);

    public delegate bool IsActive();

    public class HotkeySettingsControlHook
    {
        const int WM_KEYDOWN = 0x100;
        const int WM_KEYUP = 0x101;
        const int WM_SYSKEYDOWN = 0x0104;
        const int WM_SYSKEYUP = 0x0105;

        private KeyboardHook hook;
        private KeyEvent keyDown;
        private KeyEvent keyUp;
        private IsActive isActive;

        public HotkeySettingsControlHook(KeyEvent keyDown, KeyEvent keyUp, IsActive isActive)
        {
            this.keyDown = keyDown;
            this.keyUp = keyUp;
            this.isActive = isActive;
            hook = new KeyboardHook(HotkeySettingsHookCallback, IsActive, null);
            hook.Start();
        }

        private bool IsActive()
        {
            return isActive();
        }

        private void HotkeySettingsHookCallback(KeyboardEvent ev)
        {
            switch (ev.message)
            {
                case WM_KEYDOWN:
                case WM_SYSKEYDOWN:
                    keyDown(ev.key);
                    break;
                case WM_KEYUP:
                case WM_SYSKEYUP:
                    keyUp(ev.key);
                    break;
            }
        }

        public void Dispose()
        {
            // Dispose the KeyboardHook object to terminate the hook threads
            hook.Dispose();
        }
    }
}
