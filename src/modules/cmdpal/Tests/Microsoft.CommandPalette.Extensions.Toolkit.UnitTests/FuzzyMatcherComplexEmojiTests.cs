// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[OSCondition(OperatingSystems.Windows)]
[TestClass]
public sealed class FuzzyMatcherComplexEmojiTests
{
    [TestMethod]
    [Ignore("For now this is not supported")]
    public void Mismatch_DifferentSkinTone_PartialMatch()
    {
        // "👍🏻" (Light) vs "👍🏿" (Dark)
        // They share the base "👍".
        const string needle = "👍🏻";
        const string haystack = "👍🏿";

        var result = FuzzyStringMatcher.ScoreFuzzyWithPositions(needle, haystack, allowNonContiguousMatches: true);

        // Should have a positive score because of the base emoji match
        Assert.IsGreaterThan(0, result.Score, "Expected partial match based on base emoji");

        // Should match the base emoji (2 chars)
        Assert.HasCount(2, result.Positions, "Expected match on base emoji only");
    }
}
