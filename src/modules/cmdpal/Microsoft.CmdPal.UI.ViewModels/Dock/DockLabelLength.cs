// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public readonly record struct DockLabelLength(double Value, bool InCharacters, string? Sample = null)
{
    internal static DockLabelLength? Parse(object? value)
    {
        if (value is double dips && IsValid(dips))
        {
            return new(dips, InCharacters: false);
        }

        if (value is not string text)
        {
            return null;
        }

        if (text.StartsWith("text:", StringComparison.Ordinal))
        {
            return new(0, InCharacters: false, Sample: text[5..]);
        }

        if (!text.EndsWith("ch", StringComparison.Ordinal) ||
            !double.TryParse(text.AsSpan(0, text.Length - 2), NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var characters))
        {
            return null;
        }

        return IsValid(characters) ? new(characters, InCharacters: true) : null;
    }

    internal double? Resolve(double characterWidth, double? sampleWidth = null)
    {
        var width = Sample is not null ? sampleWidth : InCharacters ? Value * characterWidth : Value;
        return width.HasValue && IsValid(width.Value) ? width : null;
    }

    // XAML layout uses single-precision sizes internally, even though its public properties are doubles.
    private static bool IsValid(double value) => double.IsFinite(value) && value >= 0 && value <= float.MaxValue;
}
