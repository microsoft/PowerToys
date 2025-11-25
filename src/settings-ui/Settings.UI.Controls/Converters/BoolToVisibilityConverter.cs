// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Controls.Converters
{
    public partial class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool bVal = false;
            if (value is bool b)
            {
                bVal = b;
            }
            else if (value is bool?)
            {
                bVal = (bool?)value ?? false;
            }

            if (parameter is string s && s == "True")
            {
                // Invert
                bVal = !bVal;
            }

            return bVal ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
