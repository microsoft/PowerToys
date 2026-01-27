// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using ImageResizer.Helpers;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Converters
{
    public partial class EnumValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var type = value?.GetType();
            if (type == null || !type.IsEnum)
            {
                return value;
            }

            var builder = new StringBuilder();

            builder
                .Append(type.Name)
                .Append('_')
                .Append(Enum.GetName(type, value));

            var toLower = false;
            if ((string)parameter == "ToLower")
            {
                toLower = true;
            }
            else if (parameter != null)
            {
                builder
                    .Append('_')
                    .Append(parameter);
            }

            var targetValue = ResourceLoaderInstance.ResourceLoader.GetString(builder.ToString());

            if (toLower && !string.IsNullOrEmpty(targetValue))
            {
                var culture = string.IsNullOrEmpty(language) ? CultureInfo.CurrentCulture : new CultureInfo(language);
                targetValue = targetValue.ToLower(culture);
            }

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => value;
    }
}
