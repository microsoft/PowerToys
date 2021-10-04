// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library;
using Windows.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class UpdateStateToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value == null || parameter == null)
            {
                return false;
            }
            else
            {
                if (value.ToString() == (string)parameter)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
