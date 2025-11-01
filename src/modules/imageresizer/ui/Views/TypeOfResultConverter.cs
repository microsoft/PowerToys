// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Windows.Data;

namespace ImageResizer.Views;

/// <summary>
/// A converter that takes the value of an object and returns its System.Type. Used in XAML
/// triggers on the results page to change presentation based on the type of the data context.
/// </summary>
public class TypeOfResultConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.GetType();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
