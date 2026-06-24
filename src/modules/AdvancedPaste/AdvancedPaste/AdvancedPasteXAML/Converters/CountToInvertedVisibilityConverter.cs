// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AdvancedPaste.Converters;

public sealed partial class CountToInvertedVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool hasItems = ((value is int intValue) && intValue > 0) || (value is IEnumerable enumerable && enumerable.GetEnumerator().MoveNext());
        return targetType == typeof(Visibility)
            ? (hasItems ? Visibility.Collapsed : Visibility.Visible)
            : !hasItems;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
