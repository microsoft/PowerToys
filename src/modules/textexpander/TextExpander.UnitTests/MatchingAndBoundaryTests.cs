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
