// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class MatchingAndBoundaryTests
{
    // A supplementary-plane letter: two UTF-16 units, neither of them a letter on its own.
    private const string Adlam = "\U0001E922";

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
        Dictionary<string, string> store = SnippetStore.Parse(SnippetJson.Bytes("!Hi=first", "!HI=second"), caseSensitive: false);
        TriggerIndex index = TriggerIndex.Build(store.Keys, caseSensitive: false);

        Assert.AreEqual(1, store.Count);
        Assert.AreEqual("!HI", index.MatchLongestSuffix("typed !hi"));
        Assert.AreEqual("second", store["!hI"]);
    }

    [TestMethod]
    public void WordBoundaryClassifiesTerminatorsAndStartPositions()
    {
        Assert.IsTrue(WordBoundaryRules.IsWordCharacter('a'));
        Assert.IsTrue(WordBoundaryRules.IsWordCharacter('7'));
        Assert.IsTrue(WordBoundaryRules.IsWordCharacter('_'));
        Assert.IsTrue(WordBoundary.IsTerminator(' '));
        Assert.IsTrue(WordBoundary.IsTerminator('.'));
        Assert.IsFalse(WordBoundary.IsTerminator('a'));
        Assert.IsFalse(WordBoundary.IsTerminator('\0'));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("hello ".AsSpan(), "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary("hello".AsSpan(), "btw"));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("hello".AsSpan(), "#sig"));
    }

    [TestMethod]
    public void AnUnderscoreKeepsTheWordGoingInsteadOfReleasingATrigger()
    {
        // '_' is part of a word, yet it also released triggers: typing addr_line expanded addr as soon as
        // the underscore was typed, although my_addr was left alone.
        Assert.IsFalse(WordBoundary.IsTerminator('_'));

        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("addr=1 Main Street"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store);

        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = true;
            foreach (char ch in "addr_line my_addr_ addr")
            {
                Assert.IsFalse(hook.ProcessKey(Native.VK_PACKET, ch, ctrl: false, alt: false, shift: false, win: false), $"'{ch}'");
            }

            Assert.IsTrue(hook.ProcessKey(Native.VK_PACKET, '.', ctrl: false, alt: false, shift: false, win: false), "Punctuation still releases it.");
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual("addr", expansion.Trigger);
            Assert.AreEqual(0, hook.Pending.Count);
        }
        finally
        {
            WordBoundary.Required = original;
        }

        // The editor's "Never expands" check follows the same rule.
        var triggers = new TextExpanderTriggerSet(["addr", "addr_line"]);
        Assert.IsNull(TextExpanderTriggerReach.FindBlocker("addr_line", triggers, wordMode: true));
        Assert.AreEqual(("addr", '_'), TextExpanderTriggerReach.FindBlocker("addr_line", triggers, wordMode: false), "Without the wait, addr fires as it's typed.");
    }

    [TestMethod]
    public void AWordStartIsJudgedOnWholeCharacters()
    {
        // The character before a trigger was judged one UTF-16 unit at a time, so a combining accent, or the second half
        // of a supplementary letter, let btw expand inside a word, though é typed as one character didn't.
        Assert.IsTrue(WordBoundaryRules.IsWordCharacter('\u0301'));
        Assert.IsTrue(WordBoundaryRules.IsWordCharacter(Adlam[0]));
        Assert.IsTrue(WordBoundaryRules.IsWordCharacter(Adlam[1]));
        Assert.IsFalse(WordBoundaryRules.IsWordCharacter('\u2764'));

        // Whether a character ends a word is unchanged: a combining mark or half a pair never did.
        Assert.IsFalse(WordBoundary.IsTerminator('\u0301'));
        Assert.IsFalse(WordBoundary.IsTerminator(Adlam[1]));
        for (int value = 0; value <= char.MaxValue; value++)
        {
            char ch = (char)value;
            bool before = !(char.IsLetterOrDigit(ch) || ch == '_') && (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch));
            if (WordBoundary.IsTerminator(ch) != before)
            {
                Assert.Fail(Hex(ch.ToString()));
            }
        }

        Assert.IsFalse(WordBoundary.StartsAtWordBoundary("e\u0301", "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary(Adlam, "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary("\u00E9", "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary("a", Adlam + "x"));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("(", "btw"));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("\U0001F600", "btw"));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary("\u2764\uFE0F", "btw"), "A variation selector goes with its emoji.");
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary(" ", Adlam + "x"));

        // Half a pair, or marks with nothing before them, are taken for a word, so nothing expands.
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary(Adlam.AsSpan(1), "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary(Adlam.AsSpan(0, 1), "btw"));
        Assert.IsFalse(WordBoundary.StartsAtWordBoundary("\u0301", "btw"));
        Assert.IsTrue(WordBoundary.StartsAtWordBoundary(ReadOnlySpan<char>.Empty, "btw"));
    }

    [TestMethod]
    public void TheHookAndTheEditorAgreeOnWordStartsBeyondOneUtf16Unit()
    {
        string adlamTrigger = Adlam + "x";
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        File.WriteAllText(path, SnippetJson.Of("btw=by the way", adlamTrigger + "=adlam"));
        using var store = new SnippetStore(path);
        Assert.AreEqual(2, store.Load().Count);
        using var hook = new KeyboardHook(store);
        var triggers = new TextExpanderTriggerSet(["btw", adlamTrigger]);

        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = true;
            foreach ((string before, string trigger, bool expands) in new[]
            {
                ("e\u0301", "btw", false),
                (Adlam, "btw", false),
                ("\u00E9", "btw", false),
                ("a", adlamTrigger, false),
                (" ", "btw", true),
                ("(", "btw", true),
                ("\U0001F600", "btw", true),
                ("\u2764\uFE0F", "btw", true),
                (" ", adlamTrigger, true),
            })
            {
                string typed = before + trigger + " ";
                Assert.IsFalse(hook.ProcessKey(Native.VK_RETURN, 0, ctrl: false, alt: false, shift: false, win: false));
                Assert.AreEqual(expands, Type(hook, typed), Hex(typed));
                Assert.AreEqual(expands, hook.Pending.TryTake(out PendingExpansion expansion), Hex(typed));
                Assert.AreEqual(expands ? trigger : null, expansion.Trigger, Hex(typed));

                // The editor's "Never expands" check follows the same rule.
                Assert.AreEqual(expands ? (trigger, ' ') : null, TextExpanderTriggerReach.FindBlocker(typed, triggers, wordMode: true), Hex(typed));
            }
        }
        finally
        {
            WordBoundary.Required = original;
        }
    }

    [TestMethod]
    public void WordBoundaryDefaultsToWaitingForSpaceOrPunctuation()
    {
        Assert.IsTrue(WordBoundary.Required);
        Assert.IsTrue(WordBoundary.KeepTerminator);
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
        Dictionary<string, string> store = SnippetStore.Parse(SnippetJson.Bytes("btw=by the way", "#sig=Best regards"));
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
        Dictionary<string, string> store = SnippetStore.Parse(SnippetJson.Bytes("btw=by the way"));
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
        Dictionary<string, string> store = SnippetStore.Parse(SnippetJson.Bytes("btw=by the way"));
        var expansion = Simulate("btw", false, TriggerIndex.Build(store.Keys), store)!.Value;
        Assert.AreEqual("bt", expansion.Erased);
        Assert.AreEqual("by the way", expansion.Inserted);
    }

    [TestMethod]
    public void WithoutTheWordWaitATriggerThatExtendsAnotherNeverFires()
    {
        // Why Settings suggests !2mysig, not !mysig2, for a copy of !mysig: typing !mysig2
        // completes !mysig first, and the engine expands at once.
        Dictionary<string, string> store = SnippetStore.Parse(SnippetJson.Bytes("!mysig=Regards", "!mysig2=Copy", "!2mysig=Copy"));
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

    // Types text the way the touch keyboard does, one UTF-16 unit per key. Returns whether the last one fired a trigger.
    private static bool Type(KeyboardHook hook, string text)
    {
        bool fired = false;
        foreach (char ch in text)
        {
            fired = hook.ProcessKey(Native.VK_PACKET, ch, ctrl: false, alt: false, shift: false, win: false);
        }

        return fired;
    }

    private static string Hex(string text)
        => string.Join(" ", text.Select(c => ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture)));
}
