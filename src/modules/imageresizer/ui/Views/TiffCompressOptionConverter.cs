﻿#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(TiffCompressOption), typeof(string))]
    internal class TiffCompressOptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => Resources.ResourceManager.GetString(
                "TiffCompressOption_" + Enum.GetName(typeof(TiffCompressOption), value),
                culture);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
