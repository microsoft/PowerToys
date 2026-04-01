// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FuzzyMatcherPinyinLogicTests
{
    [TestInitialize]
    public void Setup()
    {
        FuzzyStringMatcher.ChinesePinYinSupport = true;
        FuzzyStringMatcher.ClearCache();
    }

    [TestCleanup]
    public void Cleanup()
    {
        FuzzyStringMatcher.ChinesePinYinSupport = false; // Reset to default state
        FuzzyStringMatcher.ClearCache();
    }

    [DataTestMethod]
    [DataRow("bj", "北京")]
    [DataRow("sh", "上海")]
    [DataRow("nihao", "你好")]
    [DataRow("北京", "北京")]
    [DataRow("北京", "Beijing")]
    [DataRow("北", "北京")]
    [DataRow("你好", "nihao")]
    public void PinyinMatch_DataDriven(string needle, string haystack)
    {
        Assert.IsTrue(FuzzyStringMatcher.ScoreFuzzy(needle, haystack) > 0, $"Expected match for '{needle}' in '{haystack}'");
    }

    [TestMethod]
    public void PinyinPositions_ShouldBeEmpty()
    {
        var (score, positions) = FuzzyStringMatcher.ScoreFuzzyWithPositions("bj", "北京", true);
        Assert.IsTrue(score > 0);
        Assert.AreEqual(0, positions.Count);
    }
}
