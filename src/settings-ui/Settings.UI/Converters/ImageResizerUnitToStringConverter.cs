// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class ImageResizerUnitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool toLower = (string)parameter == "ToLower";

            string targetValue = string.Empty;
            switch (value is ResizeUnit enumValue ? enumValue : value is int intValue ? (ResizeUnit)intValue : default)
            {
                case ResizeUnit.Centimeter: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Centimeter"); break;
                case ResizeUnit.Inch: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Inch"); break;
                case ResizeUnit.Percent: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Percent"); break;
                case ResizeUnit.Pixel: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Pixel"); break;
            }

            if (toLower)
            {
                targetValue = targetValue.ToLower(CultureInfo.CurrentCulture);
            }

            return targetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return value;
        }
    }
}
