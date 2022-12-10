// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using ColorPicker.Helpers;
using ColorPicker.Settings;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using static ColorPicker.NativeMethods;

namespace ColorPicker.Keyboard
{
    [Export(typeof(KeyboardMonitor))]
    public class KeyboardMonitor : IDisposable
    {
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private List<string> _previouslyPressedKeys = new List<string>();

        private List<string> _activationKeys = new List<string>();
        private GlobalKeyboardHook _keyboardHook;
        private bool disposedValue;
        private bool _activationShortcutPressed;

        [ImportingConstructor]
        public KeyboardMonitor(AppStateHandler appStateHandler, IUserSettings userSettings)
        {
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;
            _userSettings.ActivationShortcut.PropertyChanged += ActivationShortcut_PropertyChanged;
            SetActivationKeys();
        }

        public void Start()
        {
            _keyboardHook = new GlobalKeyboardHook();
            _keyboardHook.KeyboardPressed += Hook_KeyboardPressed;
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
            if (virtualCode == KeyInterop.VirtualKeyFromKey(Key.Escape)
                && e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown
                )
            {
                if (_appStateHandler.IsColorPickerVisible()
                    || !AppStateHandler.BlockEscapeKeyClosingColorPickerEditor
                    )
                {
                    e.Handled = _appStateHandler.EndUserSession();
                    return;
                }
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
            if (!disposedValue)
            {
                if (disposing)
                {
                    _keyboardHook?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
