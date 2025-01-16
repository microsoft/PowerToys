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
/// <example>"Edit the Small preset"</example>
/// <example>"Edits the Large preset, which fits within 1920 by 1080 pixels"</example>"
public sealed partial class ImageResizerSizeToAccessibleTextConverter : IValueConverter
{
    /// <summary>
    /// Maps the supplied accessibility identifier to the format string of the localized accessible text.
    /// </summary>
    private static readonly Dictionary<string, string> AccessibilityFormats = new()
    {
        { "EditName", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_EditButton_Accessibility_Name") },
        { "RemoveName", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_RemoveButton_Accessibility_Name") },
        { "EditDescription", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_EditButton_Accessibility_Description") },
        { "RemoveDescription", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_RemoveButton_Accessibility_Description") },
        { "EditDescriptionPercent", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_EditButton_Accessibility_DescriptionPercent") },
        { "RemoveDescriptionPercent", Helpers.ResourceLoaderInstance.ResourceLoader.GetString("ImageResizer_RemoveButton_Accessibility_DescriptionPercent") },
    };

    private readonly ImageResizerFitToStringConverter _fitConverter = new();
    private readonly ImageResizerUnitToStringConverter _unitConverter = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return (value, parameter) switch
        {
            (string presetName, string nameId) => FormatNameText(presetName, nameId),
            (ImageSize preset, string descriptionId) => FormatDescriptionText(preset, descriptionId),
            _ => DependencyProperty.UnsetValue,
        };
    }

    private object FormatNameText(string presetName, string nameId)
    {
        return AccessibilityFormats.TryGetValue(nameId, out string format) ?
            string.Format(CultureInfo.CurrentCulture, format, presetName) :
            DependencyProperty.UnsetValue;
    }

    private object FormatDescriptionText(ImageSize preset, string descriptionId)
    {
        string lookupKey = preset.IsHeightUsed ? descriptionId : descriptionId + "Percent";

        if (!AccessibilityFormats.TryGetValue(lookupKey, out string format))
        {
            return DependencyProperty.UnsetValue;
        }

        string fitText = (_fitConverter.Convert(preset.Fit, typeof(string), null, null) as string).ToLower(CultureInfo.CurrentCulture);
        string unitText = _unitConverter.Convert(preset.Unit, typeof(string), null, null) as string;

        var parameters = preset.IsHeightUsed ?
            new object[] { preset.Name, fitText, preset.Width, preset.Height, unitText } :
            new object[] { preset.Name, fitText, preset.Width };

        return string.Format(CultureInfo.CurrentCulture, format, parameters);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
