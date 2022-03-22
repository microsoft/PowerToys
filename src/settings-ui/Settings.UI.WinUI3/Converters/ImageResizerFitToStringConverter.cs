// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Windows.ApplicationModel.Resources;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.WinUI3.Converters
{
    public sealed class ImageResizerFitToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var toLower = false;
            if ((string)parameter == "ToLower")
            {
                toLower = true;
            }

            string targetValue = string.Empty;
            switch (value)
            {
                case 0: targetValue = ResourceLoader.GetForCurrentView().GetString("ImageResizer_Fit_Fill_ThirdPersonSingular"); break;
                case 1: targetValue = ResourceLoader.GetForCurrentView().GetString("ImageResizer_Fit_Fit_ThirdPersonSingular"); break;
                case 2: targetValue = ResourceLoader.GetForCurrentView().GetString("ImageResizer_Fit_Stretch_ThirdPersonSingular"); break;
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
