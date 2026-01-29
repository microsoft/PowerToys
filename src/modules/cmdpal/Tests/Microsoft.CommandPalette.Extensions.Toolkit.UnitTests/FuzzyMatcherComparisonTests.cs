// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions.Toolkit.UnitTests.Legacy;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FuzzyMatcherComparisonTests
{
    public static IEnumerable<object[]> TestData =>
        [
            ["a", "a"],
            ["a", "A"],
            ["A", "a"],
            ["abc", "abc"],
            ["abc", "axbycz"],
            ["abc", "abxcyz"],
            ["sln", "solution.sln"],
            ["vs", "visualstudio"],
            ["test", "Test"],
            ["pt", "PowerToys"],
            ["p/t", "power\\toys"],
            ["p\\t", "power/toys"],
            ["c/w", "c:\\windows"],
            ["foo", "bar"],
            ["verylongstringthatdoesnotmatch", "short"],
            [string.Empty, "anything"],
            ["something", string.Empty],
            ["git", "git"],
            ["em", "Emmy"],
            ["my", "Emmy"],
            ["word", "word"],
            ["wd", "word"],
            ["w d", "word"],
            ["a", "ba"],
            ["a", "ab"],
            ["a", "bab"],
            ["z", "abcdefg"],
            ["CC", "CamelCase"],
            ["cc", "camelCase"],
            ["cC", "camelCase"],
            ["some", "awesome"],
            ["some", "somewhere"],
            ["1", "1"],
            ["1", "2"],
            [".", "."],
            ["f.t", "file.txt"],
            ["excel", "Excel"],
            ["Excel", "excel"],
            ["PowerPoint", "Power Point"],
            ["power point", "PowerPoint"],
            ["visual studio code", "Visual Studio Code"],
            ["vsc", "Visual Studio Code"],
            ["code", "Visual Studio Code"],
            ["vs code", "Visual Studio Code"],
            ["word", "Microsoft Word"],
            ["ms word", "Microsoft Word"],
            ["browser", "Internet Explorer"],
            ["chrome", "Google Chrome"],
            ["edge", "Microsoft Edge"],
            ["term", "Windows Terminal"],
            ["cmd", "Command Prompt"],
            ["calc", "Calculator"],
            ["snipping", "Snipping Tool"],
            ["note", "Notepad"],
            ["file expl", "File Explorer"],
            ["settings", "Settings"],
            ["p t", "PowerToys"],
            ["p  t", "PowerToys"],
            [" v ", " Visual Studio "],
            [" a b ", " a b c d "],
            [string.Empty, string.Empty],
            [" ", " "],
            ["   ", " "],
            [" ", "abc"],
            ["abc", " "],
            ["   ", "   "],
            [" ", " a b "],
            ["sh", "ShangHai"],
            ["bj", "BeiJing"],
            ["bj", "北京"],
            ["sh", "上海"],
            ["nh", "你好"],
            ["bj", "Beijing"],
            ["hello", "你好"],
            ["nihao", "你好"],
            ["rmb", "人民币"],
            ["zwr", "中文"],
            ["zw", "中文"],
            ["fbr", "foobar"],
            ["w11", "windows 11"],
            ["pwr", "powershell"],
            ["vm", "void main"],
            ["ps", "PowerShell"],
            ["az", "Azure"],
            ["od", "onedrive"],
            ["gc", "google chrome"],
            ["ff", "firefox"],
            ["fs", "file_system"],
            ["pt", "power-toys"],
            ["jt", "json.test"],
            ["ps", "power shell"],
            ["ps", "power'shell"],
            ["ps", "power\"shell"],
            ["hw", "hello:world"],
            ["abc", "a_b_c"],
            ["abc", "a-b-c"],
            ["abc", "a.b.c"],
            ["abc", "a b c"],
            ["abc", "a'b'c"],
            ["abc", "a\"b\"c"],
            ["abc", "a:b:c"],
            ["_a", "_a"],
            ["a_", "a_"],
            ["-a", "-a"],
            ["a-", "a-"]
        ];

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void CompareScores(string needle, string haystack)
    {
        var legacyScore = LegacyFuzzyStringMatcher.ScoreFuzzy(needle, haystack);
        var newScore = FuzzyStringMatcher.ScoreFuzzy(needle, haystack);

        Assert.AreEqual(legacyScore, newScore, $"Score mismatch for needle='{needle}', haystack='{haystack}'");
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void ComparePositions(string needle, string haystack)
    {
        var (legacyScore, legacyPos) = LegacyFuzzyStringMatcher.ScoreFuzzyWithPositions(needle, haystack, true);
        var (newScore, newPos) = FuzzyStringMatcher.ScoreFuzzyWithPositions(needle, haystack, true);

        Assert.AreEqual(legacyScore, newScore, $"Score mismatch (with pos) for needle='{needle}', haystack='{haystack}'");

        // Ensure lists are not null
        legacyPos ??= [];
        newPos ??= [];

        // Compare list contents
        var legacyPosStr = string.Join(',', legacyPos);
        var newPosStr = string.Join(',', newPos);

        Assert.AreEqual(legacyPos.Count, newPos.Count, $"Position count mismatch: Legacy=[{legacyPosStr}], New=[{newPosStr}]");

        for (var i = 0; i < legacyPos.Count; i++)
        {
            Assert.AreEqual(legacyPos[i], newPos[i], $"Position mismatch at index {i}: Legacy=[{legacyPosStr}], New=[{newPosStr}]");
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void CompareScores_ContiguousOnly(string needle, string haystack)
    {
        var legacyScore = LegacyFuzzyStringMatcher.ScoreFuzzy(needle, haystack, allowNonContiguousMatches: false);
        var newScore = FuzzyStringMatcher.ScoreFuzzy(needle, haystack, allowNonContiguousMatches: false);

        Assert.AreEqual(legacyScore, newScore, $"Score mismatch (contiguous only) for needle='{needle}', haystack='{haystack}'");
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void CompareScores_PinyinEnabled(string needle, string haystack)
    {
        var originalNew = FuzzyStringMatcher.ChinesePinYinSupport;
        var originalLegacy = LegacyFuzzyStringMatcher.ChinesePinYinSupport;
        try
        {
            FuzzyStringMatcher.ChinesePinYinSupport = true;
            LegacyFuzzyStringMatcher.ChinesePinYinSupport = true;

            var legacyScore = LegacyFuzzyStringMatcher.ScoreFuzzy(needle, haystack);
            var newScore = FuzzyStringMatcher.ScoreFuzzy(needle, haystack);

            Assert.AreEqual(legacyScore, newScore, $"Score mismatch (Pinyin enabled) for needle='{needle}', haystack='{haystack}'");
        }
        finally
        {
            FuzzyStringMatcher.ChinesePinYinSupport = originalNew;
            LegacyFuzzyStringMatcher.ChinesePinYinSupport = originalLegacy;
        }
    }

    [TestMethod]
    [DynamicData(nameof(TestData))]
    public void ComparePositions_PinyinEnabled(string needle, string haystack)
    {
        var originalNew = FuzzyStringMatcher.ChinesePinYinSupport;
        var originalLegacy = LegacyFuzzyStringMatcher.ChinesePinYinSupport;
        try
        {
            FuzzyStringMatcher.ChinesePinYinSupport = true;
            LegacyFuzzyStringMatcher.ChinesePinYinSupport = true;

            var (legacyScore, legacyPos) = LegacyFuzzyStringMatcher.ScoreFuzzyWithPositions(needle, haystack, true);
            var (newScore, newPos) = FuzzyStringMatcher.ScoreFuzzyWithPositions(needle, haystack, true);

            Assert.AreEqual(legacyScore, newScore, $"Score mismatch (with pos, Pinyin enabled) for needle='{needle}', haystack='{haystack}'");

            // Ensure lists are not null
            legacyPos ??= [];
            newPos ??= [];

            // If newPos is empty but newScore > 0, it means it's a secondary match (like Pinyin)
            // which we don't return positions for in the new matcher.
            if (newScore > 0 && newPos.Count == 0 && legacyPos.Count > 0)
            {
                return;
            }

            // Compare list contents
            var legacyPosStr = string.Join(',', legacyPos);
            var newPosStr = string.Join(',', newPos);

            Assert.AreEqual(legacyPos.Count, newPos.Count, $"Position count mismatch: Legacy=[{legacyPosStr}], New=[{newPosStr}]");

            for (var i = 0; i < legacyPos.Count; i++)
            {
                Assert.AreEqual(legacyPos[i], newPos[i], $"Position mismatch at index {i}: Legacy=[{legacyPosStr}], New=[{newPosStr}]");
            }
        }
        finally
        {
            FuzzyStringMatcher.ChinesePinYinSupport = originalNew;
            LegacyFuzzyStringMatcher.ChinesePinYinSupport = originalLegacy;
        }
    }
}
