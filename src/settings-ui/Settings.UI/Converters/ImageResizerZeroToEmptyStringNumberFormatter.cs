// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters;

public partial class ImageResizerZeroToEmptyStringNumberFormatter
{
    public string Format(long value) => throw new NotImplementedException();

    public string Format(ulong value) => throw new NotImplementedException();

    public string Format(double value) => throw new NotImplementedException();

    public string FormatDouble(double? value) => value switch
    {
        null => string.Empty,
        0 => string.Empty,
        _ => value.Value.ToString(CultureInfo.CurrentCulture),
    };

    public double? ParseDouble(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0.0;
        }

        return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out double result) ? result : 0.0;
    }

    public long? ParseInt(string text) => throw new NotImplementedException();

    public ulong? ParseUInt(string text) => throw new NotImplementedException();
}
