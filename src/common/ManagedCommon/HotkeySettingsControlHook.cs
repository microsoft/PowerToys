// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;

using PowerToys.Interop;

namespace ManagedCommon
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
        private const int Disposing = 1;
        private const int Disposed = 2;

        private IDisposable _hook;
        private KeyEvent _keyDown;
        private KeyEvent _keyUp;
        private IsActive _isActive;
        private int _disposeState;

        private FilterAccessibleKeyboardEvents _filterKeyboardEvent;

        public HotkeySettingsControlHook(KeyEvent keyDown, KeyEvent keyUp, IsActive isActive, FilterAccessibleKeyboardEvents filterAccessibleKeyboardEvents)
            : this(keyDown, keyUp, isActive, filterAccessibleKeyboardEvents, CreateHook)
        {
        }

        internal HotkeySettingsControlHook(
            KeyEvent keyDown,
            KeyEvent keyUp,
            IsActive isActive,
            FilterAccessibleKeyboardEvents filterAccessibleKeyboardEvents,
            Func<KeyboardEventCallback, IsActiveCallback, FilterKeyboardEvent, IDisposable> createHook)
        {
            _keyDown = keyDown;
            _keyUp = keyUp;
            _isActive = isActive;
            _filterKeyboardEvent = filterAccessibleKeyboardEvents;
            _hook = createHook(HotkeySettingsHookCallback, IsActive, FilterKeyboardEvents);
        }

        private static IDisposable CreateHook(KeyboardEventCallback keyboardEventCallback, IsActiveCallback isActiveCallback, FilterKeyboardEvent filterKeyboardEvent)
        {
            var hook = new KeyboardHook(keyboardEventCallback, isActiveCallback, filterKeyboardEvent);
            hook.Start();
            return hook;
        }

        private bool IsActive()
        {
            var hook = _hook;
            try
            {
                var isActive = _isActive;
                return Volatile.Read(ref _disposeState) == 0 && (isActive?.Invoke() ?? false) && Volatile.Read(ref _disposeState) == 0;
            }
            finally
            {
                // A callback can dispose its owner while native dispatch is still on the stack.
                GC.KeepAlive(hook);
            }
        }

        private void HotkeySettingsHookCallback(KeyboardEvent ev)
        {
            var hook = _hook;
            try
            {
                if (Volatile.Read(ref _disposeState) != 0)
                {
                    return;
                }

                switch (ev.message)
                {
                    case WmKeyDown:
                    case WmSysKeyDown:
                        _keyDown?.Invoke(ev.key);
                        break;
                    case WmKeyUp:
                    case WmSysKeyUp:
                        _keyUp?.Invoke(ev.key);
                        break;
                }
            }
            finally
            {
                GC.KeepAlive(hook);
            }
        }

        private bool FilterKeyboardEvents(KeyboardEvent ev)
        {
            var hook = _hook;
            try
            {
                var filter = _filterKeyboardEvent;
#pragma warning disable CA2020 // Prevent from behavioral change
                return Volatile.Read(ref _disposeState) == 0 && (filter?.Invoke(ev.key, (UIntPtr)ev.dwExtraInfo) ?? false) && Volatile.Read(ref _disposeState) == 0;
#pragma warning restore CA2020 // Prevent from behavioral change
            }
            finally
            {
                GC.KeepAlive(hook);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposeState, Disposing, 0) != 0)
            {
                return;
            }

            var disposed = false;
            try
            {
                if (disposing)
                {
                    _hook.Dispose();

                    // Close unregisters the native hook but retains its WinRT delegates.
                    // Break the native/managed cycle and release the callback owner.
                    _hook = null;
                    _keyDown = null;
                    _keyUp = null;
                    _isActive = null;
                    _filterKeyboardEvent = null;
                }

                disposed = true;
            }
            finally
            {
                Volatile.Write(ref _disposeState, disposed ? Disposed : 0);
            }
        }

        public bool GetDisposedState() => Volatile.Read(ref _disposeState) == Disposed;

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
