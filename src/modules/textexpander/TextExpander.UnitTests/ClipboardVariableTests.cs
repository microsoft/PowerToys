// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

[TestClass]
public sealed class ClipboardVariableTests
{
    // The block the feature's mockups copy, with sample data only.
    private const string Copied = "Hi! Order 1042 shipped today.\nQuestions? Email you@example.com\nor call 555-0123.\nTrack it: https://example.com/track/1042\nReturns: https://example.com/returns";

    private static readonly CultureInfo EnglishUS = CultureInfo.GetCultureInfo("en-US");

    [TestMethod]
    [DataRow("{{clipboard}}", false, ClipboardPart.All, ClipboardMatch.First, 0)]
    [DataRow("{{clipboard:email}}", false, ClipboardPart.Email, ClipboardMatch.First, 0)]
    [DataRow("{{clipboard:link:2}}", false, ClipboardPart.Link, ClipboardMatch.Numbered, 2)]
    [DataRow("{{clipboard:phone:last}}", false, ClipboardPart.Phone, ClipboardMatch.Last, 0)]
    [DataRow("{{clipboard:number:all}}", false, ClipboardPart.Number, ClipboardMatch.All, 0)]
    [DataRow("{{clipboard:link:pick}}", false, ClipboardPart.Link, ClipboardMatch.Pick, 0)]
    [DataRow("{{clipboard:pick}}", true, ClipboardPart.All, ClipboardMatch.First, 0)]
    [DataRow("{{clipboard:pick:email}}", true, ClipboardPart.Email, ClipboardMatch.First, 0)]
    [DataRow("{{clipboard:pick:link:99}}", true, ClipboardPart.Link, ClipboardMatch.Numbered, 99)]
    [DataRow("{{clipboard:pick:link:pick}}", true, ClipboardPart.Link, ClipboardMatch.Pick, 0)]
    public void EveryFormIsReadAndWrittenTheSameWay(string token, bool pickCopy, ClipboardPart part, ClipboardMatch match, int index)
    {
        var expected = new ClipboardVariable(pickCopy, part, match, index);
        Assert.IsTrue(ClipboardVariable.TryParse(token, out ClipboardVariable variable), token);
        Assert.AreEqual(expected, variable);
        Assert.AreEqual(token, variable.ToToken());
        CollectionAssert.AreEqual(new[] { (0, token.Length) }, SnippetTemplate.FindVariables(token).ToArray(), "The editor treats it as one variable.");
        Assert.AreEqual(match == ClipboardMatch.Pick || pickCopy, variable.MayAsk);
    }

    [TestMethod]
    [DataRow("{{clipboard:link:1}}", "{{clipboard:link}}")]
    [DataRow("{{clipboard:link:01}}", null)]
    [DataRow("{{clipboard:link:0}}", null)]
    [DataRow("{{clipboard:link:100}}", null)]
    [DataRow("{{clipboard:Link}}", null)]
    [DataRow("{{ clipboard }}", null)]
    [DataRow("{{clipboard:all}}", null)]
    [DataRow("{{clipboard:email:first}}", null)]
    [DataRow("{{clipboard:pick:pick}}", null)]
    [DataRow("{{clipboard:link:pick:pick}}", null)]
    [DataRow("{{clipboard:}}", null)]
    public void OnlyExactFormsAreVariablesAndANumberOneIsTheFirst(string token, string? written)
    {
        Assert.AreEqual(written != null, ClipboardVariable.TryParse(token, out ClipboardVariable variable), token);
        if (written != null)
        {
            Assert.AreEqual(written, variable.ToToken());
            return;
        }

        // Anything else stays as typed.
        Assert.AreEqual(token, Variables.Expand(token, _ => null, () => Copied));
        Assert.AreEqual(0, SnippetTemplate.FindVariables(token).Count);
    }

    [TestMethod]
    public void WhatDoesNotApplyIsDropped()
    {
        Assert.AreEqual("{{clipboard}}", new ClipboardVariable(Part: ClipboardPart.All, Match: ClipboardMatch.Last).ToToken());
        Assert.AreEqual("{{clipboard:pick}}", new ClipboardVariable(PickCopy: true, Match: ClipboardMatch.Pick, Index: 4).ToToken());
        Assert.AreEqual("{{clipboard:email}}", new ClipboardVariable(Part: ClipboardPart.Email, Match: ClipboardMatch.Numbered, Index: 1).ToToken());
        Assert.AreEqual("{{clipboard:email:99}}", new ClipboardVariable(Part: ClipboardPart.Email, Match: ClipboardMatch.Numbered, Index: 500).ToToken());
        Assert.AreEqual("{{clipboard:email:last}}", new ClipboardVariable(Part: ClipboardPart.Email, Match: ClipboardMatch.Last, Index: 7).ToToken());
        Assert.IsFalse(new ClipboardVariable(Part: ClipboardPart.All, Match: ClipboardMatch.Pick).MayAsk, "A whole copy has no matches to pick from.");
    }

