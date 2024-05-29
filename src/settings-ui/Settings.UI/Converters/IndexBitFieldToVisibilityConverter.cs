// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class IndexBitFieldToVisibilityConverter : IValueConverter
    {
        // Receives a hexadecimal bit mask as a parameter. Will check the value against that bitmask.
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var currentIndexBit = 1 << (int)value;
            var selectedIndicesBitField = System.Convert.ToUInt32(parameter as string, 16);

            return (selectedIndicesBitField & currentIndexBit) == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
