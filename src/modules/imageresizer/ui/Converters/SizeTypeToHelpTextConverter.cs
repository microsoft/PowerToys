// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ImageResizer.Models;
using Microsoft.UI.Xaml.Data;

namespace ImageResizer.Converters
{
    public sealed partial class SizeTypeToHelpTextConverter : IValueConverter
    {
        private const char MultiplicationSign = '\u00D7';

        private readonly EnumValueConverter _enumConverter = new();
        private readonly AutoDoubleConverter _autoDoubleConverter = new();

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is not ResizeSize size)
            {
                return null;
            }

            string EnumToString(Enum value, string parameter = null) =>
                _enumConverter.Convert(value, typeof(string), parameter, language) as string;

            string DoubleToString(double value) =>
                _autoDoubleConverter.Convert(value, typeof(string), null, language) as string;

            var fit = EnumToString(size.Fit, "ThirdPersonSingular");
            var width = DoubleToString(size.Width);
            var unit = EnumToString(size.Unit);

            return size.ShowHeight ?
                $"{fit} {width} {MultiplicationSign} {DoubleToString(size.Height)} {unit}" :
                $"{fit} {width} {unit}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}