    [TestMethod]
    public void TheMockupsCopyGivesWhatTheyShow()
    {
        Assert.AreEqual("you@example.com", Expand("{{clipboard:email}}"));
        Assert.AreEqual("https://example.com/track/1042", Expand("{{clipboard:link}}"));
        Assert.AreEqual("https://example.com/returns", Expand("{{clipboard:link:2}}"));
        Assert.AreEqual("https://example.com/track/1042\nhttps://example.com/returns", Expand("{{clipboard:link:all}}"));
        Assert.AreEqual("555-0123", Expand("{{clipboard:phone}}"));
        Assert.AreEqual("1042", Expand("{{clipboard:number}}"));
        Assert.AreEqual("Thanks! I'll send it to you@example.com today.", Expand("Thanks! I'll send it to {{clipboard:email}} today."));
        Assert.AreEqual(Copied, Expand("{{clipboard}}"));
    }

    [TestMethod]
    public void APartThatIsNotThereInsertsNothingAndTheRestExpands()
    {
        (string text, _) = Variables.ExpandWithPromptRequests("Email: {{clipboard:email:3}}, link: {{clipboard:link}} on {{isodate}}", _ => null, () => Copied, new DateTime(2026, 10, 18), EnglishUS);
        Assert.AreEqual("Email: , link: https://example.com/track/1042 on 2026-10-18", text);
        Assert.AreEqual("[][][][]", Variables.Expand("[{{clipboard:email}}][{{clipboard:phone:last}}][{{clipboard:number:all}}][{{clipboard:link:pick}}]", _ => null, () => "nothing here"));
        Assert.AreEqual("[]", Variables.Expand("[{{clipboard:email}}]", _ => null), "No clipboard text.");
    }

    [TestMethod]
    public void WhatsOnTheClipboardIsReadOnceAndOnlyWhenUsed()
    {
        int reads = 0;
        string? Read()
        {
            reads++;
            return Copied;
        }

        _ = Variables.Expand("{{clipboard:email}} {{clipboard:link:2}} {{clipboard}} {{clipboard:number:last}}", _ => null, Read);
        Assert.AreEqual(1, reads);

        _ = Variables.Expand("{{clipboard:pick}} {{date}}", _ => null, Read, _ => "picked");
        Assert.AreEqual(1, reads, "A copy from history doesn't read what's on the clipboard now.");
    }

    [TestMethod]
    public void EachDifferentMatchCountsOnceInOrder()
    {
        const string text = "a@x.com, B@x.com, A@X.com, c@x.com, b@x.com";
        Assert.AreEqual("a@x.com\nB@x.com\nc@x.com", Variables.Expand("{{clipboard:email:all}}", _ => null, () => text));
        Assert.AreEqual("B@x.com", Variables.Expand("{{clipboard:email:2}}", _ => null, () => text));
        Assert.AreEqual("c@x.com", Variables.Expand("{{clipboard:email:last}}", _ => null, () => text));
        Assert.AreEqual("12\n7", Variables.Expand("{{clipboard:number:all}}", _ => null, () => "12 apples, 7 pears, 12 plums"));
    }

    [TestMethod]
    public void PickingAMatchAsksOnlyWhenThereIsAChoice()
    {
        var asked = new List<ClipboardChoice>();
        string? Choose(ClipboardChoice choice)
        {
            asked.Add(choice);
            return choice.Options[^1];
        }

        Assert.AreEqual("https://example.com/returns", Variables.Expand("{{clipboard:link:pick}}", _ => null, () => Copied, Choose));
        Assert.AreEqual(1, asked.Count);
        Assert.AreEqual(ClipboardChoiceKind.Match, asked[0].Kind);
        Assert.AreEqual(ClipboardPart.Link, asked[0].Part);
        Assert.AreEqual("https://example.com/track/1042|https://example.com/returns", string.Join("|", asked[0].Options));
        Assert.IsTrue(asked[0].IsLast);

        asked.Clear();
        Assert.AreEqual("you@example.com", Variables.Expand("{{clipboard:email:pick}}", _ => null, () => Copied, Choose), "One match needs no choice.");
        Assert.AreEqual(string.Empty, Variables.Expand("{{clipboard:phone:pick}}", _ => null, () => "none", Choose), "Nor does none.");
        Assert.AreEqual(0, asked.Count);

        Assert.AreEqual("https://example.com/track/1042", Variables.Expand("{{clipboard:link:pick}}", _ => null, () => Copied), "Without a window, the first.");
    }

