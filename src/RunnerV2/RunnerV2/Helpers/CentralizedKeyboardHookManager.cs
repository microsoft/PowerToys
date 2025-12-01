// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.System;

namespace RunnerV2.Helpers
{
    internal static class CentralizedKeyboardHookManager
    {
        private static readonly UIntPtr _ignoreKeyEventFlag = 0x5555;

        private static readonly Dictionary<string, List<(HotkeySettings HotkeySettings, Action Action)>> _keyboardHooks = [];

        private static HotkeySettingsControlHook _hotkeySettingsControlHook = new(OnKeyDown, OnKeyUp, IsActive, (_, specialFlags) => specialFlags != _ignoreKeyEventFlag);

        private static void OnKeyDown(int key)
        {
            switch ((VirtualKey)key)
            {
                case VirtualKey.Control:
                case VirtualKey.LeftControl:
                case VirtualKey.RightControl:
                    _ctrlState = true;
                    break;
                case VirtualKey.Menu:
                case VirtualKey.LeftMenu:
                case VirtualKey.RightMenu:
                    _altState = true;
                    break;
                case VirtualKey.Shift:
                case VirtualKey.LeftShift:
                case VirtualKey.RightShift:
                    _shiftState = true;
                    break;
                case VirtualKey.LeftWindows:
                case VirtualKey.RightWindows:
                    _winState = true;
                    break;
            }

            SendSingleKeyboardInput((short)key, (uint)NativeKeyboardHelper.KeyEventF.KeyDown);
        }

        private static void OnKeyUp(int key)
        {
            switch ((VirtualKey)key)
            {
                case VirtualKey.Control:
                case VirtualKey.LeftControl:
                case VirtualKey.RightControl:
                    _ctrlState = false;
                    break;
                case VirtualKey.Menu:
                case VirtualKey.LeftMenu:
                case VirtualKey.RightMenu:
                    _altState = false;
                    break;
                case VirtualKey.Shift:
                case VirtualKey.LeftShift:
                case VirtualKey.RightShift:
                    _shiftState = false;
                    break;
                case VirtualKey.LeftWindows:
                case VirtualKey.RightWindows:
                    _winState = false;
                    break;
                default:
                    OnKeyboardEvent(new HotkeySettings
                    {
                        Code = key,
                        Ctrl = _ctrlState,
                        Alt = _altState,
                        Shift = _shiftState,
                        Win = _winState,
                    });
                    break;
            }

            SendSingleKeyboardInput((short)key, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
        }

        private static bool _ctrlState;
        private static bool _altState;
        private static bool _shiftState;
        private static bool _winState;

        private static bool _isActive;

        private static bool IsActive()
        {
            return _isActive;
        }

        public static void AddKeyboardHook(string moduleName, HotkeySettings hotkeySettings, Action action)
        {
#pragma warning disable CA1854 // Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method
            if (!_keyboardHooks.ContainsKey(moduleName))
            {
                _keyboardHooks[moduleName] = [];
            }
#pragma warning restore CA1854 // Prefer the 'IDictionary.TryGetValue(TKey, out TValue)' method

            _keyboardHooks[moduleName].Add((hotkeySettings, action));
        }

        public static void RemoveAllHooksFromModule(string moduleName)
        {
            _keyboardHooks.Remove(moduleName);
        }

        private static void OnKeyboardEvent(HotkeySettings pressedHotkey)
        {
            foreach (var moduleHooks in _keyboardHooks.Values)
            {
                foreach (var (hotkeySettings, action) in moduleHooks)
                {
                    if (hotkeySettings == pressedHotkey)
                    {
                        action();
                    }
                }
            }
        }

        public static void Start()
        {
            if (_hotkeySettingsControlHook.GetDisposedState())
            {
                _hotkeySettingsControlHook = new(OnKeyDown, OnKeyUp, IsActive, (_, specialFlags) => specialFlags != _ignoreKeyEventFlag);
            }

            _isActive = true;
        }

        public static void Stop()
        {
            _isActive = false;
            _hotkeySettingsControlHook.Dispose();
        }

        // Function to send a single key event to the system which would be ignored by the hotkey control.
        private static void SendSingleKeyboardInput(short keyCode, uint keyStatus)
        {
            NativeKeyboardHelper.INPUT inputShift = new()
            {
                type = NativeKeyboardHelper.INPUTTYPE.INPUT_KEYBOARD,
                data = new NativeKeyboardHelper.InputUnion
                {
                    ki = new NativeKeyboardHelper.KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = keyStatus,
                        dwExtraInfo = _ignoreKeyEventFlag,
                    },
                },
            };

            NativeKeyboardHelper.INPUT[] inputs = [inputShift];

            _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);
        }
    }
}
