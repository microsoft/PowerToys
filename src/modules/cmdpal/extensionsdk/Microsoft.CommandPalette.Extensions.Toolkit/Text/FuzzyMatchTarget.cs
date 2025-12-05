// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
///     Precomputed fuzzy match target. Create once per searchable item, reuse across queries.
/// </summary>
public readonly struct FuzzyMatchTarget
{
    /// <summary>
    ///     Original string for display and exact-match scoring.
    /// </summary>
    public readonly string Original;

    /// <summary>
    ///     Normalized string (uppercase, no diacritics, Katakana-normalized).
    /// </summary>
    public readonly string Normalized;

    /// <summary>
    ///     Bloom filter for quick character existence check.
    ///     If a query char's bit isn't set, the target can't match.
    /// </summary>
    public readonly ulong CharBloom;

    /// <summary>
    ///     Precomputed separator/boundary positions for scoring.
    ///     Bit N is set if position N is a word boundary.
    /// </summary>
    public readonly ulong BoundaryMask;

    public FuzzyMatchTarget(string value)
    {
        Original = value ?? string.Empty;
        Normalized = FuzzyStringMatcher.NormalizeString(Original);
        CharBloom = ComputeBloomFilter(Normalized);
        BoundaryMask = ComputeBoundaryMask(Original);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeBloomFilter(string normalized)
    {
        ulong bloom = 0;
        foreach (var c in normalized)
        {
            // Use multiple hash positions for better distribution
            bloom |= 1UL << (c & 63);
            bloom |= 1UL << ((c >> 6) & 63);
        }

        return bloom;
    }

    private static ulong ComputeBoundaryMask(string original)
    {
        if (original.Length == 0)
        {
            return 0;
        }

        ulong mask = 1; // Position 0 is always a boundary

        // Only track first 64 positions (sufficient for most strings)
        var limit = Math.Min(original.Length, 64);
        for (var i = 1; i < limit; i++)
        {
            var prev = original[i - 1];
            var curr = original[i];

            var isBoundary = IsSeparator(prev) ||
                             (char.IsLower(prev) && char.IsUpper(curr)) ||
                             (char.IsLetter(prev) && char.IsDigit(curr)) ||
                             (char.IsDigit(prev) && char.IsLetter(curr));

            if (isBoundary)
            {
                mask |= 1UL << i;
            }
        }

        return mask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSeparator(char c)
    {
        return c is '/' or '\\' or '_' or '-' or '.' or ' ' or '\'' or '"' or ':';
    }
}