    [TestMethod]
    public void OnlyTheLastWindowToShowSaysSo()
    {
        var asked = new List<ClipboardChoice>();
        string? Choose(ClipboardChoice choice)
        {
            asked.Add(choice);
            return choice.Kind == ClipboardChoiceKind.Copy ? Copied : choice.Options[0];
        }

        // The phone has one match, so it doesn't ask, and the second link choice is the last to show.
        _ = Variables.Expand("{{clipboard:link:pick}} {{clipboard:link:pick}} {{clipboard:phone:pick}}", _ => null, () => Copied, Choose);
        Assert.AreEqual("Match:False Match:True", Describe(asked));

        // A copy from history with a match to pick may ask again; one without, last, doesn't.
        asked.Clear();
        _ = Variables.Expand("{{clipboard:pick:link:pick}} {{clipboard:pick}}", _ => null, null, Choose);
        Assert.AreEqual("Copy:False Match:False Copy:True", Describe(asked));

        static string Describe(List<ClipboardChoice> choices) => string.Join(" ", choices.Select(choice => $"{choice.Kind}:{choice.IsLast}"));
    }

    [TestMethod]
    public void ACopyFromHistoryIsWhatTheWindowGives()
    {
        Assert.AreEqual("Picked: you@example.com", Variables.Expand("Picked: {{clipboard:pick:email}}", _ => null, () => "now@example.com", _ => Copied));
        Assert.AreEqual("[]", Variables.Expand("[{{clipboard:pick}}]", _ => null, () => "now"), "Without a window there's nothing to pick from.");
        Assert.AreEqual("[]", Variables.Expand("[{{clipboard:pick:link}}]", _ => null, () => Copied, _ => string.Empty), "Clipboard history off or empty.");
        Assert.AreEqual("[]", Variables.Expand("[{{clipboard:pick}}]", _ => null, () => Copied, _ => null), "Cancelled; the engine drops the whole snippet.");
    }

    [TestMethod]
    public void ChoicesAndClipboardVariablesAreDetected()
    {
        Assert.IsTrue(Variables.MayChoose("{{clipboard:link:pick}}"));
        Assert.IsTrue(Variables.MayChoose("x {{clipboard:pick}} y"));
        Assert.IsFalse(Variables.MayChoose("{{clipboard:link}} {{clipboard}} {{clipboard:Link:pick}}"));
        Assert.IsFalse(Variables.MayChoose("{{date:'{{clipboard:pick}}'}}"), "Inside a date format, it's the format's text.");
        Assert.IsTrue(Variables.MayAsk("{{prompt:Name}}"));
        Assert.IsTrue(Variables.MayAsk("{{clipboard:pick}}"));
        Assert.IsFalse(Variables.MayAsk("{{clipboard:email}} {{date}}"));
        Assert.IsFalse(Variables.NeedsPrompt("{{clipboard:pick}}"));

        // Any clipboard variable, or text that looks like one, is never kept for undo.
        foreach (string snippet in new[] { "{{clipboard:email}}", "{{clipboard:pick}}", "{{clipboard:link:2}}", "{{clipboard:Link}}" })
        {
            Assert.IsTrue(Variables.UsesSensitiveRuntimeInput(snippet), snippet);
        }
    }

