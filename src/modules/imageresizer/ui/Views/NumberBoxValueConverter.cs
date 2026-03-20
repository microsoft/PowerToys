// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Views;

public partial class NumberBoxValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double d && double.IsNaN(d) ? 0 : value;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            null => 0,
            double d when double.IsNaN(d) => 0,
            string str when !double.TryParse(str, out _) => 0,
            _ => value,
        };
}
