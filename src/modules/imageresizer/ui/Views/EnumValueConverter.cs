// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
// Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;
using ImageResizer.Properties;

namespace ImageResizer.Views
{
    [ValueConversion(typeof(Enum), typeof(string))]
    public class EnumValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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

            // Fixes #16792 - Looks like culture defaults to en-US, so wrong resource is being fetched.
#pragma warning disable CA1304 // Specify CultureInfo
            var targetValue = Resources.ResourceManager.GetString(builder.ToString());
#pragma warning restore CA1304 // Specify CultureInfo

            if (toLower)
            {
                targetValue = targetValue.ToLower(culture);
            }

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value;
    }
}
