// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
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
            if ((VirtualKey)key == VirtualKey.RightMenu && _ctrlState)
            {
                _ctrlAltState = true;
            }

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
                default:
                    if (OnKeyboardEvent(new HotkeySettings
                    {
                        Code = key,
                        Ctrl = _ctrlState,
                        Alt = _altState,
                        Shift = _shiftState,
                        Win = _winState,
                    }))
                    {
                        return;
                    }

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
            }

            // Correctly release Ctrl key if Ctrl+Alt (AltGr) was used.
            if (_ctrlAltState && (VirtualKey)key == VirtualKey.RightMenu)
            {
                _ctrlAltState = false;
                _ctrlState = false;

                SendSingleKeyboardInput((short)VirtualKey.LeftControl, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
            }

            SendSingleKeyboardInput((short)key, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
        }

        private static bool _ctrlState;
        private static bool _altState;
        private static bool _shiftState;
        private static bool _winState;
        private static bool _ctrlAltState;

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

        private static bool OnKeyboardEvent(HotkeySettings pressedHotkey)
        {
            bool shortcutHandled = false;

            foreach (var moduleHooks in _keyboardHooks.Values)
            {
                foreach (var (hotkeySettings, action) in moduleHooks)
                {
                    if (hotkeySettings == pressedHotkey)
                    {
                        action();
                        shortcutHandled = true;
                    }
                }
            }

            return shortcutHandled;
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
            if (IsExtendedVirtualKey(keyCode))
            {
               keyStatus |= (uint)NativeKeyboardHelper.KeyEventF.ExtendedKey;
            }

            NativeKeyboardHelper.INPUT input = new()
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

            NativeKeyboardHelper.INPUT[] inputs = [input];

            _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);
        }

        private static bool IsExtendedVirtualKey(short vk)
        {
            return vk switch
            {
                0xA5 => true, // VK_RMENU (Right Alt - AltGr)
                0xA3 => true, // VK_RCONTROL
                0x2D => true, // VK_INSERT
                0x2E => true, // VK_DELETE
                0x23 => true, // VK_END
                0x24 => true, // VK_HOME
                0x21 => true, // VK_PRIOR (Page Up)
                0x22 => true, // VK_NEXT (Page Down)
                0x25 => true, // VK_LEFT
                0x26 => true, // VK_UP
                0x27 => true, // VK_RIGHT
                0x28 => true, // VK_DOWN
                0x90 => true, // VK_NUMLOCK
                _ => false,
            };
        }
    }
}
