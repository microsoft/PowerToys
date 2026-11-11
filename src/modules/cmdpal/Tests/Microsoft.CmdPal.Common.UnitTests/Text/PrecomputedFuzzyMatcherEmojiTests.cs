// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[Microsoft.VisualStudio.TestTools.UnitTesting.OSCondition(Microsoft.VisualStudio.TestTools.UnitTesting.OperatingSystems.Windows)]
[TestClass]
public sealed class PrecomputedFuzzyMatcherEmojiTests
{
    private readonly PrecomputedFuzzyMatcher _matcher = new();

    [TestMethod]
    public void ExactMatch_SimpleEmoji_ReturnsScore()
    {
        const string needle = "🚀";
        const string haystack = "Launch 🚀 sequence";

        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.IsGreaterThan(0, score, "Expected match for simple emoji");
    }

    [TestMethod]
    public void ExactMatch_SkinTone_ReturnsScore()
    {
        const string needle = "👍🏽"; // Medium skin tone
        const string haystack = "Thumbs up 👍🏽 here";

        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.IsGreaterThan(0, score, "Expected match for emoji with skin tone");
    }

    [TestMethod]
    public void ZWJSequence_Family_Match()
    {
        const string needle = "👨‍👩‍👧‍👦"; // Family: Man, Woman, Girl, Boy
        const string haystack = "Emoji 👨‍👩‍👧‍👦 Test";

        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.IsGreaterThan(0, score, "Expected match for ZWJ sequence");
    }

    [TestMethod]
    public void Flags_Match()
    {
        const string needle = "🇺🇸"; // US Flag (Regional Indicator U + Regional Indicator S)
        const string haystack = "USA 🇺🇸";

        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.IsGreaterThan(0, score, "Expected match for flag emoji");
    }

    [TestMethod]
    public void Emoji_MixedWithText_Search()
    {
        const string needle = "t🌮o"; // "t" + taco + "o"
        const string haystack = "taco 🌮 on tuesday";

        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.IsGreaterThan(0, score, "Expected match for emoji mixed with text");
    }
}
