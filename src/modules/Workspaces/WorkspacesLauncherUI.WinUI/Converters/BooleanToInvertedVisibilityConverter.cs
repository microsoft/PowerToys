// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WorkspacesLauncherUI.Converters
{
    /// <summary>
    /// Converts a boolean to inverted Visibility:
    ///   true  → Collapsed (hide the element)
    ///   false → Visible (show the element)
    ///
    /// Used to show the status glyph (checkmark/X) only when loading is complete.
    /// The spinner uses the standard BooleanToVisibility (true=Visible),
    /// and this converter shows the glyph when loading is false.
    /// </summary>
    public sealed partial class BooleanToInvertedVisibilityConverter : IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, string language)
        {
            if (value is bool boolValue && boolValue)
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        {
            throw new System.NotImplementedException();
        }
    }
}
