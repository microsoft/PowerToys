// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ImageResizer.Helpers;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Converters
{
    public partial class AutoDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double d && (d == 0 || double.IsNaN(d)))
            {
                return ResourceLoaderInstance.ResourceLoader.GetString("Auto");
            }

            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
