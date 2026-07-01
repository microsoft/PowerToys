// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WorkspacesLauncherUI.Converters
{
    /// <summary>
    /// Converts a boolean to Visibility:
    ///   true  → Visible
    ///   false → Collapsed
    ///
    /// Used to show the loading spinner while an app is still launching.
    /// </summary>
    public sealed partial class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && boolValue)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        {
            throw new System.NotImplementedException();
        }
    }
}
