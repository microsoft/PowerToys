﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using CommunityToolkit.WinUI;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ShortcutControl : UserControl, IDisposable
    {
        private readonly UIntPtr ignoreKeyEventFlag = (UIntPtr)0x5555;
        private System.Collections.Generic.HashSet<VirtualKey> _modifierKeysOnEntering = new System.Collections.Generic.HashSet<VirtualKey>();
        private bool _enabled;
        private HotkeySettings hotkeySettings;
        private HotkeySettings internalSettings;
        private HotkeySettings lastValidSettings;
        private HotkeySettingsControlHook hook;
        private bool _isActive;
        private bool disposedValue;

        public string Header { get; set; }

        public string Keys { get; set; }

        public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register("Enabled", typeof(bool), typeof(ShortcutControl), null);
        public static readonly DependencyProperty HotkeySettingsProperty = DependencyProperty.Register("HotkeySettings", typeof(HotkeySettings), typeof(ShortcutControl), null);

        public static readonly DependencyProperty AllowDisableProperty = DependencyProperty.Register("AllowDisable", typeof(bool), typeof(ShortcutControl), new PropertyMetadata(false, OnAllowDisableChanged));

        private static void OnAllowDisableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var me = d as ShortcutControl;
            if (me == null)
            {
                return;
            }

            var description = me.c?.FindDescendant<TextBlock>();
            if (description == null)
            {
                return;
            }

            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            var newValue = (bool)(e?.NewValue ?? false);

            var text = newValue ? resourceLoader.GetString("Activation_Shortcut_With_Disable_Description") : resourceLoader.GetString("Activation_Shortcut_Description");
            description.Text = text;
        }

        private ShortcutDialogContentControl c = new ShortcutDialogContentControl();
        private ContentDialog shortcutDialog;

        public bool AllowDisable
        {
            get => (bool)GetValue(AllowDisableProperty);
            set => SetValue(AllowDisableProperty, value);
        }

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
                    EditButton.IsEnabled = true;
                }
                else
                {
                    EditButton.IsEnabled = false;
                }
            }
        }

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
                    PreviewKeysControl.ItemsSource = HotkeySettings.GetKeysList();
                    AutomationProperties.SetHelpText(EditButton, HotkeySettings.ToString());
                    c.Keys = HotkeySettings.GetKeysList();
                }
            }
        }

        public ShortcutControl()
        {
            InitializeComponent();
            internalSettings = new HotkeySettings();

            this.Unloaded += ShortcutControl_Unloaded;
            this.Loaded += ShortcutControl_Loaded;

            var resourceLoader = Helpers.ResourceLoaderInstance.ResourceLoader;

            // We create the Dialog in C# because doing it in XAML is giving WinUI/XAML Island bugs when using dark theme.
            shortcutDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = resourceLoader.GetString("Activation_Shortcut_Title"),
                Content = c,
                PrimaryButtonText = resourceLoader.GetString("Activation_Shortcut_Save"),
                SecondaryButtonText = resourceLoader.GetString("Activation_Shortcut_Reset"),
                CloseButtonText = resourceLoader.GetString("Activation_Shortcut_Cancel"),
                DefaultButton = ContentDialogButton.Primary,
            };
            shortcutDialog.SecondaryButtonClick += ShortcutDialog_Reset;
            shortcutDialog.RightTapped += ShortcutDialog_Disable;

            AutomationProperties.SetName(EditButton, resourceLoader.GetString("Activation_Shortcut_Title"));

            OnAllowDisableChanged(this, null);
        }

        private void ShortcutControl_Unloaded(object sender, RoutedEventArgs e)
        {
            shortcutDialog.PrimaryButtonClick -= ShortcutDialog_PrimaryButtonClick;
            shortcutDialog.Opened -= ShortcutDialog_Opened;
            shortcutDialog.Closing -= ShortcutDialog_Closing;

            if (App.GetSettingsWindow() != null)
            {
                App.GetSettingsWindow().Activated -= ShortcutDialog_SettingsWindow_Activated;
            }

            // Dispose the HotkeySettingsControlHook object to terminate the hook threads when the textbox is unloaded
            hook?.Dispose();

            hook = null;
        }

        private void ShortcutControl_Loaded(object sender, RoutedEventArgs e)
        {
            // These all belong here; because of virtualization in e.g. a ListView, the control can go through several Loaded / Unloaded cycles.
            hook?.Dispose();

            hook = new HotkeySettingsControlHook(Hotkey_KeyDown, Hotkey_KeyUp, Hotkey_IsActive, FilterAccessibleKeyboardEvents);

            shortcutDialog.PrimaryButtonClick += ShortcutDialog_PrimaryButtonClick;
            shortcutDialog.Opened += ShortcutDialog_Opened;
            shortcutDialog.Closing += ShortcutDialog_Closing;

            if (App.GetSettingsWindow() != null)
            {
                App.GetSettingsWindow().Activated += ShortcutDialog_SettingsWindow_Activated;
            }
        }

        private void KeyEventHandler(int key, bool matchValue, int matchValueCode)
        {
            VirtualKey virtualKey = (VirtualKey)key;
            switch (virtualKey)
            {
                case VirtualKey.LeftWindows:
                case VirtualKey.RightWindows:
                    if (!matchValue && _modifierKeysOnEntering.Contains(virtualKey))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(virtualKey);
                    }

                    internalSettings.Win = matchValue;
                    break;
                case VirtualKey.Control:
                case VirtualKey.LeftControl:
                case VirtualKey.RightControl:
                    if (!matchValue && _modifierKeysOnEntering.Contains(VirtualKey.Control))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(VirtualKey.Control);
                    }

                    internalSettings.Ctrl = matchValue;
                    break;
                case VirtualKey.Menu:
                case VirtualKey.LeftMenu:
                case VirtualKey.RightMenu:
                    if (!matchValue && _modifierKeysOnEntering.Contains(VirtualKey.Menu))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(VirtualKey.Menu);
                    }

                    internalSettings.Alt = matchValue;
                    break;
                case VirtualKey.Shift:
                case VirtualKey.LeftShift:
                case VirtualKey.RightShift:
                    if (!matchValue && _modifierKeysOnEntering.Contains(VirtualKey.Shift))
                    {
                        SendSingleKeyboardInput((short)virtualKey, (uint)NativeKeyboardHelper.KeyEventF.KeyUp);
                        _ = _modifierKeysOnEntering.Remove(VirtualKey.Shift);
                    }

                    internalSettings.Shift = matchValue;
                    break;
                case VirtualKey.Escape:
                    internalSettings = new HotkeySettings();
                    shortcutDialog.IsPrimaryButtonEnabled = false;
                    return;
                default:
                    internalSettings.Code = matchValueCode;
                    break;
            }
        }

        // Function to send a single key event to the system which would be ignored by the hotkey control.
        private void SendSingleKeyboardInput(short keyCode, uint keyStatus)
        {
            NativeKeyboardHelper.INPUT inputShift = new NativeKeyboardHelper.INPUT
            {
                type = NativeKeyboardHelper.INPUTTYPE.INPUT_KEYBOARD,
                data = new NativeKeyboardHelper.InputUnion
                {
                    ki = new NativeKeyboardHelper.KEYBDINPUT
                    {
                        wVk = keyCode,
                        dwFlags = keyStatus,

                        // Any keyevent with the extraInfo set to this value will be ignored by the keyboard hook and sent to the system instead.
                        dwExtraInfo = ignoreKeyEventFlag,
                    },
                },
            };

            NativeKeyboardHelper.INPUT[] inputs = new NativeKeyboardHelper.INPUT[] { inputShift };

            _ = NativeMethods.SendInput(1, inputs, NativeKeyboardHelper.INPUT.Size);
        }

        private bool FilterAccessibleKeyboardEvents(int key, UIntPtr extraInfo)
        {
            // A keyboard event sent with this value in the extra Information field should be ignored by the hook so that it can be captured by the system instead.
            if (extraInfo == ignoreKeyEventFlag)
            {
                return false;
            }

            // If the current key press is tab, based on the other keys ignore the key press so as to shift focus out of the hotkey control.
            if ((VirtualKey)key == VirtualKey.Tab)
            {
                // Shift was not pressed while entering and Shift is not pressed while leaving the hotkey control, treat it as a normal tab key press.
                if (!internalSettings.Shift && !_modifierKeysOnEntering.Contains(VirtualKey.Shift) && !internalSettings.Win && !internalSettings.Alt && !internalSettings.Ctrl)
                {
                    return false;
                }

                // Shift was not pressed while entering but it was pressed while leaving the hotkey, therefore simulate a shift key press as the system does not know about shift being pressed in the hotkey.
                else if (internalSettings.Shift && !_modifierKeysOnEntering.Contains(VirtualKey.Shift) && !internalSettings.Win && !internalSettings.Alt && !internalSettings.Ctrl)
                {
                    // This is to reset the shift key press within the control as it was not used within the control but rather was used to leave the hotkey.
                    internalSettings.Shift = false;

                    SendSingleKeyboardInput((short)VirtualKey.Shift, (uint)NativeKeyboardHelper.KeyEventF.KeyDown);

                    return false;
                }

                // Shift was pressed on entering and remained pressed, therefore only ignore the tab key so that it can be passed to the system.
                // As the shift key is already assumed to be pressed by the system while it entered the hotkey control, shift would still remain pressed, hence ignoring the tab input would simulate a Shift+Tab key press.
                else if (!internalSettings.Shift && _modifierKeysOnEntering.Contains(VirtualKey.Shift) && !internalSettings.Win && !internalSettings.Alt && !internalSettings.Ctrl)
                {
                    return false;
                }
            }

            // Either the cancel or save button has keyboard focus.
            if (FocusManager.GetFocusedElement(LayoutRoot.XamlRoot).GetType() == typeof(Button))
            {
                return false;
            }

            return true;
        }

        private void Hotkey_KeyDown(int key)
        {
            KeyEventHandler(key, true, key);

            c.Keys = internalSettings.GetKeysList();

            if (internalSettings.GetKeysList().Count == 0)
            {
                // Empty, disable save button
                shortcutDialog.IsPrimaryButtonEnabled = false;
            }
            else if (internalSettings.GetKeysList().Count == 1)
            {
                // 1 key, disable save button
                shortcutDialog.IsPrimaryButtonEnabled = false;

                // Check if the one key is a hotkey
                if (internalSettings.Shift || internalSettings.Win || internalSettings.Alt || internalSettings.Ctrl)
                {
                    c.IsError = false;
                }
                else
                {
                    c.IsError = true;
                }
            }

            // Tab and Shift+Tab are accessible keys and should not be displayed in the hotkey control.
            if (internalSettings.Code > 0 && !internalSettings.IsAccessibleShortcut())
            {
                lastValidSettings = internalSettings with { };

                if (!ComboIsValid(lastValidSettings))
                {
                    DisableKeys();
                }
                else
                {
                    EnableKeys();
                }
            }

            c.IsWarningAltGr = internalSettings.Ctrl && internalSettings.Alt && !internalSettings.Win && (internalSettings.Code > 0);
        }

        private void EnableKeys()
        {
            shortcutDialog.IsPrimaryButtonEnabled = true;
            c.IsError = false;

            // WarningLabel.Style = (Style)App.Current.Resources["SecondaryTextStyle"];
        }

        private void DisableKeys()
        {
            shortcutDialog.IsPrimaryButtonEnabled = false;
            c.IsError = true;

            // WarningLabel.Style = (Style)App.Current.Resources["SecondaryWarningTextStyle"];
        }

        private void Hotkey_KeyUp(int key)
        {
            KeyEventHandler(key, false, 0);
        }

        private bool Hotkey_IsActive()
        {
            return _isActive;
        }

        private void ShortcutDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (!ComboIsValid(hotkeySettings))
            {
                DisableKeys();
            }
            else
            {
                EnableKeys();
            }

            // Reset the status on entering the hotkey each time.
            _modifierKeysOnEntering.Clear();

            // To keep track of the modifier keys, whether it was pressed on entering.
            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.Shift) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.Shift);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.Control) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.Control);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.Menu) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.Menu);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.LeftWindows) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.LeftWindows);
            }

            if ((NativeMethods.GetAsyncKeyState((int)VirtualKey.RightWindows) & 0x8000) != 0)
            {
                _modifierKeysOnEntering.Add(VirtualKey.RightWindows);
            }

            _isActive = true;
        }

        private async void OpenDialogButton_Click(object sender, RoutedEventArgs e)
        {
            c.Keys = null;
            c.Keys = HotkeySettings.GetKeysList();

            // 92 means the Win key. The logic is: warning should be visible if the shortcut contains Alt AND contains Ctrl AND NOT contains Win.
            // Additional key must be present, as this is a valid, previously used shortcut shown at dialog open. Check for presence of non-modifier-key is not necessary therefore
            c.IsWarningAltGr = c.Keys.Contains("Ctrl") && c.Keys.Contains("Alt") && !c.Keys.Contains(92);

            shortcutDialog.XamlRoot = this.XamlRoot;
            shortcutDialog.RequestedTheme = this.ActualTheme;
            await shortcutDialog.ShowAsync();
        }

        private void ShortcutDialog_Reset(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            hotkeySettings = null;

            SetValue(HotkeySettingsProperty, hotkeySettings);
            PreviewKeysControl.ItemsSource = HotkeySettings.GetKeysList();

            lastValidSettings = hotkeySettings;

            AutomationProperties.SetHelpText(EditButton, HotkeySettings.ToString());
            shortcutDialog.Hide();
        }

        private void ShortcutDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (ComboIsValid(lastValidSettings))
            {
                HotkeySettings = lastValidSettings with { };
            }

            PreviewKeysControl.ItemsSource = hotkeySettings.GetKeysList();
            AutomationProperties.SetHelpText(EditButton, HotkeySettings.ToString());
            shortcutDialog.Hide();
        }

        private void ShortcutDialog_Disable(object sender, RightTappedRoutedEventArgs e)
        {
            if (!AllowDisable)
            {
                return;
            }

            var empty = new HotkeySettings();
            HotkeySettings = empty;

            PreviewKeysControl.ItemsSource = HotkeySettings.GetKeysList();
            AutomationProperties.SetHelpText(EditButton, HotkeySettings.ToString());
            shortcutDialog.Hide();
        }

        private static bool ComboIsValid(HotkeySettings settings)
        {
            if (settings != null && (settings.IsValid() || settings.IsEmpty()))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void ShortcutDialog_SettingsWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            args.Handled = true;
            if (args.WindowActivationState != WindowActivationState.Deactivated && (hook == null || hook.GetDisposedState() == true))
            {
                // If the PT settings window gets focussed/activated again, we enable the keyboard hook to catch the keyboard input.
                hook = new HotkeySettingsControlHook(Hotkey_KeyDown, Hotkey_KeyUp, Hotkey_IsActive, FilterAccessibleKeyboardEvents);
            }
            else if (args.WindowActivationState == WindowActivationState.Deactivated && hook != null && hook.GetDisposedState() == false)
            {
                // If the PT settings window lost focus/activation, we disable the keyboard hook to allow keyboard input on other windows.
                hook.Dispose();
                hook = null;
            }
        }

        private void ShortcutDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            _isActive = false;
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (hook != null)
                    {
                        hook.Dispose();
                    }

                    hook = null;
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
