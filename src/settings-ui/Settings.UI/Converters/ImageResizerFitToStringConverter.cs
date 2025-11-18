// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters;

public sealed partial class ImageResizerFitToStringConverter : IValueConverter
{
    // Maps each ResizeFit to its localized string.
    private static readonly Dictionary<ResizeFit, string> FitToText = new()
    {
        { ResizeFit.Fill,    Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Fit_Fill_ThirdPersonSingular") },
        { ResizeFit.Fit,     Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Fit_Fit_ThirdPersonSingular") },
        { ResizeFit.Stretch, Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Fit_Stretch_ThirdPersonSingular") },
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ResizeFit fit && FitToText.TryGetValue(fit, out string fitText))
        {
            return parameter is string lowerParam && lowerParam == "ToLower" ?
                fitText.ToLower(CultureInfo.CurrentCulture) :
                fitText;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value;
    }
}
