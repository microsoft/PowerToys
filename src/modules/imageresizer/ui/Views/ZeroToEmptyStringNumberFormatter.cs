// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Windows.Globalization.NumberFormatting;

namespace ImageResizer.Views;

public partial class ZeroToEmptyStringNumberFormatter : INumberFormatter2, INumberParser
{
    public string FormatDouble(double value) => value switch
    {
        0 => string.Empty,
        _ => value.ToString(CultureInfo.CurrentCulture),
    };

    public double? ParseDouble(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out double result) ? result : 0;
    }

    public string FormatInt(long value) => throw new NotImplementedException();

    public string FormatUInt(ulong value) => throw new NotImplementedException();

    public long? ParseInt(string text) => throw new NotImplementedException();

    public ulong? ParseUInt(string text) => throw new NotImplementedException();
}
