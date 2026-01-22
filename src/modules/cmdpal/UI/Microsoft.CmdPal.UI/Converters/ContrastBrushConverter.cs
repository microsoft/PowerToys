// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Converters;

/// <summary>
/// Gets a color, either black or white, depending on the brightness of the supplied color.
/// </summary>
public sealed partial class ContrastBrushConverter : IValueConverter
{
    /// <summary>
    /// Gets or sets the alpha channel threshold below which a default color is used instead of black/white.
    /// </summary>
    public byte AlphaThreshold { get; set; } = 128;

    /// <inheritdoc />
    public object Convert(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        Color comparisonColor;
        Color? defaultColor = null;

        // Get the changing color to compare against
        if (value is Color valueColor)
        {
            comparisonColor = valueColor;
        }
        else if (value is SolidColorBrush valueBrush)
        {
            comparisonColor = valueBrush.Color;
        }
        else
        {
            // Invalid color value provided
            return DependencyProperty.UnsetValue;
        }

        // Get the default color when transparency is high
        if (parameter is Color parameterColor)
        {
            defaultColor = parameterColor;
        }
        else if (parameter is SolidColorBrush parameterBrush)
        {
            defaultColor = parameterBrush.Color;
        }

        if (comparisonColor.A < AlphaThreshold &&
            defaultColor.HasValue)
        {
            // If the transparency is less than 50 %, just use the default brush
            // This can commonly be something like the TextControlForeground brush
            return new SolidColorBrush(defaultColor.Value);
        }
        else
        {
            // Chose a white/black brush based on contrast to the base color
            return UseLightContrastColor(comparisonColor)
                ? new SolidColorBrush(Colors.White)
                : new SolidColorBrush(Colors.Black);
        }
    }

    /// <inheritdoc />
    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        string language)
    {
        return DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Determines whether a light or dark contrast color should be used with the given displayed color.
    /// </summary>
    /// <remarks>
    /// This code is using the WinUI algorithm.
    /// </remarks>
    private bool UseLightContrastColor(Color displayedColor)
    {
        // The selection ellipse should be light if and only if the chosen color
        // contrasts more with black than it does with white.
        // To find how much something contrasts with white, we use the equation
        // for relative luminance, which is given by
        //
        // L = 0.2126 * Rg + 0.7152 * Gg + 0.0722 * Bg
        //
        // where Xg = { X/3294 if X <= 10, (R/269 + 0.0513)^2.4 otherwise }
        //
        // If L is closer to 1, then the color is closer to white; if it is closer to 0,
        // then the color is closer to black.  This is based on the fact that the human
        // eye perceives green to be much brighter than red, which in turn is perceived to be
        // brighter than blue.
        //
        // If the third dimension is value, then we won't be updating the spectrum's displayed colors,
        // so in that case we should use a value of 1 when considering the backdrop
        // for the selection ellipse.
        var rg = displayedColor.R <= 10
            ? displayedColor.R / 3294.0
            : Math.Pow((displayedColor.R / 269.0) + 0.0513, 2.4);
        var gg = displayedColor.G <= 10
            ? displayedColor.G / 3294.0
            : Math.Pow((displayedColor.G / 269.0) + 0.0513, 2.4);
        var bg = displayedColor.B <= 10
            ? displayedColor.B / 3294.0
            : Math.Pow((displayedColor.B / 269.0) + 0.0513, 2.4);

        return (0.2126 * rg) + (0.7152 * gg) + (0.0722 * bg) <= 0.5;
    }
}
