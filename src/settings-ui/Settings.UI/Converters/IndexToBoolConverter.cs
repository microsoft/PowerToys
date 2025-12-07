// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    /// <summary>
    /// Converts an integer index to a boolean value for use with RadioButton groups.
    /// The ConverterParameter specifies which index value should return true.
    /// </summary>
    public sealed partial class IndexToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null)
            {
                return false;
            }

            if (value is int intValue && int.TryParse(parameter.ToString(), out int paramIndex))
            {
                return intValue == paramIndex;
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (value is bool isChecked && isChecked && parameter != null)
                {
                    if (int.TryParse(parameter.ToString(), out int paramIndex))
                    {
                        return paramIndex;
                    }
                }
            }
            catch
            {
                // Ignore exceptions
            }

            // Return UnsetValue to indicate no update should occur (RadioButton unchecked)
            return DependencyProperty.UnsetValue;
        }
    }
}
