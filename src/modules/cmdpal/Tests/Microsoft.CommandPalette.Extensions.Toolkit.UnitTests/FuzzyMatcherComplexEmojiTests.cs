// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

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
        Assert.IsTrue(result.Score > 0, "Expected partial match based on base emoji");

        // Should match the base emoji (2 chars)
        Assert.AreEqual(2, result.Positions.Count, "Expected match on base emoji only");
    }
}
