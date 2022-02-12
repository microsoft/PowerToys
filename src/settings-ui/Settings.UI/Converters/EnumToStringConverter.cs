// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;
using Windows.ApplicationModel.Resources;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public class EnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var typeName = value.GetType().Name;
            var valueString = value.ToString();
            var resourceKey = typeName + "_" + valueString;
            return ResourceLoader.GetForCurrentView().GetString(resourceKey);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
