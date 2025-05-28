// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.UI.Converters;

public class PercentageToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int percentage)
        {
            return percentage / 100.0;
        }

        return 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is double opacity)
        {
            return (int)(opacity * 100);
        }

        return 100;
    }
}