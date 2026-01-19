// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class StringFolder : IStringFolder
{
    // Cache for diacritic-stripped uppercase characters.
    // Benign race: worst case is redundant computation writing the same value.
    private static readonly char[] StripCacheUpper = new char[char.MaxValue + 1];

    public string Fold(string input, bool removeDiacritics)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        if (!removeDiacritics || Ascii.IsValid(input))
        {
            if (IsAlreadyFoldedAndSlashNormalized(input))
            {
                return input;
            }

            return string.Create(input.Length, input, static (dst, src) =>
            {
                for (var i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    dst[i] = c == '\\' ? '/' : char.ToUpperInvariant(c);
                }
            });
        }

        return string.Create(input.Length, input, static (dst, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                var upper = c == '\\' ? '/' : char.ToUpperInvariant(c);
                dst[i] = StripDiacriticsFromUpper(upper);
            }
        });
    }

    private static bool IsAlreadyFoldedAndSlashNormalized(string input)
    {
        var sawNonAscii = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '\\')
            {
                return false;
            }

            if ((uint)(c - 'a') <= 'z' - 'a')
            {
                return false;
            }

            if (c > 0x7F)
            {
                sawNonAscii = true;
            }
        }

        if (sawNonAscii)
        {
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (c <= 0x7F)
                {
                    continue;
                }

                var cat = CharUnicodeInfo.GetUnicodeCategory(c);
                if (cat is UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter)
                {
                    return false;
                }
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char StripDiacriticsFromUpper(char upper)
    {
        if (upper <= 0x7F)
        {
            return upper;
        }

        var cached = StripCacheUpper[upper];
        if (cached != '\0')
        {
            return cached;
        }

        var mapped = StripDiacriticsSlow(upper);
        StripCacheUpper[upper] = mapped;
        return mapped;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static char StripDiacriticsSlow(char upper)
    {
        var baseChar = FirstNonMark(upper, NormalizationForm.FormD);
        if (baseChar == '\0' || baseChar == upper)
        {
            var kd = FirstNonMark(upper, NormalizationForm.FormKD);
            if (kd != '\0')
            {
                baseChar = kd;
            }
        }

        return char.ToUpperInvariant(baseChar == '\0' ? upper : baseChar);

        static char FirstNonMark(char c, NormalizationForm form)
        {
            var normalized = c.ToString().Normalize(form);

            foreach (var ch in normalized)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (cat is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
                {
                    return ch;
                }
            }

            return '\0';
        }
    }
}
