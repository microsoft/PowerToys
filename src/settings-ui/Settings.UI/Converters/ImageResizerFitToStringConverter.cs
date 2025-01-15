// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed partial class ImageResizerFitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool toLower = (string)parameter == "ToLower";

            string targetValue = string.Empty;

            switch (value is ResizeFit enumValue ? enumValue : value is int intValue ? (ResizeFit)intValue : default)
            {
                case ResizeFit.Fill: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Fit_Fill_ThirdPersonSingular"); break;
                case ResizeFit.Fit: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Fit_Fit_ThirdPersonSingular"); break;
                case ResizeFit.Stretch: targetValue = Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Fit_Stretch_ThirdPersonSingular"); break;
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
