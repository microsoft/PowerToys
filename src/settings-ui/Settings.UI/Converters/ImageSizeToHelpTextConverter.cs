// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters;

public sealed partial class ImageSizeToHelpTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ImageSize imageSize)
        {
            return string.Empty;
        }

        var fit = new ImageResizerFitToStringConverter().Convert(imageSize.Fit, targetType, parameter, language);
        var unit = new ImageResizerUnitToStringConverter().Convert(imageSize.Unit, targetType, parameter, language);
        const string timesSymbol = "\u00D7";

        return imageSize.EnableEtraBoxes
            ? $"{imageSize.Name} - {fit} {imageSize.Width} {timesSymbol} {imageSize.Height} {unit}"
            : $"{imageSize.Name} - {fit} {imageSize.Width} {unit}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}
