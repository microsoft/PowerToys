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

public sealed partial class ImageResizerUnitToStringConverter : IValueConverter
{
    // Maps each ResizeUnit value to its localized string.
    private static readonly Dictionary<ResizeUnit, string> UnitToText = new()
    {
        { ResizeUnit.Centimeter, Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Centimeter") },
        { ResizeUnit.Inch,       Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Inch") },
        { ResizeUnit.Percent,    Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Percent") },
        { ResizeUnit.Pixel,      Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_Unit_Pixel") },
    };

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ResizeUnit unit && UnitToText.TryGetValue(unit, out string unitText))
        {
            return parameter is string lowerParam && lowerParam == "ToLower" ?
                unitText.ToLower(CultureInfo.CurrentCulture) :
                unitText;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value;
    }
}
