// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[TestClass]
public class PrecomputedFuzzyMatcherTests
{
    private readonly PrecomputedFuzzyMatcher _matcher = new();

    public static IEnumerable<object[]> MatchData =>
        [
            ["a", "a"],
            ["abc", "abc"],
            ["a", "ab"],
            ["b", "ab"],
            ["abc", "axbycz"],
            ["pt", "PowerToys"],
            ["calc", "Calculator"],
            ["vs", "Visual Studio"],
            ["code", "Visual Studio Code"],

            // Diacritics
            ["abc", "ÁBC"],

            // Separators
            ["p/t", "power\\toys"],
        ];

    public static IEnumerable<object[]> NonMatchData =>
        [
            ["z", "abc"],
            ["verylongstring", "short"],
        ];

    [TestMethod]
    [DynamicData(nameof(MatchData))]
    public void Score_Matches_ShouldHavePositiveScore(string needle, string haystack)
    {
        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, $"Expected positive score for needle='{needle}', haystack='{haystack}'");
    }

    [TestMethod]
    [DynamicData(nameof(NonMatchData))]
    public void Score_NonMatches_ShouldHaveZeroScore(string needle, string haystack)
    {
        var query = _matcher.PrecomputeQuery(needle);
        var target = _matcher.PrecomputeTarget(haystack);
        var score = _matcher.Score(query, target);

        Assert.AreEqual(0, score, $"Expected 0 score for needle='{needle}', haystack='{haystack}'");
    }

    [TestMethod]
    public void Score_EmptyQuery_ReturnsZero()
    {
        var query = _matcher.PrecomputeQuery(string.Empty);
        var target = _matcher.PrecomputeTarget("something");
        Assert.AreEqual(0, _matcher.Score(query, target));
    }

    [TestMethod]
    public void Score_EmptyTarget_ReturnsZero()
    {
        var query = _matcher.PrecomputeQuery("something");
        var target = _matcher.PrecomputeTarget(string.Empty);
        Assert.AreEqual(0, _matcher.Score(query, target));
    }

    [TestMethod]
    public void SchemaId_DefaultMatcher_IsConsistent()
    {
        var matcher1 = new PrecomputedFuzzyMatcher();
        var matcher2 = new PrecomputedFuzzyMatcher();

        Assert.AreEqual(matcher1.SchemaId, matcher2.SchemaId, "Default matchers should have the same SchemaId");
    }

    [TestMethod]
    public void SchemaId_SameOptions_ProducesSameId()
    {
        var options = new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = true };
        var matcher1 = new PrecomputedFuzzyMatcher(options);
        var matcher2 = new PrecomputedFuzzyMatcher(options);

        Assert.AreEqual(matcher1.SchemaId, matcher2.SchemaId, "Matchers with same options should have the same SchemaId");
    }

    [TestMethod]
    public void SchemaId_DifferentRemoveDiacriticsOption_ProducesDifferentId()
    {
        var matcherWithDiacriticsRemoval = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = true });
        var matcherWithoutDiacriticsRemoval = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { RemoveDiacritics = false });

        Assert.AreNotEqual(
            matcherWithDiacriticsRemoval.SchemaId,
            matcherWithoutDiacriticsRemoval.SchemaId,
            "Different RemoveDiacritics option should produce different SchemaId");
    }

    [TestMethod]
    public void SchemaId_ScoringOptionsDoNotAffectId()
    {
        // SchemaId should only be affected by options that affect folding/bloom, not scoring
        var matcher1 = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { CharMatchBonus = 1, CamelCaseBonus = 2 });
        var matcher2 = new PrecomputedFuzzyMatcher(
            new PrecomputedFuzzyMatcherOptions { CharMatchBonus = 100, CamelCaseBonus = 200 });

        Assert.AreEqual(matcher1.SchemaId, matcher2.SchemaId, "Scoring options should not affect SchemaId");
    }

    [TestMethod]
    public void Score_WordSeparatorMatching_PowerPoint()
    {
        // Test that "Power Point" can match "PowerPoint" when word separators are skipped
        var query = _matcher.PrecomputeQuery("Power Point");
        var target = _matcher.PrecomputeTarget("PowerPoint");
        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected 'Power Point' to match 'PowerPoint'");
    }

    [TestMethod]
    public void Score_WordSeparatorMatching_UnderscoreDash()
    {
        // Test that different word separators match each other
        var query = _matcher.PrecomputeQuery("hello_world");
        var target = _matcher.PrecomputeTarget("hello-world");
        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected 'hello_world' to match 'hello-world'");
    }

    [TestMethod]
    public void Score_WordSeparatorMatching_MixedSeparators()
    {
        // Test multiple different separators
        var query = _matcher.PrecomputeQuery("my.file_name");
        var target = _matcher.PrecomputeTarget("my-file.name");
        var score = _matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected mixed separators to match");
    }

    [TestMethod]
    public void Score_PrecomputedQueryReuse_ShouldWorkConsistently()
    {
        // Test that precomputed query can be reused across multiple targets
        var query = _matcher.PrecomputeQuery("test");
        var target1 = _matcher.PrecomputeTarget("test123");
        var target2 = _matcher.PrecomputeTarget("mytest");
        var target3 = _matcher.PrecomputeTarget("unrelated");

        var score1 = _matcher.Score(query, target1);
        var score2 = _matcher.Score(query, target2);
        var score3 = _matcher.Score(query, target3);

        Assert.IsTrue(score1 > 0, "Expected query to match first target");
        Assert.IsTrue(score2 > 0, "Expected query to match second target");
        Assert.AreEqual(0, score3, "Expected query not to match third target");
    }

    [TestMethod]
    public void Score_PrecomputedTargetReuse_ShouldWorkConsistently()
    {
        // Test that precomputed target can be reused across multiple queries
        var target = _matcher.PrecomputeTarget("calculator");
        var query1 = _matcher.PrecomputeQuery("calc");
        var query2 = _matcher.PrecomputeQuery("lator");
        var query3 = _matcher.PrecomputeQuery("xyz");

        var score1 = _matcher.Score(query1, target);
        var score2 = _matcher.Score(query2, target);
        var score3 = _matcher.Score(query3, target);

        Assert.IsTrue(score1 > 0, "Expected first query to match target");
        Assert.IsTrue(score2 > 0, "Expected second query to match target");
        Assert.AreEqual(0, score3, "Expected third query not to match target");
    }

    [TestMethod]
    public void Score_CaseInsensitiveMatching_Works()
    {
        // Test various case combinations
        var query1 = _matcher.PrecomputeQuery("test");
        var query2 = _matcher.PrecomputeQuery("TEST");
        var query3 = _matcher.PrecomputeQuery("TeSt");

        var target = _matcher.PrecomputeTarget("TestFile");

        var score1 = _matcher.Score(query1, target);
        var score2 = _matcher.Score(query2, target);
        var score3 = _matcher.Score(query3, target);

        Assert.IsTrue(score1 > 0, "Expected lowercase query to match");
        Assert.IsTrue(score2 > 0, "Expected uppercase query to match");
        Assert.IsTrue(score3 > 0, "Expected mixed case query to match");
    }
}
