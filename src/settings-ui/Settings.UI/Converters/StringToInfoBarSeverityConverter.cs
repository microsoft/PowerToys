// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class StringToInfoBarSeverityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            try
            {
                if (value == null)
                {
                    return Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
                }
                else
                {
                    return (Microsoft.UI.Xaml.Controls.InfoBarSeverity)Enum.Parse(typeof(Microsoft.UI.Xaml.Controls.InfoBarSeverity), (string)value, true);
                }
            }
            catch
            {
                return Microsoft.UI.Xaml.Controls.InfoBarSeverity.Informational;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
