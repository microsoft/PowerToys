// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public partial class ConflictStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool hasConflict)
            {
                if (hasConflict)
                {
                    return Application.Current.Resources["ConflictKeyVisualStyle"];
                }
                else
                {
                    return Application.Current.Resources["AccentKeyVisualStyle"];
                }
            }

            return Application.Current.Resources["AccentKeyVisualStyle"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
