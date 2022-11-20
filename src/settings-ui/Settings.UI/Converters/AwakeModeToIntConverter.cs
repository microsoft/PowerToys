// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class AwakeModeToIntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var mode = (AwakeMode)value;
            return (int)mode;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (AwakeMode)Enum.ToObject(typeof(AwakeMode), (int)value);
        }
    }
}
