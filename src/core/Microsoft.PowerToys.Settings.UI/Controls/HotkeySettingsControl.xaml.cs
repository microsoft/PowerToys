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

        public HotkeySettings HotkeySettings
        {
            get
            {
                return this.hotkeySettings;
            }

            set
            {
                if (this.hotkeySettings != value)
                {
                    this.hotkeySettings = value;
                    this.SetValue(HotkeySettingsProperty, value);
                    this.HotkeyTextBox.Text = this.HotkeySettings.ToString();
                }
            }
        }

        public HotkeySettingsControl()
        {
            this.InitializeComponent();
            this.HotkeyTextBox.PreviewKeyDown += this.HotkeyTextBox_KeyDown;
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

            var settings = new HotkeySettings();

            // Display HotKey value
            if (IsDown(Windows.System.VirtualKey.LeftWindows) ||
                IsDown(Windows.System.VirtualKey.RightWindows))
            {
                settings.win = true;
            }

            if (IsDown(Windows.System.VirtualKey.Control))
            {
                settings.ctrl = true;
            }

            if (IsDown(Windows.System.VirtualKey.Menu))
            {
                settings.alt = true;
            }

            if (IsDown(Windows.System.VirtualKey.Shift))
            {
                settings.shift = true;
            }

            settings.key = e.Key.ToString();

            // TODO: Check that e.OriginalKey is the ScanCode. It is not clear from docs.
            settings.code = (int)e.OriginalKey;
            this.HotkeySettings = settings;
        }
    }
}
