// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Text;

public readonly struct FuzzyQuery
{
    public readonly string Original;

    public readonly string Folded;

    public readonly ulong Bloom;

    public readonly int EffectiveLength;

    public readonly bool IsAllLowercaseAsciiOrNonLetter;

    public readonly string? SecondaryOriginal;

    public readonly string? SecondaryFolded;

    public readonly ulong SecondaryBloom;

    public readonly int SecondaryEffectiveLength;

    public readonly bool SecondaryIsAllLowercaseAsciiOrNonLetter;

    public int Length => Folded.Length;

    public bool HasSecondary => SecondaryFolded is not null;

    public ReadOnlySpan<char> OriginalSpan => Original.AsSpan();

    public ReadOnlySpan<char> FoldedSpan => Folded.AsSpan();

    public ReadOnlySpan<char> SecondaryOriginalSpan => SecondaryOriginal.AsSpan();

    public ReadOnlySpan<char> SecondaryFoldedSpan => SecondaryFolded.AsSpan();

    public FuzzyQuery(
        string original,
        string folded,
        ulong bloom,
        int effectiveLength,
        bool isAllLowercaseAsciiOrNonLetter,
        string? secondaryOriginal = null,
        string? secondaryFolded = null,
        ulong secondaryBloom = 0,
        int secondaryEffectiveLength = 0,
        bool secondaryIsAllLowercaseAsciiOrNonLetter = true)
    {
        Original = original;
        Folded = folded;
        Bloom = bloom;
        EffectiveLength = effectiveLength;
        IsAllLowercaseAsciiOrNonLetter = isAllLowercaseAsciiOrNonLetter;

        SecondaryOriginal = secondaryOriginal;
        SecondaryFolded = secondaryFolded;
        SecondaryBloom = secondaryBloom;
        SecondaryEffectiveLength = secondaryEffectiveLength;
        SecondaryIsAllLowercaseAsciiOrNonLetter = secondaryIsAllLowercaseAsciiOrNonLetter;
    }
}
