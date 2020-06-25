// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using System;
using System.Data;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236
namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class HotkeySettingsControl : UserControl
    {
        public string Header { get; set; }

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
            hook = new HotkeySettingsControlHook(Hotkey_KeyDown, Hotkey_KeyUp, Hotkey_IsActive);
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
                    internalSettings.Shift = matchValue;
                    break;
                case Windows.System.VirtualKey.Escape:
                    internalSettings = new HotkeySettings();
                    HotkeySettings = new HotkeySettings();
                    return;
                default:
                    internalSettings.Code = matchValueCode;
                    internalSettings.Key = matchValueText;
                    break;
            }
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
