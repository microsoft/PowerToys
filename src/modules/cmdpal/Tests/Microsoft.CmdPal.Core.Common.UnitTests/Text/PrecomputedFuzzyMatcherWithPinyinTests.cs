// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.CmdPal.Core.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[TestClass]
public class PrecomputedFuzzyMatcherWithPinyinTests
{
    private PrecomputedFuzzyMatcherWithPinyin CreateMatcher(PinyinMode mode = PinyinMode.On, bool removeApostrophes = true)
    {
        return new PrecomputedFuzzyMatcherWithPinyin(
            new PrecomputedFuzzyMatcherOptions(),
            new PinyinFuzzyMatcherOptions { Mode = mode, RemoveApostrophesForQuery = removeApostrophes },
            new StringFolder(),
            new BloomFilter());
    }

    [TestMethod]
    [DataRow("bj", "北京")]
    [DataRow("sh", "上海")]
    [DataRow("nihao", "你好")]
    [DataRow("beijing", "北京")]
    [DataRow("ce", "测试")]
    public void Score_PinyinMatches_ShouldHavePositiveScore(string needle, string haystack)
    {
        var matcher = CreateMatcher(PinyinMode.On);
        var query = matcher.PrecomputeQuery(needle);
        var target = matcher.PrecomputeTarget(haystack);
        var score = matcher.Score(query, target);

        Assert.IsTrue(score > 0, $"Expected positive score for needle='{needle}', haystack='{haystack}'");
    }

    [TestMethod]
    public void Score_PinyinOff_ShouldNotMatchPinyin()
    {
        var matcher = CreateMatcher(PinyinMode.Off);
        var needle = "bj";
        var haystack = "北京";

        var query = matcher.PrecomputeQuery(needle);
        var target = matcher.PrecomputeTarget(haystack);
        var score = matcher.Score(query, target);

        Assert.AreEqual(0, score, "Pinyin match should be disabled.");
    }

    [TestMethod]
    public void Score_StandardMatch_WorksWithPinyinMatcher()
    {
        var matcher = CreateMatcher(PinyinMode.On);
        var needle = "abc";
        var haystack = "abc";

        var query = matcher.PrecomputeQuery(needle);
        var target = matcher.PrecomputeTarget(haystack);
        var score = matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Standard match should still work.");
    }

    [TestMethod]
    public void Score_ApostropheRemoval_Works()
    {
        var matcher = CreateMatcher(PinyinMode.On, removeApostrophes: true);
        var needle = "xi'an";

        // "xi'an" -> "xian" -> matches "西安" (Xi An)
        var haystack = "西安";

        var query = matcher.PrecomputeQuery(needle);
        var target = matcher.PrecomputeTarget(haystack);
        var score = matcher.Score(query, target);

        Assert.IsTrue(score > 0, "Expected match for 'xi'an' -> '西安' with apostrophe removal.");
    }

    [TestMethod]
    public void AutoMode_EnablesForChineseCulture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("zh-CN");
            var matcher = CreateMatcher(PinyinMode.AutoSimplifiedChineseUi);

            var score = matcher.Score(matcher.PrecomputeQuery("bj"), matcher.PrecomputeTarget("北京"));
            Assert.IsTrue(score > 0, "Should match when UI culture is zh-CN");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [TestMethod]
    public void AutoMode_DisablesForNonChineseCulture()
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var matcher = CreateMatcher(PinyinMode.AutoSimplifiedChineseUi);

            var score = matcher.Score(matcher.PrecomputeQuery("bj"), matcher.PrecomputeTarget("北京"));
            Assert.AreEqual(0, score, "Should NOT match when UI culture is en-US");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}
