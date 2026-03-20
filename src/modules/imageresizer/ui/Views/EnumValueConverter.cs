#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.Text;
using ImageResizer.Properties;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Views
{
    public partial class EnumValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var type = value?.GetType();
            if (!type.IsEnum)
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

            if (toLower)
            {
                targetValue = targetValue.ToLower(CultureInfo.CurrentCulture);
            }

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => value;
    }
}
