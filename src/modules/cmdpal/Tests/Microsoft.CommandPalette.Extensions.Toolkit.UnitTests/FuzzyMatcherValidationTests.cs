// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FuzzyMatcherValidationTests
{
    [DataTestMethod]
    [DataRow(null, "haystack")]
    [DataRow("", "haystack")]
    [DataRow("needle", null)]
    [DataRow("needle", "")]
    [DataRow(null, null)]
    public void ScoreFuzzy_HandlesIncorrectInputs(string needle, string haystack)
    {
        Assert.AreEqual(0, FuzzyStringMatcher.ScoreFuzzy(needle!, haystack!));
        Assert.AreEqual(0, FuzzyStringMatcher.ScoreFuzzy(needle!, haystack!, allowNonContiguousMatches: true, removeDiacritics: true));
        Assert.AreEqual(0, FuzzyStringMatcher.ScoreFuzzy(needle!, haystack!, allowNonContiguousMatches: false, removeDiacritics: false));
    }

    [DataTestMethod]
    [DataRow(null, "haystack")]
    [DataRow("", "haystack")]
    [DataRow("needle", null)]
    [DataRow("needle", "")]
    [DataRow(null, null)]
    public void ScoreFuzzyWithPositions_HandlesIncorrectInputs(string needle, string haystack)
    {
        var (score1, pos1) = FuzzyStringMatcher.ScoreFuzzyWithPositions(needle!, haystack!, true);
        Assert.AreEqual(0, score1);
        Assert.IsNotNull(pos1);
        Assert.AreEqual(0, pos1.Count);

        var (score2, pos2) = FuzzyStringMatcher.ScoreFuzzyWithPositions(needle!, haystack!, allowNonContiguousMatches: true, removeDiacritics: true);
        Assert.AreEqual(0, score2);
        Assert.IsNotNull(pos2);
        Assert.AreEqual(0, pos2.Count);
    }
}
