// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

/// <summary>
///     Precomputed fuzzy match query. Create once per search, reuse across targets.
/// </summary>
public readonly struct FuzzyMatchQuery
{
    public readonly string Original;
    public readonly string Normalized;
    public readonly ulong CharBloom;
    public readonly int Length;

    public FuzzyMatchQuery(string value)
    {
        Original = value ?? string.Empty;
        Normalized = FuzzyStringMatcher.NormalizeString(Original);
        CharBloom = ComputeBloomFilter(Normalized);
        Length = Original.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ComputeBloomFilter(string normalized)
    {
        ulong bloom = 0;
        foreach (var c in normalized)
        {
            bloom |= 1UL << (c & 63);
            bloom |= 1UL << ((c >> 6) & 63);
        }

        return bloom;
    }

    /// <summary>
    ///     Quick check if this query could possibly match the target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CouldMatch(in FuzzyMatchTarget target)
    {
        // Length check
        if (target.Normalized.Length < Length)
        {
            return false;
        }

        // Bloom filter: all query char bits must be present in target
        return (CharBloom & ~target.CharBloom) == 0;
    }
}
