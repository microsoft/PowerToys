// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Common.Text;

public sealed class PinyinFuzzyMatcherOptions
{
    public PinyinMode Mode { get; init; } = PinyinMode.AutoSimplifiedChineseUi;

    /// <summary>Remove IME syllable separators (') for query secondary variant.</summary>
    public bool RemoveApostrophesForQuery { get; init; } = true;
}
