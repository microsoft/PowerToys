// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Input;
using ColorPicker.Helpers;
using ColorPicker.Settings;
using ColorPicker.Telemetry;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Telemetry;

namespace ColorPicker.Keyboard
{
    [Export(typeof(KeyboardMonitor))]
    public class KeyboardMonitor
    {
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;

        private List<string> _currentlyPressedKeys = new List<string>();
        private List<string> _activationKeys = new List<string>();
        private GlobalKeyboardHook _keyboardHook;

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
            var virtualCode = e.KeyboardData.VirtualCode;

            // ESC pressed
            if (virtualCode == KeyInterop.VirtualKeyFromKey(Key.Escape))
            {
                _currentlyPressedKeys.Clear();
                _appStateHandler.HideColorPicker();
                PowerToysTelemetry.Log.WriteEvent(new ColorPickerCancelledEvent());
            }

            var name = Helper.GetKeyName((uint)virtualCode);

            // we got modifier with additional info such as "Ctrl (left)" - get rid of parenthesess
            if (name.IndexOf("(", StringComparison.OrdinalIgnoreCase) > 0 && name.Length > 1)
            {
                name = name.Substring(0, name.IndexOf("(", StringComparison.OrdinalIgnoreCase)).Trim();
            }

            if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyDown || e.KeyboardState == GlobalKeyboardHook.KeyboardState.SysKeyDown)
            {
                if (!_currentlyPressedKeys.Contains(name))
                {
                    _currentlyPressedKeys.Add(name);
                }
            }
            else if (e.KeyboardState == GlobalKeyboardHook.KeyboardState.KeyUp || e.KeyboardState == GlobalKeyboardHook.KeyboardState.SysKeyUp)
            {
                if (_currentlyPressedKeys.Contains(name))
                {
                    _currentlyPressedKeys.Remove(name);
                }
            }

            _currentlyPressedKeys.Sort();

            if (ArraysAreSame(_currentlyPressedKeys, _activationKeys))
            {
                _appStateHandler.ShowColorPicker();
                _currentlyPressedKeys.Clear();
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
    }
}
