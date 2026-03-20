#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using ImageResizer.Models;
using ImageResizer.Properties;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Views
{
    internal sealed partial class TiffCompressOptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => ResourceLoaderInstance.ResourceLoader.GetString(
                "TiffCompressOption_" + Enum.GetName(typeof(TiffCompressOption), value));

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
