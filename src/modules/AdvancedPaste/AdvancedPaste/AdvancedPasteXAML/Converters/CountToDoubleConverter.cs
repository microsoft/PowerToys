// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;

namespace AdvancedPaste.Converters;

public sealed class CountToDoubleConverter : IValueConverter
{
    public double ValueIfZero { get; set; }

    public double ValueIfNonZero { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var count = (value is int intValue) ? intValue : 0;

        return count == 0 ? ValueIfZero : ValueIfNonZero;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
