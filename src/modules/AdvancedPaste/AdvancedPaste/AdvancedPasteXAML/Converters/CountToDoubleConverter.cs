// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

using Microsoft.UI.Xaml.Data;

namespace AdvancedPaste.Converters;

public sealed partial class CountToDoubleConverter : IValueConverter
{
    public double ValueIfZero { get; set; }

    public double ValueIfNonZero { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool hasCount = ((value is int intValue) && intValue > 0) || (value is IEnumerable collection && collection.GetEnumerator().MoveNext());

        return hasCount ? ValueIfNonZero : ValueIfZero;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
