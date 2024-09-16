// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Windows.Data;

namespace PowerLauncher.Converters
{
    public class SumConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var sum = 0.0d;

            foreach (var value in values)
            {
                if (value is double number)
                {
                    sum += number;
                }
                else if (value is string strNumber)
                {
                    sum += double.Parse(strNumber, NumberStyles.Any, CultureInfo.InvariantCulture);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return sum;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
