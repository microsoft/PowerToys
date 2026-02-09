// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.Common.Text;

namespace Microsoft.CmdPal.Common.UnitTests.Text;

[TestClass]
public sealed class PrecomputedFuzzyMatcherUnicodeTests
{
    private readonly PrecomputedFuzzyMatcher _defaultMatcher = new();

    [TestMethod]
    public void UnpairedHighSurrogateInNeedle_ShouldNotThrow()
    {
        const string needle = "\uD83D"; // high surrogate (unpaired)
        const string haystack = "abc";

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        _ = _defaultMatcher.Score(q, t);
    }

    [TestMethod]
    public void UnpairedLowSurrogateInNeedle_ShouldNotThrow()
    {
        const string needle = "\uDC00"; // low surrogate (unpaired)
        const string haystack = "abc";

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        _ = _defaultMatcher.Score(q, t);
    }

    [TestMethod]
    public void UnpairedHighSurrogateInHaystack_ShouldNotThrow()
    {
        const string needle = "a";
        const string haystack = "a\uD83D" + "bc"; // inject unpaired high surrogate

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        _ = _defaultMatcher.Score(q, t);
    }

    [TestMethod]
    public void MixedSurrogatesAndMarks_ShouldNotThrow()
    {
        // "Garbage smoothie": unpaired surrogate + combining mark + emoji surrogate pair
        const string needle = "a\uD83D\u0301";         // 'a' + unpaired high surrogate + combining acute
        const string haystack = "a\u0301 \U0001F600";  // 'a' + combining acute + space + 😀 (valid pair)

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        _ = _defaultMatcher.Score(q, t);
    }

    [TestMethod]
    public void ValidEmojiSurrogatePair_ShouldNotThrow_AndCanMatch()
    {
        // 😀 U+1F600 encoded as surrogate pair in UTF-16
        const string needle = "\U0001F600";
        const string haystack = "x \U0001F600 y";

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        var score = _defaultMatcher.Score(q, t);

        Assert.IsTrue(score > 0, "Expected emoji to produce a match score > 0.");
    }

    [TestMethod]
    public void RandomUtf16Garbage_ShouldNotThrow()
    {
        // Deterministic pseudo-random "UTF-16 garbage", including surrogates.
        var s1 = MakeDeterministicGarbage(seed: 1234, length: 512);
        var s2 = MakeDeterministicGarbage(seed: 5678, length: 1024);

        var q = _defaultMatcher.PrecomputeQuery(s1);
        var t = _defaultMatcher.PrecomputeTarget(s2);
        _ = _defaultMatcher.Score(q, t);
    }

    [TestMethod]
    public void HighSurrogateAtEndOfHaystack_ShouldNotThrow()
    {
        const string needle = "a";
        const string haystack = "abc\uD83D"; // Ends with high surrogate

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        _ = _defaultMatcher.Score(q, t);
    }

    [TestMethod]
    public void VeryLongStrings_ShouldNotThrow()
    {
        var needle = new string('a', 100);
        var haystack = new string('b', 10000) + needle + new string('c', 10000);

        var q = _defaultMatcher.PrecomputeQuery(needle);
        var t = _defaultMatcher.PrecomputeTarget(haystack);
        _ = _defaultMatcher.Score(q, t);
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
