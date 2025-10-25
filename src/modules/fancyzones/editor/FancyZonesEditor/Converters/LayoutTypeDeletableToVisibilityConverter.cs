// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

using FancyZonesEditor.Models;

namespace FancyZonesEditor.Converters
{
    public class LayoutTypeDeletableToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Allow deletion for custom layouts and all template layouts except Blank
            LayoutType type = (LayoutType)value;
            return (type == LayoutType.Custom || (type != LayoutType.Blank && type != LayoutType.Custom)) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
    }
}
