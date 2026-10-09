// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FuzzyMatcherDiacriticsTests
{
    [TestMethod]
    public void ScoreFuzzy_WithDiacriticsRemoval_MatchesWithDiacritics()
    {
        // "eco" should match "école" when diacritics are removed (é -> E)
        var score = FuzzyStringMatcher.ScoreFuzzy("eco", "école", allowNonContiguousMatches: true, removeDiacritics: true);
        Assert.IsTrue(score > 0, "Should match 'école' with 'eco' when diacritics are removed");

        // "uber" should match "über"
        score = FuzzyStringMatcher.ScoreFuzzy("uber", "über", allowNonContiguousMatches: true, removeDiacritics: true);
        Assert.IsTrue(score > 0, "Should match 'über' with 'uber' when diacritics are removed");
    }

    [TestMethod]
    public void ScoreFuzzy_WithoutDiacriticsRemoval_DoesNotMatchWhenCharactersDiffer()
    {
        // "eco" should NOT match "école" if 'é' is treated as distinct from 'e' and order is strict
        // 'é' (index 0) != 'e'. 'e' (index 4) is after 'c' (index 1) and 'o' (index 2).
        // Since needle is "e-c-o", to match "école":
        // 'e' matches 'e' at 4.
        // 'c' must show up after. No.
        // So no match.
        var score = FuzzyStringMatcher.ScoreFuzzy("eco", "école", allowNonContiguousMatches: true, removeDiacritics: false);
        Assert.AreEqual(0, score, "Should not match 'école' with 'eco' when diacritics are NOT removed");

        // "uber" vs "über"
        // u != ü.
        // b (index 1) match b (index 2). e (2) match e (3). r (3) match r (4).
        // but 'u' has no match.
        score = FuzzyStringMatcher.ScoreFuzzy("uber", "über", allowNonContiguousMatches: true, removeDiacritics: false);
        Assert.AreEqual(0, score, "Should not match 'über' with 'uber' when diacritics are NOT removed");
    }

    [TestMethod]
    public void ScoreFuzzy_DefaultRemovesDiacritics()
    {
        // Now default is true, so "eco" vs "école" should match
        var score = FuzzyStringMatcher.ScoreFuzzy("eco", "école");
        Assert.IsTrue(score > 0, "Default should remove diacritics and match 'école'");
    }

    [DataTestMethod]
    [DataRow("a", "à", true)]
    [DataRow("e", "é", true)]
    [DataRow("i", "ï", true)]
    [DataRow("o", "ô", true)]
    [DataRow("u", "ü", true)]
    [DataRow("c", "ç", true)]
    [DataRow("n", "ñ", true)]
    [DataRow("s", "ß", false)] // ß doesn't strip to s via simple invalid-uppercasing
    public void VerifySpecificCharacters(string needle, string haystack, bool expectingMatch)
    {
        var score = FuzzyStringMatcher.ScoreFuzzy(needle, haystack, allowNonContiguousMatches: true, removeDiacritics: true);
        if (expectingMatch)
        {
            Assert.IsTrue(score > 0, $"Expected match for '{needle}' in '{haystack}' with diacritics removal");
        }
        else
        {
            Assert.AreEqual(0, score, $"Expected NO match for '{needle}' in '{haystack}' even with diacritics removal");
        }
    }

    [TestMethod]
    public void VerifyBothPathsWorkSameForASCII()
    {
        var needle = "test";
        var haystack = "TestString";

        var score1 = FuzzyStringMatcher.ScoreFuzzy(needle, haystack, allowNonContiguousMatches: true, removeDiacritics: true);
        var score2 = FuzzyStringMatcher.ScoreFuzzy(needle, haystack, allowNonContiguousMatches: true, removeDiacritics: false);

        Assert.AreEqual(score1, score2, "Scores should be identical for ASCII strings regardless of diacritics setting");
    }
}
