// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public partial class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null)
            {
                return false;
            }

            // Get the enum value as string
            var enumString = value.ToString();
            var parameterString = parameter.ToString();

            return enumString.Equals(parameterString, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
