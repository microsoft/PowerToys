// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    // Formats a pixel value (int property binding, or the double value a Slider passes to its
    // ThumbToolTipValueConverter) as a short "<n> px" label. One-way display only.
    public sealed partial class PixelsLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int pixels = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return $"{pixels} px";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
