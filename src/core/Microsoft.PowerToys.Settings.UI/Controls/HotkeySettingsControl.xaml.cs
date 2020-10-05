// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Lib;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class HotkeySettingsControl : UserControl
    {
        private bool _shiftKeyDownOnEntering = false;

        private bool _shiftToggled = false;

        public string Header { get; set; }

        public string Keys { get; set; }

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(
                "Enabled",
                typeof(bool),
                typeof(HotkeySettingsControl),
                null);

        private bool _enabled = false;

        public bool Enabled
        {
            get
            {
                return _enabled;
            }

            set
            {
                SetValue(IsActiveProperty, value);
                _enabled = value;

                if (value)
                {
                    HotkeyTextBox.IsEnabled = true;

                    // TitleText.IsActive = "True";
                    // TitleGlyph.IsActive = "True";
                }
                else
                {
                    HotkeyTextBox.IsEnabled = false;

                    // TitleText.IsActive = "False";
                    // TitleGlyph.IsActive = "False";
                }
            }
        }

        public static readonly DependencyProperty HotkeySettingsProperty =
            DependencyProperty.Register(
                "HotkeySettings",
                typeof(HotkeySettings),
                typeof(HotkeySettingsControl),
                null);

        private HotkeySettings hotkeySettings;
        private HotkeySettings internalSettings;
        private HotkeySettings lastValidSettings;
        private HotkeySettingsControlHook hook;
        private bool _isActive;

        public HotkeySettings HotkeySettings
        {
            get
            {
                return hotkeySettings;
            }

            set
            {
                if (hotkeySettings != value)
                {
                    hotkeySettings = value;
                    SetValue(HotkeySettingsProperty, value);
                    HotkeyTextBox.Text = HotkeySettings.ToString();
                }
            }
        }

        public HotkeySettingsControl()
        {
            InitializeComponent();
            internalSettings = new HotkeySettings();

            HotkeyTextBox.GettingFocus += HotkeyTextBox_GettingFocus;
            HotkeyTextBox.LosingFocus += HotkeyTextBox_LosingFocus;
            HotkeyTextBox.Unloaded += HotkeyTextBox_Unloaded;
            hook = new HotkeySettingsControlHook(Hotkey_KeyDown, Hotkey_KeyUp, Hotkey_IsActive, FilterAccessibleKeyboardEvents);
        }

        private void HotkeyTextBox_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dispose the HotkeySettingsControlHook object to terminate the hook threads when the textbox is unloaded
            hook.Dispose();
        }

        private void KeyEventHandler(int key, bool matchValue, int matchValueCode, string matchValueText)
        {
            switch ((Windows.System.VirtualKey)key)
            {
                case Windows.System.VirtualKey.LeftWindows:
                case Windows.System.VirtualKey.RightWindows:
                    internalSettings.Win = matchValue;
                    break;
                case Windows.System.VirtualKey.Control:
                case Windows.System.VirtualKey.LeftControl:
                case Windows.System.VirtualKey.RightControl:
                    internalSettings.Ctrl = matchValue;
                    break;
                case Windows.System.VirtualKey.Menu:
                case Windows.System.VirtualKey.LeftMenu:
                case Windows.System.VirtualKey.RightMenu:
                    internalSettings.Alt = matchValue;
                    break;
                case Windows.System.VirtualKey.Shift:
                case Windows.System.VirtualKey.LeftShift:
                case Windows.System.VirtualKey.RightShift:
                    _shiftToggled = true;
                    internalSettings.Shift = matchValue;
                    break;
                case Windows.System.VirtualKey.Escape:
                    internalSettings = new HotkeySettings();
                    HotkeySettings = new HotkeySettings();
                    return;
                default:
                    internalSettings.Code = matchValueCode;
                    break;
            }
        }

        private bool FilterAccessibleKeyboardEvents(int key)
        {
            if (key == 0x09)
            {
                // TODO: Others should not be pressed
                if (!internalSettings.Shift && !_shiftKeyDownOnEntering)
                {
                    return false;
                }

                // shift was not pressed while entering but it was pressed while leaving the hotkey
                else if (internalSettings.Shift && !_shiftKeyDownOnEntering)
                {
                    internalSettings.Shift = false;

                    NativeKeyboardHelper.INPUT inputShift = new NativeKeyboardHelper.INPUT
                    {
                        type = NativeKeyboardHelper.INPUTTYPE.INPUT_KEYBOARD,
                        data = new NativeKeyboardHelper.InputUnion
                        {
                            ki = new NativeKeyboardHelper.KEYBDINPUT
                            {
                                wVk = 0x10,
                                dwFlags = (uint)NativeKeyboardHelper.KeyEventF.KeyDown,
                                dwExtraInfo = (UIntPtr)0x5555,
                            },
                        },
                    };

                    NativeKeyboardHelper.INPUT[] inputs = new NativeKeyboardHelper.INPUT[] { inputShift };

                    _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);

                    return false;
                }

                // Shift was pressed on entering and remained pressed
                else if (!internalSettings.Shift && _shiftKeyDownOnEntering && !_shiftToggled)
                {
                    return false;
                }

                // Shift was pressed on entering but it was released and later pressed again
                else if (internalSettings.Shift && _shiftKeyDownOnEntering && _shiftToggled)
                {
                    internalSettings.Shift = false;

                    return false;
                }

                // Shift was pressed on entering and was later released
                else if (!internalSettings.Shift && _shiftKeyDownOnEntering && _shiftToggled)
                {
                    NativeKeyboardHelper.INPUT inputShift = new NativeKeyboardHelper.INPUT
                    {
                        type = NativeKeyboardHelper.INPUTTYPE.INPUT_KEYBOARD,
                        data = new NativeKeyboardHelper.InputUnion
                        {
                            ki = new NativeKeyboardHelper.KEYBDINPUT
                            {
                                wVk = 0x10,
                                dwFlags = (uint)NativeKeyboardHelper.KeyEventF.KeyUp,
                                dwExtraInfo = (UIntPtr)0x5555,
                            },
                        },
                    };

                    NativeKeyboardHelper.INPUT[] inputs = new NativeKeyboardHelper.INPUT[] { inputShift };

                    _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);

                    return false;
                }
            }

            return true;
        }

        private async void Hotkey_KeyDown(int key)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                KeyEventHandler(key, true, key, Lib.Utilities.Helper.GetKeyName((uint)key));
                if (internalSettings.Code > 0)
                {
                    lastValidSettings = internalSettings.Clone();
                    HotkeyTextBox.Text = lastValidSettings.ToString();
                }
            });
        }

        private async void Hotkey_KeyUp(int key)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                KeyEventHandler(key, false, 0, string.Empty);
            });
        }

        private bool Hotkey_IsActive()
        {
            return _isActive;
        }

        private void HotkeyTextBox_GettingFocus(object sender, RoutedEventArgs e)
        {
            _shiftKeyDownOnEntering = false;
            _shiftToggled = false;

            if ((NativeMethods.GetAsyncKeyState(0x10) & 0x8000) != 0)
            {
                _shiftKeyDownOnEntering = true;
            }

            _isActive = true;
        }

        private void HotkeyTextBox_LosingFocus(object sender, RoutedEventArgs e)
        {
            if (lastValidSettings != null && (lastValidSettings.IsValid() || lastValidSettings.IsEmpty()))
            {
                HotkeySettings = lastValidSettings.Clone();
            }

            HotkeyTextBox.Text = hotkeySettings.ToString();
            _isActive = false;
        }
    }
}
