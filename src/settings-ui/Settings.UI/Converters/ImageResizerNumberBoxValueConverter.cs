// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.PowerToys.Settings.UI.Converters;

public partial class ImageResizerNumberBoxValueConverter : IValueConverter
{
    /// <summary>
    /// Converts the underlying double value to a display-friendly format. Ensures that NaN values
    /// are not propagated to the UI.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double d && double.IsNaN(d) ? 0.0 : value;

    /// <summary>
    /// Converts the user input back to the underlying double value. If the input is not a valid
    /// number, a double with value 0 is returned.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value switch
        {
            null => 0.0,
            double d when double.IsNaN(d) => 0.0,
            string str when !double.TryParse(str, out _) => 0.0,
            _ => value,
        };
}
