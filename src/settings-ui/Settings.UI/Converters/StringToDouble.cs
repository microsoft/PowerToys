// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public partial class StringToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is double d)
            {
                return d.ToString(CultureInfo.InvariantCulture);
            }

            return "0";
        }
    }
}
