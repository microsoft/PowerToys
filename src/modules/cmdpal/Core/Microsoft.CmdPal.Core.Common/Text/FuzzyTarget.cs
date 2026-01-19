// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Core.Common.Text;

public readonly struct FuzzyTarget
{
    public readonly string Original;
    public readonly string Folded;
    public readonly ulong Bloom;

    public readonly string? SecondaryOriginal;
    public readonly string? SecondaryFolded;
    public readonly ulong SecondaryBloom;

    public int Length => Folded.Length;

    public bool HasSecondary => SecondaryFolded is not null;

    public int SecondaryLength => SecondaryFolded?.Length ?? 0;

    public ReadOnlySpan<char> OriginalSpan => Original.AsSpan();

    public ReadOnlySpan<char> FoldedSpan => Folded.AsSpan();

    public ReadOnlySpan<char> SecondaryOriginalSpan => SecondaryOriginal.AsSpan();

    public ReadOnlySpan<char> SecondaryFoldedSpan => SecondaryFolded.AsSpan();

    public FuzzyTarget(
        string original,
        string folded,
        ulong bloom,
        string? secondaryOriginal = null,
        string? secondaryFolded = null,
        ulong secondaryBloom = 0)
    {
        Original = original;
        Folded = folded;
        Bloom = bloom;
        SecondaryOriginal = secondaryOriginal;
        SecondaryFolded = secondaryFolded;
        SecondaryBloom = secondaryBloom;
    }
}
