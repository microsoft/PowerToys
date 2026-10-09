// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public sealed class FuzzyMatcherUnicodeGarbageTests
{
    [TestMethod]
    public void UnpairedHighSurrogateInNeedle_RemoveDiacritics_ShouldNotThrow()
    {
        const string needle = "\uD83D"; // high surrogate (unpaired)
        const string haystack = "abc";

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void UnpairedLowSurrogateInNeedle_RemoveDiacritics_ShouldNotThrow()
    {
        const string needle = "\uDC00"; // low surrogate (unpaired)
        const string haystack = "abc";

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void UnpairedHighSurrogateInHaystack_RemoveDiacritics_ShouldNotThrow()
    {
        const string needle = "a";
        const string haystack = "a\uD83D" + "bc"; // inject unpaired high surrogate

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void UnpairedLowSurrogateInHaystack_RemoveDiacritics_ShouldNotThrow()
    {
        const string needle = "a";
        const string haystack = "a\uDC00" + "bc"; // inject unpaired low surrogate

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void MixedSurrogatesAndMarks_RemoveDiacritics_ShouldNotThrow()
    {
        // "Garbage smoothie": unpaired surrogate + combining mark + emoji surrogate pair
        const string needle = "a\uD83D\u0301";         // 'a' + unpaired high surrogate + combining acute
        const string haystack = "a\u0301 \U0001F600";  // 'a' + combining acute + space + 😀 (valid pair)

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void ValidEmojiSurrogatePair_RemoveDiacritics_ShouldNotThrow_AndCanMatch()
    {
        // 😀 U+1F600 encoded as surrogate pair in UTF-16
        const string needle = "\U0001F600";
        const string haystack = "x \U0001F600 y";

        var result = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);

        // Keep assertions minimal: just ensure it doesn't act like "no match".
        // If your API returns score=0 for no match, this is stable.
        Assert.IsTrue(result.Score > 0, "Expected emoji to produce a match score > 0.");
        Assert.IsTrue(result.Positions.Count > 0, "Expected at least one matched position.");
    }

    [TestMethod]
    public void DiacriticStripping_StillWorks_OnBMPNonSurrogate()
    {
        // This is a regression guard: we fixed surrogates; don't break diacritic stripping.
        // "é" should fold like "e" when removeDiacritics=true.
        const string needle = "cafe";
        const string haystack = "CAFÉ";

        var withDiacriticsRemoved = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);

        var withoutDiacriticsRemoved = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: false);

        Assert.IsTrue(withDiacriticsRemoved.Score >= withoutDiacriticsRemoved.Score, "Removing diacritics should not make matching worse for 'CAFÉ' vs 'cafe'.");
        Assert.IsTrue(withDiacriticsRemoved.Score > 0, "Expected a match when diacritics are removed.");
    }

    [TestMethod]
    public void RandomUtf16Garbage_RemoveDiacritics_ShouldNotThrow()
    {
        // Deterministic pseudo-random "UTF-16 garbage", including surrogates.
        // This is a quick fuzz-lite test that’s stable across runs.
        var s1 = MakeDeterministicGarbage(seed: 1234, length: 512);
        var s2 = MakeDeterministicGarbage(seed: 5678, length: 1024);

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            s1,
            s2,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void RandomUtf16Garbage_NoDiacritics_ShouldNotThrow()
    {
        var s1 = MakeDeterministicGarbage(seed: 42, length: 512);
        var s2 = MakeDeterministicGarbage(seed: 43, length: 1024);

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            s1,
            s2,
            allowNonContiguousMatches: true,
            removeDiacritics: false);
    }

    [TestMethod]
    public void HighSurrogateAtEndOfHaystack_RemoveDiacritics_ShouldNotThrow()
    {
        const string needle = "a";
        const string haystack = "abc\uD83D"; // Ends with high surrogate

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void ComplexEmojiSequence_RemoveDiacritics_ShouldNotThrow()
    {
        // Family: Man, Woman, Girl, Boy
        // U+1F468 U+200D U+1F469 U+200D U+1F467 U+200D U+1F466
        const string needle = "\U0001F468";
        const string haystack = "Info: \U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466 family";

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    [TestMethod]
    public void NullOrEmptyInputs_ShouldNotThrow()
    {
        // Empty needle
        var result1 = FuzzyStringMatcher.ScoreFuzzyWithPositions(string.Empty, "abc", true, true);
        Assert.AreEqual(0, result1.Score);

        // Empty haystack
        var result2 = FuzzyStringMatcher.ScoreFuzzyWithPositions("abc", string.Empty, true, true);
        Assert.AreEqual(0, result2.Score);

        // Null haystack
        var result3 = FuzzyStringMatcher.ScoreFuzzyWithPositions("abc", null!, true, true);
        Assert.AreEqual(0, result3.Score);
    }

    [TestMethod]
    public void VeryLongStrings_ShouldNotThrow()
    {
        var needle = new string('a', 100);
        var haystack = new string('b', 10000) + needle + new string('c', 10000);

        _ = FuzzyStringMatcher.ScoreFuzzyWithPositions(
            needle,
            haystack,
            allowNonContiguousMatches: true,
            removeDiacritics: true);
    }

    private static string MakeDeterministicGarbage(int seed, int length)
    {
        // LCG for deterministic generation without Random’s platform/version surprises.
        var x = (uint)seed;
        var chars = length <= 2048 ? stackalloc char[length] : new char[length];

        for (var i = 0; i < chars.Length; i++)
        {
            // LCG: x = (a*x + c) mod 2^32
            x = unchecked((1664525u * x) + 1013904223u);

            // Take top 16 bits as UTF-16 code unit (includes surrogates).
            chars[i] = (char)(x >> 16);
        }

        return new string(chars);
    }
}
