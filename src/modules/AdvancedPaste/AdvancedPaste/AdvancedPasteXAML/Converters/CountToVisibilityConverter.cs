// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AdvancedPaste.Converters;

public sealed partial class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool hasCount = ((value is int intValue) && intValue > 0) || (value is IEnumerable collection && collection.GetEnumerator().MoveNext());

        if (targetType == typeof(Visibility))
        {
            return hasCount ? Visibility.Visible : Visibility.Collapsed;
        }
        else if (targetType == typeof(bool))
        {
            return hasCount;
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(targetType));
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
