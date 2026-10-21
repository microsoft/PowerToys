// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class MatchingAndBoundaryTests
{
    [TestMethod]
    public void TriggerIndexReturnsLongestSuffixInBoundedLookup()
    {
        Assert.IsNull(TriggerIndex.Empty.MatchLongestSuffix("anything"));
        TriggerIndex index = TriggerIndex.Build([":a", ":ab", "x:ab", ":b"]);
        Assert.AreEqual("x:ab", index.MatchLongestSuffix("hello x:ab"));
        Assert.AreEqual(":ab", index.MatchLongestSuffix("hello y:ab"));
        Assert.IsNull(index.MatchLongestSuffix(":ab then more"));
        Assert.AreEqual(4, index.MaxTriggerLength);
        Assert.AreEqual(4, index.Count);
    }

    [TestMethod]
    public void CaseInsensitiveIndexMatchesAnyCapitalizationAndKeepsLongestMatch()
    {
        TriggerIndex index = TriggerIndex.Build(["!Hi", "!HiThere"], caseSensitive: false);

        Assert.AreEqual("!Hi", index.MatchLongestSuffix("typed !hI"));
        Assert.AreEqual("!HiThere", index.MatchLongestSuffix("typed !HITHERE"));
        Assert.IsNull(index.MatchLongestSuffix("typed !hello"));
    }

    [TestMethod]
    public void CaseInsensitiveFileTreatsCaseOnlyDuplicatesAsLastRowWins()
    {
        Dictionary<string, string> store = SnippetStore.Parse(["!Hi=first", "!HI=second"], caseSensitive: false);
        TriggerIndex index = TriggerIndex.Build(store.Keys, caseSensitive: false);

        Assert.AreEqual(1, store.Count);
        Assert.AreEqual("!HI", index.MatchLongestSuffix("typed !hi"));
        Assert.AreEqual("second", store["!hI"]);
    }

    [TestMethod]
    public void WordBoundaryClassifiesTerminatorsAndStartPositions()
    {
        Assert.IsTrue(WordBoundary.IsWordCharacter('a'));
        Assert.IsTrue(WordBoundary.IsWordCharacter('7'));
        Assert.IsTrue(WordBoundary.IsWordCharacter('_'));
        Assert.IsTrue(WordBoundary.IsTerminator(' '));
        Assert.IsTrue(WordBoundary.IsTerminator('.'));
        Assert.IsFalse(WordBoundary.IsTerminator('a'));
        Assert.IsFalse(WordBoundary.IsTerminator('\0'));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("hello ".AsSpan(), "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary("hello".AsSpan(), "btw"));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("hello".AsSpan(), "#sig"));
    }

    [TestMethod]
    public void TriggerIndexAgreesWithBruteForceForManySharedPrefixes()
    {
        const int count = 1000;
        string prefix = ":" + new string('a', 30);
        string[] triggers = [.. Enumerable.Range(0, count).Select(i => prefix + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture))];
        TriggerIndex index = TriggerIndex.Build(triggers);
        string[] longestFirst = [.. triggers.OrderByDescending(t => t.Length)];
        var rng = new Random(20260916);
        for (int i = 0; i < 200; i++)
        {
            string tail = rng.Next(3) == 0 ? "typed " + triggers[rng.Next(count)] : prefix + rng.Next(999999).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
            Assert.AreEqual(BruteForce(longestFirst, tail), index.MatchLongestSuffix(tail));
        }

        static string? BruteForce(string[] triggers, string tail)
        {
            foreach (string trigger in triggers)
            {
                if (tail.EndsWith(trigger, StringComparison.Ordinal))
                {
                    return trigger;
                }
            }

            return null;
        }
    }

    [TestMethod]
    public void WordBoundaryModeWaitsForTerminatorAndKeepsItByDefault()
    {
        Dictionary<string, string> store = SnippetStore.Parse(["btw=by the way", "#sig=Best regards"]);
        TriggerIndex index = TriggerIndex.Build(store.Keys);
        Assert.IsNull(Simulate("btw", true, index, store));
        Assert.AreEqual("by the way ", Simulate("btw ", true, index, store)!.Value.Inserted);
        Assert.AreEqual("by the way.", Simulate("btw.", true, index, store)!.Value.Inserted);
        Assert.IsNull(Simulate("abtw ", true, index, store));
        Assert.AreEqual("Best regards ", Simulate("x#sig ", true, index, store)!.Value.Inserted);
    }

    [TestMethod]
    public void WordBoundaryCanDropTerminatorWithoutChangingErasedTrigger()
    {
        Dictionary<string, string> store = SnippetStore.Parse(["btw=by the way"]);
        TriggerIndex index = TriggerIndex.Build(store.Keys);
        bool original = WordBoundary.KeepTerminator;
        try
        {
            WordBoundary.KeepTerminator = false;
            var expansion = Simulate("btw ", true, index, store)!.Value;
            Assert.AreEqual("btw", expansion.Erased);
            Assert.AreEqual("by the way", expansion.Inserted);
        }
        finally
        {
            WordBoundary.KeepTerminator = original;
        }
    }

    [TestMethod]
    public void InstantModeSwallowsLastKeystroke()
    {
        Dictionary<string, string> store = SnippetStore.Parse(["btw=by the way"]);
        var expansion = Simulate("btw", false, TriggerIndex.Build(store.Keys), store)!.Value;
        Assert.AreEqual("bt", expansion.Erased);
        Assert.AreEqual("by the way", expansion.Inserted);
    }

    [TestMethod]
    public void WithoutTheWordWaitATriggerThatExtendsAnotherNeverFires()
    {
        // Why Settings suggests !2mysig, not !mysig2, for a copy of !mysig: typing !mysig2
        // completes !mysig first, and the engine expands at once.
        Dictionary<string, string> store = SnippetStore.Parse(["!mysig=Regards", "!mysig2=Copy", "!2mysig=Copy"]);
        TriggerIndex index = TriggerIndex.Build(store.Keys);
        Assert.AreEqual("!mysig", FirstExpansion("!mysig2", index, store));
        Assert.AreEqual("!2mysig", FirstExpansion("!2mysig", index, store));
    }

    [TestMethod]
    public void TheIndexKeepsASingleChildWithoutADictionary()
    {
        // PERF-7: every node with a child had a dictionary of its own, nearly always holding one
        // entry; about 18 MB for these 10,000 triggers.
        string[] triggers = Enumerable.Range(0, 10_000).Select(i => "!snip_" + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        _ = TriggerIndex.Build(["warm", "up"]);

        long before = GC.GetAllocatedBytesForCurrentThread();
        TriggerIndex index = TriggerIndex.Build(triggers);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual(10_000, index.Count);
        Assert.AreEqual("!snip_04711", index.MatchLongestSuffix("typed !snip_04711"));
        Assert.IsNull(index.MatchLongestSuffix("snip_04711"));
        Assert.IsTrue(allocated < 8_000_000, $"{allocated} bytes");

        // Branching keeps every path: one child, then a second, then a third.
        TriggerIndex branching = TriggerIndex.Build(["ab", "cb", "db", "b"]);
        Assert.AreEqual("ab", branching.MatchLongestSuffix("xab"));
        Assert.AreEqual("cb", branching.MatchLongestSuffix("xcb"));
        Assert.AreEqual("db", branching.MatchLongestSuffix("xdb"));
        Assert.AreEqual("b", branching.MatchLongestSuffix("xeb"));
    }

    [TestMethod]
    public void WordBoundaryToggleRoundTrips()
    {
        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = true;
            Assert.IsTrue(WordBoundary.Required);
            WordBoundary.Required = false;
            Assert.IsFalse(WordBoundary.Required);
        }
        finally
        {
            WordBoundary.Required = original;
        }
    }

    private static string? FirstExpansion(string typed, TriggerIndex index, Dictionary<string, string> store)
    {
        for (int length = 1; length <= typed.Length; length++)
        {
            if (Simulate(typed[..length], false, index, store) is { } expansion)
            {
                return expansion.Trigger;
            }
        }

        return null;
    }

    private static (string Trigger, string Erased, string Inserted)? Simulate(string typed, bool wordMode, TriggerIndex index, Dictionary<string, string> store)
    {
        char ch = typed[^1];
        int usable = wordMode ? typed.Length - 1 : typed.Length;
        if (wordMode && (!WordBoundary.IsTerminator(ch) || usable <= 0))
        {
            return null;
        }

        string? trigger = index.MatchLongestSuffix(typed.AsSpan(0, usable));
        if (trigger is null)
        {
            return null;
        }

        if (wordMode && !WordBoundary.StartsAtWordBoundary(typed.AsSpan(0, usable - trigger.Length), trigger))
        {
            return null;
        }

        string replacement = store[trigger];
        return (trigger, wordMode ? trigger : trigger[..^1], wordMode && WordBoundary.KeepTerminator ? replacement + ch : replacement);
    }
}
