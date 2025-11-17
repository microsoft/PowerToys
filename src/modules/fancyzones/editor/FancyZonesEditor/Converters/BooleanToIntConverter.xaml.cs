// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Data;

namespace FancyZonesEditor.Converters
{
    public class BooleanToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool valueBool)
            {
                return valueBool == true ? 1 : 0;
            }

            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int valueInt)
            {
                return valueInt == 1;
            }

            return false;
        }
    }
}
