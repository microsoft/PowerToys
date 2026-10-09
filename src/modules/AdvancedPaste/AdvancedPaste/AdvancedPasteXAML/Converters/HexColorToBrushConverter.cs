// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AdvancedPaste.Converters
{
    public sealed partial class HexColorToBrushConverter : IValueConverter
    {
        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotSupportedException();

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not string hexColor || string.IsNullOrWhiteSpace(hexColor))
            {
                return null;
            }

            Windows.UI.Color? color = HexColorConverterHelper.ConvertHexColorToRgb(hexColor);

            return color != null ? new SolidColorBrush((Windows.UI.Color)color) : null;
        }
    }
}
