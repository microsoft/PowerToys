// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>
/// Ignoring case, the index has to agree with the replacement map's comparer,
/// <see cref="StringComparer.OrdinalIgnoreCase"/>, which cases a surrogate pair as one character.
/// </summary>
[TestClass]
public sealed class TriggerIndexScalarCaseTests
{
    private const string AdlamCapitalAlif = "\U0001E900";
    private const string AdlamSmallAlif = "\U0001E922";
    private const string DeseretCapitalLongI = "\U00010400";
    private const string DeseretSmallLongI = "\U00010428";

    [TestMethod]
    public void AnUppercaseSupplementaryLetterMatchesItsLowercaseTrigger()
    {
        // Neither half of the pair has a case, but the pair does.
        Assert.IsTrue(string.Equals(AdlamCapitalAlif, AdlamSmallAlif, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(AdlamSmallAlif[1], char.ToUpperInvariant(AdlamSmallAlif[1]));

        string deseret = "!" + DeseretSmallLongI + "x";
        TriggerIndex index = TriggerIndex.Build([AdlamSmallAlif, deseret], caseSensitive: false);
        Assert.AreEqual(AdlamSmallAlif, index.MatchLongestSuffix(AdlamCapitalAlif));
        Assert.AreEqual(AdlamSmallAlif, index.MatchLongestSuffix("typed " + AdlamCapitalAlif));
        Assert.AreEqual(deseret, index.MatchLongestSuffix("typed !" + DeseretCapitalLongI + "X"));

        // Half a pair is not a character, at either end of what the hook passes.
        Assert.IsNull(index.MatchLongestSuffix(AdlamCapitalAlif.AsSpan(0, 1)));
        Assert.IsNull(index.MatchLongestSuffix(AdlamCapitalAlif.AsSpan(1)));

        Assert.IsNull(TriggerIndex.Build([AdlamSmallAlif]).MatchLongestSuffix(AdlamCapitalAlif));
    }

    [TestMethod]
    public void IgnoringCaseMatchesWhatOrdinalIgnoreCaseEquates()
    {
        // Every character with another case form, and pairs that invariant casing gets wrong: long s
        // has S as its uppercase form yet is not equal to it, and Garay has case only in .NET's own tables.
        var pairs = new List<(string Trigger, string Typed)>
        {
            (AdlamSmallAlif, AdlamCapitalAlif),
            (DeseretCapitalLongI, DeseretSmallLongI),
            ("s", "\u017F"),
            ("S", "\u017F"),
            ("\u212A", "k"),
            ("\U00010D50", "\U00010D70"),
            ("\U00010D70", "\U00010D50"),
        };
        for (int value = 0; value <= 0x10FFFF; value++)
        {
            if (!Rune.TryCreate(value, out Rune rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.OtherNotAssigned or UnicodeCategory.PrivateUse)
            {
                continue;
            }

            foreach (Rune other in new[] { Rune.ToUpperInvariant(rune), Rune.ToLowerInvariant(rune) })
            {
                if (other != rune)
                {
                    pairs.Add((rune.ToString(), other.ToString()));
                }
            }
        }

        var mismatches = new List<string>();
        foreach ((string trigger, string typed) in pairs)
        {
            string? expected = string.Equals(trigger, typed, StringComparison.OrdinalIgnoreCase) ? trigger : null;
            if (TriggerIndex.Build([trigger], caseSensitive: false).MatchLongestSuffix("typed " + typed) != expected)
            {
                mismatches.Add(Hex(trigger) + " typed as " + Hex(typed));
            }
        }

        Assert.AreEqual(0, mismatches.Count, string.Join(", ", mismatches.Take(20)));

        // Whole triggers, against the longest-first EndsWith scan the index replaced.
        string[] pieces = ["a", "A", "!", "s", "S", "\u017F", "\u00E9", "\u00C9", "\U0001F600", AdlamSmallAlif, AdlamCapitalAlif, DeseretSmallLongI, DeseretCapitalLongI];
        var random = new Random(20261028);
        var triggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (triggers.Count < 80)
        {
            triggers.Add(Join(random, pieces, 2, 4));
        }

        TriggerIndex index = TriggerIndex.Build(triggers, caseSensitive: false);
        string[] longestFirst = [.. triggers.OrderByDescending(trigger => trigger.Length)];
        for (int i = 0; i < 5000; i++)
        {
            string tail = Join(random, pieces, 1, 8);
            string? expected = longestFirst.FirstOrDefault(trigger => tail.EndsWith(trigger, StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(expected, index.MatchLongestSuffix(tail), Hex(tail));
        }
    }

    [TestMethod]
    public void TheHookExpandsASupplementaryTriggerTypedInTheOtherCase()
    {
        using ScratchDirectory scratch = ScratchDirectory.Create();
        string path = Path.Combine(scratch.Path, "snippets.json");
        string trigger = "a" + AdlamSmallAlif;
        File.WriteAllText(path, SnippetJson.Of(trigger + "=adlam"));
        using var store = new SnippetStore(path, caseSensitive: false);
        Assert.AreEqual(1, store.Load().Count);
        using var hook = new KeyboardHook(store);

        bool original = WordBoundary.Required;
        try
        {
            WordBoundary.Required = false;

            // The tail the hook passes is only as long as the trigger, so here it starts inside a pair.
            Assert.IsFalse(Type(hook, AdlamCapitalAlif + AdlamCapitalAlif));
            Assert.AreEqual(0, hook.Pending.Count);

            // Touch keyboards and the emoji panel send each half of a pair as its own key.
            Assert.IsFalse(Type(hook, "A" + AdlamCapitalAlif[0]));
            Assert.IsTrue(Type(hook, AdlamCapitalAlif[1].ToString()));
            Assert.IsTrue(hook.Pending.TryTake(out PendingExpansion expansion));
            Assert.AreEqual(trigger, expansion.Trigger);

            WordBoundary.Required = true;
            Assert.IsFalse(Type(hook, " A" + AdlamCapitalAlif));
            Assert.IsTrue(Type(hook, "."));
            Assert.IsTrue(hook.Pending.TryTake(out expansion));
            Assert.AreEqual(trigger, expansion.Trigger);
        }
        finally
        {
            WordBoundary.Required = original;
        }
    }

    [TestMethod]
    public void IgnoringCaseMatchingAllocatesNothing()
    {
        TriggerIndex index = TriggerIndex.Build(["!" + AdlamSmallAlif, "!" + DeseretCapitalLongI + "x", "btw", "caf\u00E9"], caseSensitive: false);
        string[] tails = ["typed !" + AdlamCapitalAlif, "typed !" + DeseretSmallLongI + "X", "BTW", "CAF\u00C9", "\u03A9\u0416" + AdlamCapitalAlif, "\U0001F600", "none"];

        // The runtime fills its casing table for a block of characters the first time it sees one.
        foreach (string tail in tails)
        {
            _ = index.MatchLongestSuffix(tail);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            foreach (string tail in tails)
            {
                _ = index.MatchLongestSuffix(tail);
            }
        }

        Assert.AreEqual(0L, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static string Join(Random random, string[] pieces, int min, int max)
    {
        var text = new StringBuilder();
        for (int count = random.Next(min, max + 1); count > 0; count--)
        {
            text.Append(pieces[random.Next(pieces.Length)]);
        }

        return text.ToString();
    }

    private static string Hex(string text)
        => string.Join(" ", text.Select(c => ((int)c).ToString("X4", CultureInfo.InvariantCulture)));

    // Types text the way the touch keyboard does, so the layout doesn't matter. Returns whether the
    // last character fired a trigger.
    private static bool Type(KeyboardHook hook, string text)
    {
        bool fired = false;
        foreach (char ch in text)
        {
            fired = hook.ProcessKey(Native.VK_PACKET, ch, ctrl: false, alt: false, shift: false, win: false);
        }

        return fired;
    }
}
