// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Wpf.Ui.Controls;

namespace ImageResizer.Views;

public class ZeroToEmptyStringNumberFormatter : INumberFormatter, INumberParser
{
    public string FormatDouble(double? value) => value switch
    {
        null => string.Empty,
        0 => string.Empty,
        _ => value.Value.ToString(CultureInfo.CurrentCulture),
    };

    public double? ParseDouble(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        return double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out double result) ? result : 0;
    }

    public string FormatInt(int? value) => throw new NotImplementedException();

    public string FormatUInt(uint? value) => throw new NotImplementedException();

    public int? ParseInt(string value) => throw new NotImplementedException();

    public uint? ParseUInt(string value) => throw new NotImplementedException();
}
