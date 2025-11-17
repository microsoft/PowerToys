// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AdvancedPaste.Converters;

public sealed partial class PasteFormatsToHeightConverter : IValueConverter
{
    private const int ItemHeight = 40;

    public int MaxItems { get; set; } = 5;

    public object Convert(object value, Type targetType, object parameter, string language) =>
        new GridLength(GetHeight((value is ICollection collection) ? collection.Count : (value is int intValue) ? intValue : 0));

    public int GetHeight(int itemCount) => Math.Min(MaxItems, itemCount) * ItemHeight;

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
