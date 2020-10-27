// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Globalization;
using System.Windows.Data;
using ImageResizer.Models;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(ResizeUnit), typeof(string))]
    internal class ResizeUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var output = Resources.ResourceManager.GetString(Enum.GetName(typeof(ResizeUnit), value), culture);

            if ((string)parameter == "ToLower")
            {
                output = output.ToLower(culture);
            }

            return output;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
