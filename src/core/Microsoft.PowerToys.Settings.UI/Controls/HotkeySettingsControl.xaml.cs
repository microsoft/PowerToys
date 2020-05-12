// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Lib;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

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
        private HotkeySettings internalSettings = new HotkeySettings();

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

            HotkeyTextBox.PreviewKeyDown += HotkeyTextBox_KeyDown;
            HotkeyTextBox.LostFocus += HotkeyTextBox_LosingFocus;
        }

        private static bool IsDown(Windows.System.VirtualKey key)
        {
            return Window.Current.CoreWindow.GetKeyState(key).HasFlag(CoreVirtualKeyStates.Down);
        }

        private void HotkeyTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            e.Handled = true;
            if (
                e.Key == Windows.System.VirtualKey.LeftWindows ||
                e.Key == Windows.System.VirtualKey.RightWindows ||
                e.Key == Windows.System.VirtualKey.Control ||
                e.Key == Windows.System.VirtualKey.Menu ||
                e.Key == Windows.System.VirtualKey.Shift)
            {
                return;
            }

            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                internalSettings = new HotkeySettings();
                HotkeySettings = new HotkeySettings();
                return;
            }

            var settings = new HotkeySettings();

            // Display HotKey value
            if (IsDown(Windows.System.VirtualKey.LeftWindows) ||
                IsDown(Windows.System.VirtualKey.RightWindows))
            {
                settings.Win = true;
            }

            if (IsDown(Windows.System.VirtualKey.Control))
            {
                settings.Ctrl = true;
            }

            if (IsDown(Windows.System.VirtualKey.Menu))
            {
                settings.Alt = true;
            }

            if (IsDown(Windows.System.VirtualKey.Shift))
            {
                settings.Shift = true;
            }

            settings.Key = Lib.Utilities.Helper.GetKeyName((uint)e.Key);

            settings.Code = (int)e.OriginalKey;
            internalSettings = settings;
            HotkeyTextBox.Text = internalSettings.ToString();
        }

        private void HotkeyTextBox_LosingFocus(object sender, RoutedEventArgs e)
        {
            if (internalSettings.IsValid() || internalSettings.IsEmpty())
            {
                HotkeySettings = internalSettings;
            }

            HotkeyTextBox.Text = hotkeySettings.ToString();
        }
    }
}
