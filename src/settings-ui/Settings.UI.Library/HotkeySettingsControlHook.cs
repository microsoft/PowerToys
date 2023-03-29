// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using interop;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public delegate void KeyEvent(int key);

    public delegate bool IsActive();

    public delegate bool FilterAccessibleKeyboardEvents(int key, UIntPtr extraInfo);

    public class HotkeySettingsControlHook : IDisposable
    {
        private const int WmKeyDown = 0x100;
        private const int WmKeyUp = 0x101;
        private const int WmSysKeyDown = 0x0104;
        private const int WmSysKeyUp = 0x0105;

        private KeyboardHook _hook;
        private KeyEvent _keyDown;
        private KeyEvent _keyUp;
        private IsActive _isActive;
        private bool disposedValue;

        private FilterAccessibleKeyboardEvents _filterKeyboardEvent;

        public HotkeySettingsControlHook(KeyEvent keyDown, KeyEvent keyUp, IsActive isActive, FilterAccessibleKeyboardEvents filterAccessibleKeyboardEvents)
        {
            _keyDown = keyDown;
            _keyUp = keyUp;
            _isActive = isActive;
            _filterKeyboardEvent = filterAccessibleKeyboardEvents;
            _hook = new KeyboardHook(HotkeySettingsHookCallback, IsActive, FilterKeyboardEvents);
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

        private bool FilterKeyboardEvents(KeyboardEvent ev)
        {
#pragma warning disable CA2020 // Prevent from behavioral change
            return _filterKeyboardEvent(ev.key, (UIntPtr)ev.dwExtraInfo);
#pragma warning restore CA2020 // Prevent from behavioral change
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // Dispose the KeyboardHook object to terminate the hook threads
                    _hook.Dispose();
                }

                disposedValue = true;
            }
        }

        public bool GetDisposedState() => disposedValue;

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
