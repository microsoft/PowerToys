// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ColorPicker.Helpers;
using ColorPicker.Settings;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Windows.System;

using static ColorPicker.NativeMethods;

namespace ColorPicker.Keyboard
{
    public class KeyboardMonitor : IDisposable
    {
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private List<string> _previouslyPressedKeys = new List<string>();

        private List<string> _activationKeys = new List<string>();
        private GlobalKeyboardHook _keyboardHook;
        private bool _activationShortcutPressed;
        private int keyboardMoveSpeed;
        private VirtualKey lastArrowKeyPressed = VirtualKey.None;

        public KeyboardMonitor(AppStateHandler appStateHandler, IUserSettings userSettings)
        {
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;
            _userSettings.ActivationShortcut.PropertyChanged += ActivationShortcut_PropertyChanged;
            SetActivationKeys();
        }

        public void Start()
        {
            // Make Start() idempotent: tear down any existing hook before creating a new one so a
            // repeated or re-entrant Start() (e.g. the activation shortcut pressed again while a
            // session is already active) cannot orphan the previous GlobalKeyboardHook — its native
            // WH_KEYBOARD_LL registration would otherwise never be unhooked and its pinned callback
            // delegate would be left dangling.
            DisposeHook();

            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyboardPressed += Hook_KeyboardPressed;
        }

        private void DisposeHook()
        {
            var hook = _keyboardHook;
            if (hook != null)
            {
                // Unsubscribe before Dispose so no keyboard callbacks fire during teardown.
                hook.KeyboardPressed -= Hook_KeyboardPressed;

                // Null the field only after Dispose succeeds. A Win32Exception from
                // UnhookWindowsHookEx retains the reference so a subsequent cleanup
                // attempt (e.g. the next Start() call) can retry disposal.
                hook.Dispose();
                _keyboardHook = null;
            }
        }

        private void SetActivationKeys()
        {
            _activationKeys.Clear();

            if (!string.IsNullOrEmpty(_userSettings.ActivationShortcut.Value))
            {
                var keys = _userSettings.ActivationShortcut.Value.Split('+');
                foreach (var key in keys)
                {
                    _activationKeys.Add(key.Trim());
                }

                _activationKeys.Sort();
            }
        }

        private void ActivationShortcut_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SetActivationKeys();
        }

        private void Hook_KeyboardPressed(object sender, GlobalKeyboardHookEventArgs e)
        {
            var currentlyPressedKeys = new List<string>();
            var virtualCode = e.KeyboardData.VirtualCode;

            // ESC pressed
            if (virtualCode == (int)VirtualKey.Escape && e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown)
            {
                e.Handled = _appStateHandler.HandleEscPressed();
                return;
            }

            if ((virtualCode == (int)VirtualKey.Space || virtualCode == (int)VirtualKey.Enter) && (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown))
            {
                e.Handled = _appStateHandler.HandleEnterPressed();
                return;
            }

            if (virtualCode == (int)VirtualKey.Back && e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown)
            {
                e.Handled = _appStateHandler.HandleEscPressed();
                return;
            }

            if (CheckMoveNeeded(virtualCode, VirtualKey.Up, e, 0, -1))
            {
                e.Handled = true;
                return;
            }
            else if (CheckMoveNeeded(virtualCode, VirtualKey.Down, e, 0, 1))
            {
                e.Handled = true;
                return;
            }
            else if (CheckMoveNeeded(virtualCode, VirtualKey.Left, e, -1, 0))
            {
                e.Handled = true;
                return;
            }
            else if (CheckMoveNeeded(virtualCode, VirtualKey.Right, e, 1, 0))
            {
                e.Handled = true;
                return;
            }

            var name = Helper.GetKeyName((uint)virtualCode);

            // If the last key pressed is a modifier key, then currentlyPressedKeys cannot possibly match with _activationKeys
            // because _activationKeys contains exactly 1 non-modifier key. Hence, there's no need to check if `name` is a
            // modifier key or to do any additional processing on it.
            if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown || e.KeyboardState == GlobalKeyboardHook.KeyboardState.SysKeyDown)
            {
                // Check pressed modifier keys.
                AddModifierKeys(currentlyPressedKeys);

                currentlyPressedKeys.Add(name);
            }

            currentlyPressedKeys.Sort();

            if (currentlyPressedKeys.Count == 0 && _previouslyPressedKeys.Count != 0)
            {
                // no keys pressed, we can enable activation shortcut again
                _activationShortcutPressed = false;
            }

            _previouslyPressedKeys = currentlyPressedKeys;

            if (ArraysAreSame(currentlyPressedKeys, _activationKeys))
            {
                // avoid triggering this action multiple times as this will be called nonstop while keys are pressed
                if (!_activationShortcutPressed)
                {
                    _activationShortcutPressed = true;

                    _appStateHandler.StartUserSession();
                }
            }
        }

        private bool CheckMoveNeeded(int virtualCode, VirtualKey key, GlobalKeyboardHookEventArgs e, int xMove, int yMove)
        {
            if (virtualCode == (int)key)
            {
                if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown && _appStateHandler.IsColorPickerVisible())
                {
                    if (lastArrowKeyPressed == key)
                    {
                        keyboardMoveSpeed++;
                    }
                    else
                    {
                        keyboardMoveSpeed = 1;
                    }

                    lastArrowKeyPressed = key;
                    _appStateHandler.MoveCursor(keyboardMoveSpeed * xMove, keyboardMoveSpeed * yMove);
                    return true;
                }
                else if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyUp)
                {
                    lastArrowKeyPressed = VirtualKey.None;
                    keyboardMoveSpeed = 0;
                }
            }

            return false;
        }

        private static bool ArraysAreSame(List<string> first, List<string> second)
        {
            if (first.Count != second.Count || (first.Count == 0 && second.Count == 0))
            {
                return false;
            }

            for (int i = 0; i < first.Count; i++)
            {
                if (first[i] != second[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddModifierKeys(List<string> currentlyPressedKeys)
        {
            if ((GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Shift");
            }

            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Ctrl");
            }

            if ((GetAsyncKeyState(VK_MENU) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Alt");
            }

            if ((GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0)
            {
                currentlyPressedKeys.Add("Win");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            DisposeHook();
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
