// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Globalization;
using System.Windows.Data;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(double), typeof(string))]
    internal class AutoDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var d = (double)value;

            return d != 0
                ? d.ToString(culture)
                : (string)parameter == "Auto"
                    ? Resources.Input_Auto
                    : string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (string)value;

            return !string.IsNullOrEmpty(text)
                ? double.Parse(text, culture)
                : 0;
        }
    }
}
