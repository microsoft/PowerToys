#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Globalization;
using System.Windows.Data;

using ImageResizer.Properties;

namespace ImageResizer.Views;

/// <summary>
/// Converts between double and string for text-based controls bound to Width or Height fields.
/// Optionally returns localized "Auto" text when the underlying value is 0, letting the UI show,
/// for example "(auto) x 1024 pixels".
/// </summary>
[ValueConversion(typeof(double), typeof(string))]
internal class AutoDoubleConverter : IValueConverter
{
    /// <summary>
    /// Converts a double to a string, optionally showing "Auto" for 0 values. NaN values are
    /// converted to empty strings.
    /// </summary>
    /// <param name="value">The value to convert from <see cref="double"/> to
    /// <see cref="string"/>.</param>
    /// <param name="targetType">The conversion target type. <see cref="string"/> here.</param>
    /// <param name="parameter">Set to "Auto" to return the localized "Auto" string if the
    /// value is 0.</param>
    /// <param name="culture">The <see cref="CultureInfo"/> to use for the number formatting.
    /// </param>
    /// <returns>The string representation of the passed-in value.</returns>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            double d => d switch
            {
                double.NaN => "0",
                0 => (string)parameter == "Auto" ? Resources.Input_Auto : "0",
                _ => d.ToString(culture),
            },

            _ => "0",
        };

    /// <summary>
    /// Converts the string representation back to a double, returning 0 if the string is empty,
    /// null or not a valid number in the specified culture.
    /// </summary>
    /// <param name="value">The string value to convert.</param>
    /// <param name="targetType">The conversion target type. <see cref="double"/> here.</param>
    /// <param name="parameter">Converter parameter. Unused.</param>
    /// <param name="culture">The <see cref="CultureInfo"/> to use for the text parsing.</param>
    /// <returns>The corresponding double value.</returns>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value switch
        {
            null or "" => 0,
            string text when double.TryParse(text, NumberStyles.Any, culture, out double result) => result,
            _ => 0,
        };
}
