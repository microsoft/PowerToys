// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.Toolkit.Win32.UI.XamlHost;

namespace Microsoft.PowerToys.Settings.UI
{
    public sealed partial class App : XamlApplication
    {
        public App()
        {
            Initialize();

            // Hide the Xaml Island window
            var coreWindow = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            var coreWindowInterop = Interop.GetInterop(coreWindow);
            NativeMethods.ShowWindow(coreWindowInterop.WindowHandle, Interop.SW_HIDE);
        }

        public static bool IsDarkTheme()
        {
            var selectedTheme = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.Theme.ToUpper(CultureInfo.InvariantCulture);
            var defaultTheme = new Windows.UI.ViewManagement.UISettings();
            var uiTheme = defaultTheme.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return selectedTheme == "DARK" || (selectedTheme == "SYSTEM" && uiTheme == "#FF000000");
        }

        private static ISettingsUtils settingsUtils = new SettingsUtils();
    }
}
