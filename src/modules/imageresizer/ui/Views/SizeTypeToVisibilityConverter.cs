// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ImageResizer.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Views
{
    internal sealed partial class SizeTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value != null && value.GetType() == typeof(CustomSize) ? Visibility.Visible : (object)Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (Visibility)value == Visibility.Visible;
        }
    }
}
