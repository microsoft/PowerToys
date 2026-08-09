// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.Helpers;

internal static class IconPathParser
{
    private const uint UnsignedConversionError = uint.MaxValue;
    private const int SignedConversionError = int.MaxValue;

    public static bool TryParseBinaryIconReference(string iconPath, out BinaryIconReference iconReference)
    {
        var commaIndex = iconPath.IndexOf(',');
        var path = commaIndex < 0 ? iconPath.AsSpan() : iconPath.AsSpan(0, commaIndex);

        if (!path.EndsWith(".exe", StringComparison.Ordinal)
            && !path.EndsWith(".dll", StringComparison.Ordinal)
            && !path.EndsWith(".lnk", StringComparison.Ordinal))
        {
            iconReference = default;
            return false;
        }

        var index = 0;
        if (commaIndex >= 0)
        {
            index = ParseNativeIconIndex(iconPath.AsSpan(commaIndex + 1));
            if (index == SignedConversionError)
            {
                iconReference = default;
                return false;
            }
        }

        iconReference = new(commaIndex < 0 ? iconPath : iconPath[..commaIndex], index);
        return true;
    }

    // Preserve til::to_int quirks from the native converter: '-' is recognized anywhere,
    // A-F digits are accepted even for decimal and octal input, and values at or above
    // uint.MaxValue / 16 are rejected.
    private static int ParseNativeIconIndex(ReadOnlySpan<char> text)
    {
        var signPosition = text.IndexOf('-');
        var hasSign = signPosition >= 0;
        var unsignedText = hasSign ? text[(signPosition + 1)..] : text;
        var result = ParseNativeUnsignedLong(unsignedText);
        if (result == UnsignedConversionError)
        {
            return SignedConversionError;
        }

        return hasSign ? -(int)result : (int)result;
    }

    private static uint ParseNativeUnsignedLong(ReadOnlySpan<char> text)
    {
        const uint maximumValue = uint.MaxValue / 16;

        var numberBase = 10u;
        var position = 0;
        if (text.Length > 1 && text[0] == '0')
        {
            numberBase = 8;
            position++;
            if (text.Length > 2 && text[position] is 'x' or 'X')
            {
                numberBase = 16;
                position++;
            }
        }

        if (position == text.Length)
        {
            return UnsignedConversionError;
        }

        var accumulator = 0u;
        while (true)
        {
            var character = text[position];
            uint value;
            if (character is >= '0' and <= '9')
            {
                value = (uint)(character - '0');
            }
            else if (character is >= 'A' and <= 'F')
            {
                value = (uint)(character - 'A') + 10u;
            }
            else if (character is >= 'a' and <= 'f')
            {
                value = (uint)(character - 'a') + 10u;
            }
            else
            {
                return UnsignedConversionError;
            }

            accumulator = unchecked(accumulator + value);
            if (accumulator >= maximumValue)
            {
                return UnsignedConversionError;
            }

            position++;
            if (position == text.Length)
            {
                return accumulator;
            }

            accumulator = unchecked(accumulator * numberBase);
        }
    }
}
