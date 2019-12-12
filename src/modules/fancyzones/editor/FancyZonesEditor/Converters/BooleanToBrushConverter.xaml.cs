// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Data;
using System.Windows.Media;

namespace FancyZonesEditor.Converters
{
    public class BooleanToBrushConverter : IValueConverter
    {
        private static readonly Brush _selectedBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD7));
        private static readonly Brush _normalBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((bool)value) ? _selectedBrush : _normalBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value == _selectedBrush;
        }
    }
}
