// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class ModuleEnabledToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isEnabled = (bool)value;

            if (App.IsDarkTheme())
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
