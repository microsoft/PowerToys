// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Common.UI.Controls
{
    public partial class BoolToKeyVisualStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b && parameter is string param)
            {
                if (b && param == "Warning")
                {
                    return State.Warning;
                }
                else if (b && param == "Error")
                {
                    return State.Error;
                }
                else
                {
                    return State.Normal;
                }
            }
            else
            {
                return State.Normal;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
