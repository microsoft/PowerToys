// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;

namespace Microsoft.CmdPal.Core.Common.Text;

public readonly struct FuzzyQuery
{
    public readonly string Text;
    public readonly string Normalized;
    public readonly string Folded;
    public readonly string NormalizedNoSep;
    public readonly string FoldedNoSep;
    public readonly bool HasSeparators;
    public readonly ulong Bloom;

    // Optional secondary (e.g., PinYin)
    public readonly string? SecondaryNormalized;
    public readonly string? SecondaryFolded;
    public readonly ulong SecondaryBloom;

    public int Length => Normalized.Length;

    public bool HasSecondary => SecondaryFolded is not null;

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

    public ReadOnlySpan<char> NormalizedNoSepSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => NormalizedNoSep.AsSpan();
    }

    public ReadOnlySpan<char> FoldedNoSepSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => FoldedNoSep.AsSpan();
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

    public FuzzyQuery(
        string text,
        string normalized,
        string folded,
        string normalizedNoSep,
        string foldedNoSep,
        bool hasSeparators,
        ulong bloom,
        string? secondaryNormalized = null,
        string? secondaryFolded = null,
        ulong secondaryBloom = 0)
    {
        Text = text;
        Normalized = normalized;
        Folded = folded;
        NormalizedNoSep = normalizedNoSep;
        FoldedNoSep = foldedNoSep;
        HasSeparators = hasSeparators;
        Bloom = bloom;
        SecondaryNormalized = secondaryNormalized;
        SecondaryFolded = secondaryFolded;
        SecondaryBloom = secondaryBloom;
    }
}
