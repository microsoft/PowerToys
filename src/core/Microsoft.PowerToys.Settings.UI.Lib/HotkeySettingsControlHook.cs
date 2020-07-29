// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using interop;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public delegate void KeyEvent(int key);

    public delegate bool IsActive();

    public class HotkeySettingsControlHook
    {
        private const int WmKeyDown = 0x100;
        private const int WmKeyUp = 0x101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;

        private KeyboardHook _hook;
        private KeyEvent _keyDown;
        private KeyEvent _keyUp;
        private IsActive _isActive;

        public HotkeySettingsControlHook(KeyEvent keyDown, KeyEvent keyUp, IsActive isActive)
        {
            _keyDown = keyDown;
            _keyUp = keyUp;
            _isActive = isActive;
            _hook = new KeyboardHook(HotkeySettingsHookCallback, IsActive, null);
            _hook.Start();
        }

        private bool IsActive()
        {
            return _isActive();
        }

        private void HotkeySettingsHookCallback(KeyboardEvent ev)
        {
            switch (ev.message)
            {
                case WmKeyDown:
                case WmSysKeyDown:
                    _keyDown(ev.key);
                    break;
                case WmKeyUp:
                case WmSysKeyUp:
                    _keyUp(ev.key);
                    break;
            }
        }

        public void Dispose()
        {
            // Dispose the KeyboardHook object to terminate the hook threads
            _hook.Dispose();
        }
    }
}
