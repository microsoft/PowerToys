// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.Editor.UnitTests;

[TestClass]
public sealed class SharedRulesTests
{
    [TestMethod]
    public void ExhaustivePreviewAndExpansionAgreeWithoutReinterpretingRuntimeValues()
    {
        string[] pieces =
        {
            "text", "\\n", "\\t", "\n", "\t", "$|$", "{{clipboard}}", "{{prompt:Q}}",
            "{{prompt?:Q}}", "{{date:'fixed'}}", "{{date:}}", "{{date:%%}}", "{{unknown}}", "\\",
        };
        const string answer = @"answer\n{{clipboard}}$|$";
        const string clipboard = @"copied\t{{prompt:Q}}$|$";
        int checkedCount = 0;
        foreach (string snippet in Combinations(pieces, 4))
        {
            string expected = Variables.Expand(snippet, _ => answer, () => clipboard);
            string preview = string.Concat(TextExpanderSnippetPreview.Build(snippet, new DateTime(2026, 10, 27))
                .Select(segment => segment.Kind switch
                {
                    TextExpanderPreviewSegmentKind.Prompt => answer,
                    TextExpanderPreviewSegmentKind.Clipboard => clipboard,
                    TextExpanderPreviewSegmentKind.Cursor => Variables.CursorMarker,
                    TextExpanderPreviewSegmentKind.LineBreak => "\n",
                    TextExpanderPreviewSegmentKind.Tab => "\t",
                    _ => segment.Value,
                }));
            Assert.AreEqual(expected, preview, $"Preview diverged for '{snippet}'.");
            checkedCount++;
        }

        Assert.AreEqual(41371, checkedCount);
    }

    [TestMethod]
    public void ExhaustiveTriggerReachUsesTheSameWinnerAsTheRuntime()
    {
        string[] pieces = { "a", "A", "!", "_" };
        string[] snippets = Combinations(pieces, 3).Where(text => text.Length > 0).ToArray();
        foreach (bool caseSensitive in new[] { true, false })
        {
            var triggers = new TextExpanderTriggerSet(snippets, caseSensitive);
            TriggerIndex runtime = TriggerIndex.Build(snippets, caseSensitive);
            foreach (string candidate in Combinations(pieces, 5))
            {
                foreach (bool wordMode in new[] { true, false })
                {
                    (string Trigger, char Next)? expected = null;
                    for (int typed = 1; typed < candidate.Length; typed++)
                    {
                        if (wordMode && !WordBoundaryRules.IsTerminator(candidate[typed]))
                        {
                            continue;
                        }

                        string winner = runtime.MatchLongestSuffix(candidate.AsSpan(0, typed));
                        if (winner is not null && (!wordMode || WordBoundaryRules.StartsAtWordBoundary(candidate.AsSpan(0, typed - winner.Length), winner)))
                        {
                            expected = (candidate.Substring(typed - winner.Length, winner.Length), candidate[typed]);
                            break;
                        }
                    }

                    Assert.AreEqual(expected, TextExpanderTriggerReach.FindBlocker(candidate, triggers, wordMode), $"{candidate}, caseSensitive={caseSensitive}, wordMode={wordMode}");
                }
            }
        }
    }

    [TestMethod]
    public void ExhaustiveSnippetRoundTripsKeepEveryReplacementExactly()
    {
        string[] pieces = { "a", " ", "=", "<", ">", "\n", "\t", "\\n", "\\", "\"", "//", "{{date}}" };
        foreach (string replacement in Combinations(pieces, 3))
        {
            SnippetFile file = SnippetFile.Read(SnippetJson.Bytes("!keep=  original  "));
            file.Upsert(null, "!new", replacement);
            byte[] saved = file.ToBytes();
            SnippetFile reloaded = SnippetFile.Read(saved);
            Assert.AreEqual("  original  ", reloaded.Snippets[0].Replacement);
            Assert.AreEqual(replacement, reloaded.Snippets.Single(entry => entry.Trigger == "!new").Replacement);
            CollectionAssert.AreEqual(saved, reloaded.ToBytes(), "Saving again changes nothing.");
        }
    }

    private static IEnumerable<string> Combinations(string[] pieces, int maxLength)
    {
        yield return string.Empty;
        var current = new List<string> { string.Empty };
        for (int length = 1; length <= maxLength; length++)
        {
            var next = new List<string>();
            foreach (string prefix in current)
            {
                foreach (string piece in pieces)
                {
                    string value = prefix + piece;
                    next.Add(value);
                    yield return value;
                }
            }

            current = next;
        }
    }
}
