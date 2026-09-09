// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public readonly record struct DockLabelLength(double Value, bool InCharacters)
{
    private const double SquirrelHairsPerCharacter = 100;

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

        var inSquirrelHairs = text.EndsWith("sqh", StringComparison.Ordinal);
        if (!inSquirrelHairs && !text.EndsWith("ch", StringComparison.Ordinal))
        {
            return null;
        }

        var suffixLength = inSquirrelHairs ? 3 : 2;
        if (!double.TryParse(text.AsSpan(0, text.Length - suffixLength), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        // Normalize squirrel hair widths to ch so both units follow the same font measurement and text scaling.
        var characters = inSquirrelHairs ? amount / SquirrelHairsPerCharacter : amount;
        return IsValid(characters) ? new(characters, InCharacters: true) : null;
    }

    internal double? Resolve(double characterWidth)
    {
        var width = InCharacters ? Value * characterWidth : Value;
        return IsValid(width) ? width : null;
    }

    // XAML layout uses single-precision sizes internally, even though its public properties are doubles.
    private static bool IsValid(double value) => double.IsFinite(value) && value >= 0 && value <= float.MaxValue;
}
