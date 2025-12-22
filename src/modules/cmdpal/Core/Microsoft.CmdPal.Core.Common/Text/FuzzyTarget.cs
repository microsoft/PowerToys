// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.Core.Common.Text;

public readonly struct FuzzyTarget
{
    public readonly string Normalized;

    public readonly string Folded;

    public readonly ulong Bloom;

    // Optional secondary (e.g., PinYin)
    public readonly string? SecondaryNormalized;

    public readonly string? SecondaryFolded;

    public readonly ulong SecondaryBloom;

    public int Length => Normalized.Length;

    public bool HasSecondary => SecondaryFolded is not null;

    public int SecondaryLength => SecondaryNormalized?.Length ?? 0;

    public ReadOnlySpan<char> NormalizedSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Normalized.AsSpan();
    }

    public ReadOnlySpan<char> FoldedSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Folded.AsSpan();
    }

    public ReadOnlySpan<char> SecondaryNormalizedSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SecondaryNormalized.AsSpan();
    }

    public ReadOnlySpan<char> SecondaryFoldedSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => SecondaryFolded.AsSpan();
    }

    public FuzzyTarget(
        string normalized,
        string folded,
        ulong bloom,
        string? secondaryNormalized = null,
        string? secondaryFolded = null,
        ulong secondaryBloom = 0)
    {
        Normalized = normalized;
        Folded = folded;
        Bloom = bloom;
        SecondaryNormalized = secondaryNormalized;
        SecondaryFolded = secondaryFolded;
        SecondaryBloom = secondaryBloom;
    }
}
