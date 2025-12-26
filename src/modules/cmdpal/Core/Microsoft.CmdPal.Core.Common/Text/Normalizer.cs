// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name

namespace Microsoft.CmdPal.Core.Common.Text;

public sealed class Normalizer : INormalizer
{
    public string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        var normalized = input.Normalize(NormalizationForm.FormKD);
        if (ReferenceEquals(normalized, input))
        {
            return input;
        }

        var marks = 0;
        foreach (var ch in normalized)
        {
            if (IsIgnoredCategory(CharUnicodeInfo.GetUnicodeCategory(ch)))
            {
                marks++;
            }
        }

        if (marks == 0)
        {
            return normalized;
        }

        var newLen = normalized.Length - marks;

        return string.Create(newLen, normalized, static (dst, src) =>
        {
            var di = 0;
            foreach (var ch in src)
            {
                if (!IsIgnoredCategory(CharUnicodeInfo.GetUnicodeCategory(ch)))
                {
                    dst[di++] = ch;
                }
            }
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string FoldCase(string input)
    {
        // LOAD BEARING:
        // this must keep the length the same as input for DP lockstep logic.
        // We prefer full Unicode ToUpperInvariant when it is 1:1,
        // but if it changes length, fall back to fixed-length per-char folding.
        var upper = input.ToUpperInvariant();

        if (upper.Length == input.Length)
        {
            return upper;
        }

        // Safe fixed-length fallback (1 UTF-16 code unit -> 1 UTF-16 code unit).
        // Note: This intentionally does NOT perform length-expanding special casing.
        return string.Create(input.Length, input, static (dst, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = char.ToUpperInvariant(src[i]);
            }
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsIgnoredCategory(UnicodeCategory cat)
    {
        return cat is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }
}
