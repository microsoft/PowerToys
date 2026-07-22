// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    // Formats a millisecond value (int property binding, or the double a Slider passes to its
    // ThumbToolTipValueConverter) as a short "<n> ms" label. One-way display only.
    public sealed partial class MillisecondsLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int milliseconds = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return $"{milliseconds} ms";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
