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

/// <summary>
/// Creates accessibility text for controls related to <see cref="ImageSize"/> properties.
/// </summary>
/// <example>(Name) "Edit the Small preset"</example>
/// <example>(FullDescription) "Large - Fits within 1920 × 1080 pixels"</example>"
public sealed partial class ImageResizerSizeToAccessibleTextConverter : IValueConverter
{
    private const char TimesGlyph = '\u00D7';   // Unicode "MULTIPLICATION SIGN"

    /// <summary>
    /// Maps the supplied accessibility identifier to the format string of the localized accessible text.
    /// </summary>
    private static readonly Dictionary<string, string> AccessibilityFormats = new()
    {
        { "Edit", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_EditButton_Accessibility_Name") },
        { "Remove", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_RemoveButton_Accessibility_Name") },
    };

    private readonly ImageResizerFitToStringConverter _fitConverter = new();
    private readonly ImageResizerUnitToStringConverter _unitConverter = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value, parameter) switch
        {
            (string presetName, string nameId) => FormatNameText(presetName, nameId),
            (ImageSize preset, string _) => FormatDescriptionText(preset),
            _ => DependencyProperty.UnsetValue,
        };
    }

    private object FormatNameText(string presetName, string nameId)
    {
        return AccessibilityFormats.TryGetValue(nameId, out string format) ?
            string.Format(CultureInfo.CurrentCulture, format, presetName) :
            DependencyProperty.UnsetValue;
    }

    private object FormatDescriptionText(ImageSize preset)
    {
        if (preset == null)
        {
            return DependencyProperty.UnsetValue;
        }

        string fitText = _fitConverter.Convert(preset.Fit, typeof(string), null, null) as string;
        string unitText = _unitConverter.Convert(preset.Unit, typeof(string), null, null) as string;

        return preset.IsHeightUsed ?
            $"{preset.Name} - {fitText} {preset.Width} {TimesGlyph} {preset.Height} {unitText}" :
            $"{preset.Name} - {fitText} {preset.Width} {unitText}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
