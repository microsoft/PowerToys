// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class ModuleEnabledToForegroundConverter : IValueConverter
    {
        private readonly ISettingsUtils settingsUtils = new SettingsUtils();

        private string selectedTheme = string.Empty;

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isEnabled = (bool)value;

            var defaultTheme = new Windows.UI.ViewManagement.UISettings();

            // Using InvariantCulture as this is an internal string and expected to be in hexadecimal
            var uiTheme = defaultTheme.GetColorValue(Windows.UI.ViewManagement.UIColorType.Background).ToString(CultureInfo.InvariantCulture);

            // Normalize strings to uppercase according to Fxcop
            selectedTheme = SettingsRepository<GeneralSettings>.GetInstance(settingsUtils).SettingsConfig.Theme.ToUpperInvariant();

            if (selectedTheme == "DARK" || (selectedTheme == "SYSTEM" && uiTheme == "#FF000000"))
            {
                // DARK
                if (isEnabled)
                {
                    return (SolidColorBrush)Application.Current.Resources["DarkForegroundBrush"];
                }
                else
                {
                    return (SolidColorBrush)Application.Current.Resources["DarkForegroundDisabledBrush"];
                }
            }
            else
            {
                // LIGHT
                if (isEnabled)
                {
                    return (SolidColorBrush)Application.Current.Resources["LightForegroundBrush"];
                }
                else
                {
                    return (SolidColorBrush)Application.Current.Resources["LightForegroundDisabledBrush"];
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