    [TestMethod]
    [DataRow("Mail you@example.com.", "you@example.com")]
    [DataRow("<first.last+tag@sub.example.co.uk>", "first.last+tag@sub.example.co.uk")]
    [DataRow("mailto:you@example.com?subject=hi", "you@example.com")]
    [DataRow("...you@example.com", "you@example.com")]
    [DataRow("not@an address, nor @example.com, nor you@example", "")]
    public void EmailAddresses(string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Email, EnglishUS)));

    [TestMethod]
    [DataRow("See https://example.com/a.", "https://example.com/a")]
    [DataRow("(see https://example.com/a)", "https://example.com/a")]
    [DataRow("[docs](https://example.com/docs)", "https://example.com/docs")]
    [DataRow("https://en.wikipedia.org/wiki/Foo_(bar), then", "https://en.wikipedia.org/wiki/Foo_(bar)")]
    [DataRow("Go to www.example.com!", "www.example.com")]
    [DataRow("HTTP://EXAMPLE.COM/X?y=1&z=2#top", "HTTP://EXAMPLE.COM/X?y=1&z=2#top")]
    [DataRow("\"https://example.com/q\"", "https://example.com/q")]
    [DataRow("https:// nothing, www. nothing", "")]
    [DataRow("ftp://example.com/file", "")]
    public void WebLinks(string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Link, EnglishUS)));

    [TestMethod]
    [DataRow("en-US", "Call 555-0123.", "555-0123")]
    [DataRow("en-US", "Call (425) 555-0123 or (425)555-0199", "(425) 555-0123|(425)555-0199")]
    [DataRow("en-US", "+1 425 555 0123, 425.555.0123", "+1 425 555 0123|425.555.0123")]
    [DataRow("en-US", "Order 1042, ID 4255550123", "")]
    [DataRow("en-US", "+14255550123", "+14255550123")]
    [DataRow("en-US", "On 2026-11-02, 11/2/2026 or 02.11.2026 at 10:30", "")]
    [DataRow("en-US", "Pi is 3.14159265, server 192.168.100.200", "")]
    [DataRow("en-US", "1,234,567 people; 1.234.567 others", "")]
    [DataRow("en-US", "x555-0123 and 555-0123x and https://example.com/555-0123 and a555-0123@example.com", "")]
    [DataRow("fr-FR", "Call 01 23 45 67 89, not 1 234 567", "01 23 45 67 89")]
    [DataRow("de-DE", "Phone 030/1234567 or 0049 30 1234567", "030/1234567|0049 30 1234567")]
    [DataRow("en-GB", "Ring 020 7946 0018", "020 7946 0018")]
    public void PhoneNumbers(string culture, string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Phone, CultureInfo.GetCultureInfo(culture))));

    [TestMethod]
    [DataRow("en-US", "Order 1042 costs $1,234.56, or -5 off.", "1042|1,234.56|-5")]
    [DataRow("en-US", "Pi 3.14 and 3,14 and 1.234,56 and 1,23", "3.14|3,14|1.234,56|1,23")]
    [DataRow("en-US", "Version v1.2.3, ID123, Windows11, 10kg, 50%", "10|50")]
    [DataRow("en-US", "1.2.3 is three numbers", "1|2|3")]
    [DataRow("en-US", "On 2026-11-02 at 10:30, from 192.168.1.10, call 555-0123", "")]
    [DataRow("en-US", "Track https://example.com/track/1042 or mail a1@example.com", "")]
    [DataRow("en-US", "1 234 is two numbers here", "1|234")]
    [DataRow("de-DE", "Total 1.234,56 € and 3,5 % and 1.000.000", "1.234,56|3,5|1.000.000")]
    [DataRow("fr-FR", "Total 1 234 567,89 € and 1\u202F000", "1 234 567,89|1\u202F000")]
    [DataRow("en-IN", "₹1,23,45,678.50 and 12,345", "1,23,45,678.50|12,345")]
    [DataRow("de-CH", "Price 1'234.50 and 1’000", "1'234.50|1’000")]
    [DataRow("en-US", "Temperature \u22125 and 4\u22122", "\u22125|4|2")]
    public void Numbers(string culture, string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Number, CultureInfo.GetCultureInfo(culture))));

    [TestMethod]
    public void AHugeCopyIsReadInLinearTime()
    {
        // Text built to make patterns work hard: long runs that almost match, and a link full of brackets.
        string[] copies =
        [
            new string('a', 3_000_000) + "@",
            string.Concat(Enumerable.Repeat("1-", 1_500_000)),
            string.Concat(Enumerable.Repeat("1.", 1_500_000)),
            "https://" + new string(')', 3_000_000),
            string.Concat(Enumerable.Repeat("{{clipboard:", 250_000)),
            string.Concat(Enumerable.Repeat("a.b@", 750_000)),
        ];

        var clock = Stopwatch.StartNew();
        foreach (string copy in copies)
        {
            foreach (ClipboardPart part in new[] { ClipboardPart.Email, ClipboardPart.Link, ClipboardPart.Phone, ClipboardPart.Number })
            {
                _ = ClipboardParts.Find(copy, part, EnglishUS);
            }

            _ = Variables.Expand(copy, _ => null, () => copy);
        }

        Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(20), $"Took {clock.Elapsed}.");

        // Only the start of a huge copy is read for its parts.
        string late = new string(' ', ClipboardParts.MaxScannedLength) + "you@example.com";
        Assert.AreEqual(0, ClipboardParts.Find(late, ClipboardPart.Email, EnglishUS).Count);
        Assert.AreEqual(late, ClipboardParts.Find(late, ClipboardPart.All, EnglishUS).Single(), "All of it is all of it.");
    }

    private static string Expand(string snippet) => Variables.Expand(snippet, _ => null, () => Copied);
}
