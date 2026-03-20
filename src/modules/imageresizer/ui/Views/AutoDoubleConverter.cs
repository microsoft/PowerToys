#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using ImageResizer.Properties;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Views;

internal sealed partial class AutoDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            double d => d switch
            {
                double.NaN => "0",
                0 => (string)parameter == "Auto" ? ResourceLoaderInstance.ResourceLoader.GetString("Input_Auto") : "0",
                _ => d.ToString(CultureInfo.CurrentCulture),
            },

            _ => "0",
        };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            null or "" => 0,
            string text when double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out double result) => result,
            _ => 0,
        };
}
