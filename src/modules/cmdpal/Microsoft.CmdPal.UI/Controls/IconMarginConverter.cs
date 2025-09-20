// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI.Controls;

public sealed class IconMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // Only include a margin if there is text to separate from the icon.
        var text = value as string;
        return string.IsNullOrEmpty(text) ? new Thickness(0) : new Thickness(0, 0, 4, 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
