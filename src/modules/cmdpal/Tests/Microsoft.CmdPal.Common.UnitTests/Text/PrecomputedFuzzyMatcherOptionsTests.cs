// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[TestClass]
public sealed class PrecomputedFuzzyMatcherOptionsTests
{
    [TestMethod]
    public void Score_RemoveDiacriticsOption_AffectsMatching()
    {
        var withDiacriticsRemoved = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = true });
        var withoutDiacriticsRemoved = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = false });

        const string needle = "cafe";
        const string haystack = "CAFÉ";

        var scoreWithRemoval = withDiacriticsRemoved.Score(
            withDiacriticsRemoved.PrecomputeQuery(needle),
            withDiacriticsRemoved.PrecomputeTarget(haystack));
        var scoreWithoutRemoval = withoutDiacriticsRemoved.Score(
            withoutDiacriticsRemoved.PrecomputeQuery(needle),
            withoutDiacriticsRemoved.PrecomputeTarget(haystack));

        Assert.IsTrue(scoreWithRemoval > 0, "Expected match when diacritics are removed.");
        Assert.AreEqual(0, scoreWithoutRemoval, "Expected no match when diacritics are preserved.");
    }

    [TestMethod]
    public void Score_SkipWordSeparatorsOption_AffectsMatching()
    {
        var skipSeparators = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { SkipWordSeparators = true });
        var keepSeparators = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { SkipWordSeparators = false });

        const string needle = "a b";
        const string haystack = "ab";

        var scoreSkip = skipSeparators.Score(
            skipSeparators.PrecomputeQuery(needle),
            skipSeparators.PrecomputeTarget(haystack));
        var scoreKeep = keepSeparators.Score(
            keepSeparators.PrecomputeQuery(needle),
            keepSeparators.PrecomputeTarget(haystack));

        Assert.IsTrue(scoreSkip > 0, "Expected match when word separators are skipped.");
        Assert.AreEqual(0, scoreKeep, "Expected no match when word separators are preserved.");
    }

    [TestMethod]
    public void Score_IgnoreSameCaseBonusOption_AffectsLowercaseQuery()
    {
        var ignoreSameCase = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions
            {
                IgnoreSameCaseBonusIfQueryIsAllLowercase = true,
                SameCaseBonus = 10,
            });
        var applySameCase = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions
            {
                IgnoreSameCaseBonusIfQueryIsAllLowercase = false,
                SameCaseBonus = 10,
            });

        const string needle = "test";
        const string haystack = "test";

        var scoreIgnore = ignoreSameCase.Score(
            ignoreSameCase.PrecomputeQuery(needle),
            ignoreSameCase.PrecomputeTarget(haystack));
        var scoreApply = applySameCase.Score(
            applySameCase.PrecomputeQuery(needle),
            applySameCase.PrecomputeTarget(haystack));

        Assert.IsTrue(scoreApply > scoreIgnore, "Expected same-case bonus to apply when not ignored.");
    }
}
