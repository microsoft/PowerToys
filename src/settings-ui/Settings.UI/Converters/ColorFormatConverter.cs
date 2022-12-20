// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class ColorFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value != null)
            {
                // get string representation in 2 steps. First replace all color specific number values then in 2nd step replace color name with localisation
                return Library.Helpers.ColorNameHelper.ReplaceName(ColorFormatHelper.GetStringRepresentation(null, (string)value), null);
            }
            else
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
