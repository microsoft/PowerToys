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
    [DataRow("{{clipboard}}", false, ClipboardPart.All)]
    [DataRow("{{clipboard:email}}", false, ClipboardPart.Email)]
    [DataRow("{{clipboard:link}}", false, ClipboardPart.Link)]
    [DataRow("{{clipboard:phone}}", false, ClipboardPart.Phone)]
    [DataRow("{{clipboard:number}}", false, ClipboardPart.Number)]
    [DataRow("{{clipboard:pick}}", true, ClipboardPart.All)]
    [DataRow("{{clipboard:pick:email}}", true, ClipboardPart.Email)]
    [DataRow("{{clipboard:pick:link}}", true, ClipboardPart.Link)]
    [DataRow("{{clipboard:pick:phone}}", true, ClipboardPart.Phone)]
    [DataRow("{{clipboard:pick:number}}", true, ClipboardPart.Number)]
    public void EveryFormIsReadAndWrittenTheSameWay(string token, bool pickCopy, ClipboardPart part)
    {
        var expected = new ClipboardVariable(pickCopy, part);
        Assert.IsTrue(ClipboardVariable.TryParse(token, out ClipboardVariable variable), token);
        Assert.AreEqual(expected, variable);
        Assert.AreEqual(token, variable.ToToken());
        CollectionAssert.AreEqual(new[] { (0, token.Length) }, SnippetTemplate.FindVariables(token).ToArray(), "The editor treats it as one variable.");
        Assert.AreEqual(pickCopy || part != ClipboardPart.All, variable.MayAsk, "A part may have two or more matches, and history two or more copies.");
    }

    [TestMethod]
    [DataRow("{{clipboard:link:1}}")]
    [DataRow("{{clipboard:link:2}}")]
    [DataRow("{{clipboard:link:last}}")]
    [DataRow("{{clipboard:number:all}}")]
    [DataRow("{{clipboard:link:pick}}")]
    [DataRow("{{clipboard:pick:link:pick}}")]
    [DataRow("{{clipboard:pick:email:2}}")]
    [DataRow("{{clipboard:Link}}")]
    [DataRow("{{ clipboard }}")]
    [DataRow("{{clipboard:all}}")]
    [DataRow("{{clipboard:email:first}}")]
    [DataRow("{{clipboard:pick:pick}}")]
    [DataRow("{{clipboard:email:pick}}")]
    [DataRow("{{clipboard:}}")]
    public void OnlyExactFormsAreVariables(string token)
    {
        Assert.IsFalse(ClipboardVariable.TryParse(token, out _), token);

        // Anything else stays as typed.
        Assert.AreEqual(token, Variables.Expand(token, _ => null, () => Copied));
        Assert.AreEqual(0, SnippetTemplate.FindVariables(token).Count);
    }

    [TestMethod]
    public void APartItDoesNotKnowIsTheWholeCopy()
    {
        Assert.AreEqual("{{clipboard}}", new ClipboardVariable(Part: (ClipboardPart)42).ToToken());
        Assert.AreEqual("{{clipboard:pick}}", new ClipboardVariable(PickCopy: true, Part: (ClipboardPart)42).ToToken());
        Assert.IsFalse(new ClipboardVariable(Part: (ClipboardPart)42).Normalized().MayAsk);
    }

    [TestMethod]
    public void TheMockupsCopyGivesWhatTheyShow()
    {
        Assert.AreEqual("you@example.com", Expand("{{clipboard:email}}"));
        Assert.AreEqual("555-0123", Expand("{{clipboard:phone}}"));
        Assert.AreEqual("1042", Expand("{{clipboard:number}}"));
        Assert.AreEqual("Thanks! I'll send it to you@example.com today.", Expand("Thanks! I'll send it to {{clipboard:email}} today."));
        Assert.AreEqual(Copied, Expand("{{clipboard}}"));

        // Two links: without a window to ask in, the first.
        Assert.AreEqual("https://example.com/track/1042", Expand("{{clipboard:link}}"));
    }

    [TestMethod]
    public void NothingFoundInsertsNothingTheRestExpandsAndTheResultSaysWhy()
    {
        ExpandedSnippet expanded = Variables.ExpandWithPromptRequests("Email: {{clipboard:email}}, phone: {{clipboard:phone}}, link: {{clipboard:link}} on {{isodate}}", _ => null, () => "Order 1042 at https://example.com/a", new DateTime(2026, 10, 18), EnglishUS);
        Assert.AreEqual("Email: , phone: , link: https://example.com/a on 2026-10-18", expanded.Text);
        CollectionAssert.AreEqual(
            new[] { new ClipboardGap(ClipboardPart.Email, ClipboardGapReason.NotFound), new ClipboardGap(ClipboardPart.Phone, ClipboardGapReason.NotFound) },
            expanded.Gaps.ToArray());

        // No text on the clipboard at all: the whole copy is missing too.
        expanded = Variables.ExpandWithPromptRequests("[{{clipboard}}][{{clipboard:number}}]", _ => null);
        Assert.AreEqual("[][]", expanded.Text);
        CollectionAssert.AreEqual(
            new[] { new ClipboardGap(ClipboardPart.All, ClipboardGapReason.NotFound), new ClipboardGap(ClipboardPart.Number, ClipboardGapReason.NotFound) },
            expanded.Gaps.ToArray());

        Assert.AreEqual(0, Variables.ExpandWithPromptRequests("Hi {{clipboard:email}} on {{date}}", _ => null, () => Copied).Gaps.Count);
        Assert.AreEqual(0, Variables.ExpandWithPromptRequests("No variables", _ => null).Gaps.Count);
    }

    [TestMethod]
    public void ACopyLosesItsControlCharactersButNotItsTabsAndLineBreaks()
    {
        // A Ctrl+O hidden at the end of a copied link ran the line in bash once the link was typed into a terminal.
        const string copied = "curl https://example.com/install.sh\u000F | sh\u001B[2J\tnext\r\nline\u0085\u007F\u0000";
        ExpandedSnippet expanded = Variables.ExpandWithPromptRequests("[{{clipboard}}][{{clipboard:link}}]", _ => null, () => copied, new DateTime(2026, 10, 18), EnglishUS);
        Assert.AreEqual("[curl https://example.com/install.sh | sh[2J\tnext\r\nline][https://example.com/install.sh]", expanded.Text);
        Assert.IsTrue(expanded.HasClipboardText);

        // Copies picked from clipboard history are cleaned the same way.
        var history = new ClipboardHistory(ClipboardHistoryState.Read, ["rm -rf ~\u000F", "See www.example.com/a\u0003b"]);
        expanded = Variables.ExpandWithPromptRequests("[{{clipboard:pick}}][{{clipboard:pick:link}}]", _ => null, null, new DateTime(2026, 10, 18), EnglishUS, choose: choice => choice.Options[0], history: () => history);
        Assert.AreEqual("[rm -rf ~][www.example.com/ab]", expanded.Text);
    }

    [TestMethod]
    public void ALinkEndsAtAControlCharacter()
    {
        // Cleaning the copy comes first; the pattern stops at one anyway, for callers that find parts in raw text.
        Assert.AreEqual("https://example.com/a", ClipboardParts.Find("Open https://example.com/a\u000Fb now", ClipboardPart.Link, EnglishUS).Single());
        Assert.AreEqual("https://example.com/x", ClipboardParts.Find("https://example.com/x\u001B[31m", ClipboardPart.Link, EnglishUS).Single());
        Assert.AreEqual("www.example.com/y", ClipboardParts.Find("www.example.com/y\u009Bz", ClipboardPart.Link, EnglishUS).Single());
    }

    [TestMethod]
    public void TheResultSaysWhetherAnyTextCameFromACopy()
    {
        Assert.IsTrue(Variables.ExpandWithPromptRequests("To {{clipboard:email}}", _ => null, () => Copied).HasClipboardText);
        Assert.IsFalse(Variables.ExpandWithPromptRequests("To {{clipboard:email}}", _ => null, () => "nothing here").HasClipboardText, "A spot left empty puts no copy in.");
        Assert.IsFalse(Variables.ExpandWithPromptRequests("To {{clipboard}}", _ => null, () => null).HasClipboardText);
        Assert.IsFalse(Variables.ExpandWithPromptRequests("Ticket {{prompt:Number}} on {{date}}", _ => "42").HasClipboardText, "An answer is the user's own.");
    }

    [TestMethod]
    public void WhatsOnTheClipboardAndInHistoryIsReadOnceAndOnlyWhenUsed()
    {
        int reads = 0;
        int historyReads = 0;
        string? Read()
        {
            reads++;
            return Copied;
        }

        ClipboardHistory ReadHistory()
        {
            historyReads++;
            return new ClipboardHistory(ClipboardHistoryState.Read, ["you@example.com"]);
        }

        _ = Variables.Expand("{{clipboard:email}} {{clipboard:link}} {{clipboard}} {{clipboard:number}}", _ => null, Read, _ => "picked", ReadHistory);
        Assert.AreEqual(1, reads);
        Assert.AreEqual(0, historyReads, "Nothing picks from clipboard history.");

        _ = Variables.Expand("{{clipboard:pick}} {{clipboard:pick:email}} {{date}}", _ => null, Read, _ => "picked", ReadHistory);
        Assert.AreEqual(1, reads, "Picking from history doesn't read what's on the clipboard now.");
        Assert.AreEqual(1, historyReads);
    }

    [TestMethod]
    public void TwoOrMoreMatchesAskWhichOneGoesStraightInAndNoneAsksNothing()
    {
        var asked = new List<ClipboardChoice>();
        string? Choose(ClipboardChoice choice)
        {
            asked.Add(choice);
            return choice.Options[^1];
        }

        Assert.AreEqual("https://example.com/returns", Variables.Expand("{{clipboard:link}}", _ => null, () => Copied, Choose));
        Assert.AreEqual(1, asked.Count);
        Assert.AreEqual(new ClipboardChoice(ClipboardPart.Link, FromHistory: false, asked[0].Options, IsLast: true), asked[0]);
        Assert.AreEqual("https://example.com/track/1042|https://example.com/returns", string.Join("|", asked[0].Options));

        asked.Clear();
        Assert.AreEqual("you@example.com", Variables.Expand("{{clipboard:email}}", _ => null, () => Copied, Choose), "One match needs no choice.");
        Assert.AreEqual(string.Empty, Variables.Expand("{{clipboard:phone}}", _ => null, () => "none", Choose), "Nor does none.");
        Assert.AreEqual(Copied, Variables.Expand("{{clipboard}}", _ => null, () => Copied, Choose), "Nor does the whole copy.");
        Assert.AreEqual(0, asked.Count);

        // Each different match once, in order; an email address the same however it's capitalized.
        Assert.AreEqual("a@x.com|B@x.com|c@x.com", string.Join("|", ClipboardParts.Find("a@x.com, B@x.com, A@X.com, c@x.com, b@x.com", ClipboardPart.Email, EnglishUS)));
        Assert.AreEqual("12|7", string.Join("|", ClipboardParts.Find("12 apples, 7 pears, 12 plums", ClipboardPart.Number, EnglishUS)));
    }

    [TestMethod]
    public void CancellingAChoiceAsksNothingMore()
    {
        var asked = new List<ClipboardChoice>();
        ExpandedSnippet expanded = Variables.ExpandWithPromptRequests(
            "{{clipboard:link}} {{clipboard:number}} {{clipboard:email}}",
            _ => null,
            () => "https://example.com/a https://example.com/b, 1 and 2",
            choice =>
            {
                asked.Add(choice);
                return null;
            });

        Assert.AreEqual(1, asked.Count, "The snippet is cancelled: the numbers aren't asked for.");
        Assert.AreEqual(0, expanded.Gaps.Count, "Nor is the email address that isn't there reported.");
    }

    [TestMethod]
    public void OnlyTheLastWindowToShowSaysSo()
    {
        var asked = new List<ClipboardChoice>();
        string? Choose(ClipboardChoice choice)
        {
            asked.Add(choice);
            return choice.Options[0];
        }

        // The phone has one match, so it doesn't ask, and the second link is the last to show.
        _ = Variables.Expand("{{clipboard:link}} {{clipboard:link}} {{clipboard:phone}}", _ => null, () => Copied, Choose);
        Assert.AreEqual("Link:False:False Link:False:True", Describe(asked));

        // From history: links across the copies, then the copies.
        asked.Clear();
        var history = new ClipboardHistory(ClipboardHistoryState.Read, ["https://example.com/new", "Older: https://example.com/old"]);
        _ = Variables.Expand("{{clipboard:pick:link}} {{clipboard:pick}} {{clipboard:pick:email}}", _ => null, null, Choose, () => history);
        Assert.AreEqual("Link:True:False All:True:True", Describe(asked), "No copy has an email address, so that asks nothing.");

        static string Describe(List<ClipboardChoice> choices) => string.Join(" ", choices.Select(choice => $"{choice.Part}:{choice.FromHistory}:{choice.IsLast}"));
    }

    [TestMethod]
    public void PickingFromHistoryOffersTheCopiesOrTheirMatchesNewestFirst()
    {
        var history = new ClipboardHistory(ClipboardHistoryState.Read, ["Write to you@example.com", "Call back on Friday", "https://example.com/docs or support@example.com, or YOU@example.com"]);
        var asked = new List<ClipboardChoice>();
        string? Choose(ClipboardChoice choice)
        {
            asked.Add(choice);
            return choice.Options[^1];
        }

        Assert.AreEqual("support@example.com", Variables.Expand("{{clipboard:pick:email}}", _ => null, () => "now@example.com", Choose, () => history));
        Assert.AreEqual("you@example.com|support@example.com", string.Join("|", asked.Single().Options), "Each different one once, newest first.");
        Assert.IsTrue(asked.Single().FromHistory);

        asked.Clear();
        Assert.AreEqual(history.Copies[2], Variables.Expand("{{clipboard:pick}}", _ => null, null, Choose, () => history));
        CollectionAssert.AreEqual(history.Copies.ToArray(), asked.Single().Options.ToArray());

        asked.Clear();
        Assert.AreEqual("https://example.com/docs", Variables.Expand("{{clipboard:pick:link}}", _ => null, null, Choose, () => history), "One match goes straight in.");
        Assert.AreEqual("Write to you@example.com", Variables.Expand("{{clipboard:pick}}", _ => null, null, Choose, () => history with { Copies = [history.Copies[0]] }), "So does one copy.");
        Assert.AreEqual(0, asked.Count);
        Assert.AreEqual("Write to you@example.com", Variables.Expand("{{clipboard:pick}}", _ => null, null, null, () => history), "Without a window, the newest.");
    }

    [TestMethod]
    public void PickingFromHistorySaysWhyNothingWentIn()
    {
        Assert.AreEqual(ClipboardGapReason.HistoryOff, GapFrom(ClipboardHistory.TurnedOff));
        Assert.AreEqual(ClipboardGapReason.HistoryUnreadable, GapFrom(ClipboardHistory.NotRead));
        Assert.AreEqual(ClipboardGapReason.HistoryUnreadable, GapFrom(null), "Nothing reads history here.");
        Assert.AreEqual(ClipboardGapReason.NoCopies, GapFrom(new ClipboardHistory(ClipboardHistoryState.Read, [])));
        Assert.AreEqual(ClipboardGapReason.NotFound, GapFrom(new ClipboardHistory(ClipboardHistoryState.Read, ["Call back on Friday"])));

        static ClipboardGapReason GapFrom(ClipboardHistory? history)
        {
            ExpandedSnippet expanded = Variables.ExpandWithPromptRequests("[{{clipboard:pick:email}}]", _ => null, () => Copied, _ => "asked", history is null ? null : () => history);
            Assert.AreEqual("[]", expanded.Text);
            return expanded.Gaps.Single() is var gap && gap.Part == ClipboardPart.Email ? gap.Reason : throw new AssertFailedException(gap.ToString());
        }
    }

    [TestMethod]
    public void AChoiceOffersAtMostAHundred()
    {
        string many = string.Join(", ", Enumerable.Range(1, 250).Select(number => "item " + number.ToString(CultureInfo.InvariantCulture)));
        var asked = new List<ClipboardChoice>();
        string? Choose(ClipboardChoice choice)
        {
            asked.Add(choice);
            return choice.Options[0];
        }

        _ = Variables.Expand("{{clipboard:number}} {{clipboard:pick:number}}", _ => null, () => many, Choose, () => new ClipboardHistory(ClipboardHistoryState.Read, [many, "251 252"]));

        Assert.AreEqual($"{ClipboardChoice.MaxOptions} {ClipboardChoice.MaxOptions}", string.Join(" ", asked.Select(choice => choice.Options.Count)));
        Assert.AreEqual("100", asked[0].Options[^1]);
    }

    [TestMethod]
    public void ChoicesAndClipboardVariablesAreDetected()
    {
        Assert.IsTrue(Variables.MayChoose("{{clipboard:link}}"), "Two or more links ask.");
        Assert.IsTrue(Variables.MayChoose("x {{clipboard:pick}} y"));
        Assert.IsFalse(Variables.MayChoose("{{clipboard}} {{clipboard:Link}} {{clipboard:link:pick}}"));
        Assert.IsFalse(Variables.MayChoose("{{date:'{{clipboard:pick}}'}}"), "Inside a date format, it's the format's text.");
        Assert.IsTrue(Variables.MayAsk("{{prompt:Name}}"));
        Assert.IsTrue(Variables.MayAsk("{{clipboard:email}} {{date}}"));
        Assert.IsFalse(Variables.MayAsk("{{clipboard}} {{date}}"));
        Assert.IsFalse(Variables.NeedsPrompt("{{clipboard:pick}}"));

        // Any clipboard variable, or text that looks like one, is never kept for undo.
        foreach (string snippet in new[] { "{{clipboard:email}}", "{{clipboard:pick}}", "{{clipboard:link:2}}", "{{clipboard:Link}}" })
        {
            Assert.IsTrue(Variables.UsesSensitiveRuntimeInput(snippet), snippet);
        }
    }

    [TestMethod]
    [DataRow("en-US")]
    [DataRow("de-DE")]
    [DataRow("fr-FR")]
    [DataRow("de-CH")]
    [DataRow("en-IN")]
    public void TheEditorsExamplesFollowTheRules(string region)
    {
        // The editor shows these by each part, and fills its hints from them, so they can't say something a snippet
        // doesn't do: each that counts is found whole, and none of the others is found at all.
        CultureInfo culture = CultureInfo.GetCultureInfo(region);
        foreach (ClipboardPart part in new[] { ClipboardPart.Email, ClipboardPart.Link, ClipboardPart.Phone, ClipboardPart.Number })
        {
            IReadOnlyList<ClipboardPartExample> examples = ClipboardPartExamples.For(part, culture);
            Assert.IsTrue(examples.Count(example => example.Counts) >= 2 && examples.Count(example => !example.Counts) >= 2, $"{region} {part}");
            Assert.IsTrue(examples.SkipWhile(example => example.Counts).All(example => !example.Counts), "Those that count come first.");
            foreach (ClipboardPartExample example in examples)
            {
                IReadOnlyList<string> found = ClipboardParts.Find(example.Text, part, culture);
                if (example.Counts)
                {
                    Assert.AreEqual(example.Text, found.SingleOrDefault(), $"{region} {part}: '{example.Text}' gave '{string.Join("|", found)}'.");
                    Assert.IsNull(example.Why);
                }
                else
                {
                    Assert.AreEqual(0, found.Count, $"{region} {part}: '{example.Text}' gave '{string.Join("|", found)}'.");
                    Assert.IsFalse(string.IsNullOrEmpty(example.Why), example.Text);
                }
            }
        }

        Assert.AreEqual(0, ClipboardPartExamples.For(ClipboardPart.All, culture).Count);
        StringAssert.Contains(ClipboardPartExamples.SampleNumber(culture), culture.NumberFormat.NumberDecimalSeparator, "Written the region's way.");
    }

    [TestMethod]
    [DataRow("Mail you@example.com.", "you@example.com")]
    [DataRow("<first.last+tag@sub.example.co.uk>", "first.last+tag@sub.example.co.uk")]
    [DataRow("mailto:you@example.com?subject=hi", "you@example.com")]
    [DataRow("...you@example.com", "you@example.com")]
    [DataRow("'you@example.com'", "you@example.com")]
    [DataRow("Mail 'you@example.com.'", "you@example.com")]
    [DataRow("\"you@example.com\"", "you@example.com")]
    [DataRow("o'connor@example.com", "o'connor@example.com")]
    [DataRow("Mail 'o'connor@example.com'.", "o'connor@example.com")]
    [DataRow("Mail 'o'connor@example.com,'", "o'connor@example.com")]
    [DataRow("mailto:o'connor@example.com?subject=hi", "o'connor@example.com")]
    [DataRow("user@example.xn--p1ai", "user@example.xn--p1ai")]
    [DataRow("<User@Example.XN--P1AI>", "User@Example.XN--P1AI")]
    [DataRow("list=tag@example.com", "tag@example.com")]
    [DataRow("*you@example.com*", "you@example.com")]
    [DataRow("**you@example.com**", "you@example.com")]
    [DataRow("`you@example.com`", "you@example.com")]
    [DataRow("``you@example.com``", "you@example.com")]
    [DataRow("Mail *o'connor@example.com*.", "o'connor@example.com")]
    [DataRow("*you@example.com.*", "you@example.com")]
    [DataRow("`user@example.xn--p1ai`", "user@example.xn--p1ai")]
    [DataRow("list=*you@example.com*", "you@example.com")]
    [DataRow("o!connor@example.com", "")]
    [DataRow("\u03b4\u03bf\u03ba\u03b9\u03bc\u03aetest@example.com", "")]
    [DataRow("e\u0301test@example.com", "")]
    [DataRow("\U0001D49Ctest@example.com", "")]
    [DataRow("a*user@example.com*", "")]
    [DataRow("a+*user@example.com*", "")]
    [DataRow("a`user@example.com`", "")]
    [DataRow("**you@example.com*", "")]
    [DataRow("*you@example.com", "")]
    [DataRow("`you@example.com", "")]
    [DataRow("o!connor@example.com, then you@example.com.", "you@example.com")]
    [DataRow("\u03b4\u03bf\u03ba\u03b9\u03bc\u03aetest@example.com, then you@example.com.", "you@example.com")]
    [DataRow("\U0001D49Ctest@example.com, then you@example.com.", "you@example.com")]
    [DataRow("a*user@example.com*, then you@example.com.", "you@example.com")]
    [DataRow("user@example.com1", "")]
    [DataRow("user@example.com-suffix", "")]
    [DataRow("user@example.com.123", "")]
    [DataRow("user@example.com\U0001D49C", "")]
    [DataRow("user@example.com\u0301", "")]
    [DataRow("user@example.com.\u0301", "")]
    [DataRow("user@example.com1, then you@example.com.", "you@example.com")]
    [DataRow("user@example.com\u0301, then you@example.com.", "you@example.com")]
    [DataRow("not@an address, nor @example.com, nor you@example", "")]
    public void EmailAddresses(string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Email, EnglishUS)));

    [TestMethod]
    [DataRow("555-0123@example.xn--p1ai")]
    [DataRow("o'555-0123@example.com")]
    [DataRow("o'4471@example.com")]
    public void EmailNamesAndDomainsKeepTheirDigitsOutOfOtherParts(string email)
    {
        Assert.AreEqual(email, ClipboardParts.Find(email, ClipboardPart.Email, EnglishUS).Single());
        Assert.AreEqual(0, ClipboardParts.Find(email, ClipboardPart.Phone, EnglishUS).Count);
        Assert.AreEqual(0, ClipboardParts.Find(email, ClipboardPart.Number, EnglishUS).Count);
    }

    [TestMethod]
    [DataRow("See https://example.com/a.", "https://example.com/a")]
    [DataRow("(see https://example.com/a)", "https://example.com/a")]
    [DataRow("[docs](https://example.com/docs)", "https://example.com/docs")]
    [DataRow("https://en.wikipedia.org/wiki/Foo_(bar), then", "https://en.wikipedia.org/wiki/Foo_(bar)")]
    [DataRow("Go to www.example.com!", "www.example.com")]
    [DataRow("HTTP://EXAMPLE.COM/X?y=1&z=2#top", "HTTP://EXAMPLE.COM/X?y=1&z=2#top")]
    [DataRow("\"https://example.com/q\"", "https://example.com/q")]
    [DataRow("https://example.com/reset?token=abc_", "https://example.com/reset?token=abc_")]
    [DataRow("https://example.com/foo~", "https://example.com/foo~")]
    [DataRow("See (https://example.com/a_).", "https://example.com/a_")]
    [DataRow("[docs](https://example.com/a_)", "https://example.com/a_")]
    [DataRow("_https://example.com/a_", "https://example.com/a")]
    [DataRow("__https://example.com/a__", "https://example.com/a")]
    [DataRow("~~https://example.com/a~~", "https://example.com/a")]
    [DataRow("_https://example.com/a__", "https://example.com/a_")]
    [DataRow("~~https://example.com/a~~~", "https://example.com/a~")]
    [DataRow("_~~https://example.com/a~~_", "https://example.com/a")]
    [DataRow("_(https://example.com/a)_", "https://example.com/a")]
    [DataRow("(_https://example.com/a_)", "https://example.com/a")]
    [DataRow("See(_https://example.com/a_)", "https://example.com/a")]
    [DataRow("_https://example.com/a_.", "https://example.com/a")]
    [DataRow("See _https://example.com/a_).", "https://example.com/a")]
    [DataRow("(see __https://example.com/a__).", "https://example.com/a")]
    [DataRow("See ~~https://example.com/a~~).", "https://example.com/a")]
    [DataRow("_(https://example.com/a_)_", "https://example.com/a_")]
    [DataRow("_(https://example.com/Foo_(bar))_", "https://example.com/Foo_(bar)")]
    [DataRow("_(https://example.com/Foo_(bar)_)_", "https://example.com/Foo_(bar)_")]
    [DataRow("_https://example.com/a(_", "https://example.com/a(")]
    [DataRow("_(https://example.com/a[)_", "https://example.com/a[")]
    [DataRow("https:// nothing, www. nothing", "")]
    [DataRow("ftp://example.com/file", "")]
    public void WebLinks(string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Link, EnglishUS)));

    [TestMethod]
    [DataRow("~home https://example.com/foo~", "https://example.com/foo~")]
    [DataRow(@"C:\~temp https://example.com/foo~", "https://example.com/foo~")]
    [DataRow("_https://example.com/a_ https://example.com/b_", "https://example.com/a|https://example.com/b_")]
    [DataRow("https://example.com/a_ _https://example.com/b_", "https://example.com/a_|https://example.com/b")]
    [DataRow("_see https://example.com/a and https://example.com/b_", "https://example.com/a|https://example.com/b_")]
    [DataRow("_(https://example.com/a_)", "https://example.com/a_")]
    [DataRow("_(https://example.com/Foo_(bar)_", "https://example.com/Foo_(bar)_")]
    [DataRow("_(https://example.com/Foo_(bar)_)", "https://example.com/Foo_(bar)_")]
    [DataRow("__https://example.com/a_", "https://example.com/a_")]
    [DataRow("~~https://example.com/a~", "https://example.com/a~")]
    [DataRow("See _https://example.com/a__).", "https://example.com/a_")]
    [DataRow("See ~~https://example.com/a~~~).", "https://example.com/a~")]
    [DataRow("~https://example.com/a~", "https://example.com/a~")]
    [DataRow("word_https://example.com/a_", "https://example.com/a_")]
    [DataRow("_note_ https://example.com/a_", "https://example.com/a_")]
    [DataRow("variable_name: https://example.com/a_", "https://example.com/a_")]
    public void LinkWrappersAreLocalAndMustBeComplete(string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Link, EnglishUS)));

    [TestMethod]
    [DataRow("email", "o'connor@example.com")]
    [DataRow("email", "user@example.xn--p1ai")]
    [DataRow("link", "https://example.com/reset?token=abc_")]
    [DataRow("link", "https://example.com/foo~")]
    public void ClipboardAndHistoryVariablesKeepCompleteAddresses(string part, string copied)
    {
        var history = new ClipboardHistory(ClipboardHistoryState.Read, [copied]);
        Assert.AreEqual(copied, Variables.Expand("{{clipboard:" + part + "}}", _ => null, () => copied));
        Assert.AreEqual(copied, Variables.Expand("{{clipboard:pick:" + part + "}}", _ => null, history: () => history));
    }

    [TestMethod]
    [DataRow("en-US", "Call 555-0123.", "555-0123")]
    [DataRow("en-US", "Call (425) 555-0123 or (425)555-0199", "(425) 555-0123|(425)555-0199")]
    [DataRow("en-US", "+1 425 555 0123, 425.555.0123", "+1 425 555 0123|425.555.0123")]
    [DataRow("en-US", "Order 1042, ID 4255550123", "")]
    [DataRow("en-US", "+14255550123", "+14255550123")]
    [DataRow("en-US", "On 2026-11-02, 11/2/2026 or 02.11.2026 at 10:30", "")]
    [DataRow("en-US", "Due 04-11-26, call 555-0123", "555-0123")]
    [DataRow("en-US", "Pi is 3.14159265, server 192.168.100.200", "")]
    [DataRow("en-US", "1,234,567 people; 1.234.567 others", "")]
    [DataRow("en-US", "x555-0123 and 555-0123x and https://example.com/555-0123 and a555-0123@example.com", "")]
    [DataRow("fr-FR", "Call 01 23 45 67 89, not 1 234 567", "01 23 45 67 89")]
    [DataRow("de-DE", "Phone 030/1234567 or 0049 30 1234567", "030/1234567|0049 30 1234567")]
    [DataRow("en-GB", "Ring 020 7946 0018", "020 7946 0018")]
    [DataRow("en-US", "Call +1(425)555-0123 or 1(800)555-0199", "+1(425)555-0123|1(800)555-0199")]
    [DataRow("en-GB", "Ring +44(0)20 7946 0018 or (+44) 20 7946 0019", "+44(0)20 7946 0018|(+44) 20 7946 0019")]
    [DataRow("en-US", "Ref 1234 5678 9012 3456 7890 1234", "")]
    [DataRow("en-US", "Invoice 00012345, account 0012345678", "")]
    [DataRow("en-US", "Order 1042 +1 425 555 0123", "+1 425 555 0123")]
    public void PhoneNumbers(string culture, string text, string expected)
        => Assert.AreEqual(expected, string.Join("|", ClipboardParts.Find(text, ClipboardPart.Phone, CultureInfo.GetCultureInfo(culture))));

    [TestMethod]
    [DataRow("en-US", "Order 1042 costs $1,234.56, or -5 off.", "1042|1,234.56|-5")]
    [DataRow("en-US", "Pi 3.14 and 3,14 and 1.234,56 and 1,23", "3.14|3,14|1.234,56|1,23")]
    [DataRow("en-US", "Version v1.2.3, ID123, Windows11, 10kg, 50%", "10|50")]
    [DataRow("en-US", "1.2.3 is three numbers", "1|2|3")]
    [DataRow("en-US", "On 2026-11-02 at 10:30, from 192.168.1.10, call 555-0123", "")]
    [DataRow("en-US", "Invoice 4471 due 04-11-26, 4/11/26 or 04.11.26", "4471")]
    [DataRow("de-DE", "Invoice 4471 due 04-11-26", "4471")]
    [DataRow("en-US", "Track https://example.com/track/1042 or mail a1@example.com", "")]
    [DataRow("en-US", "1 234 is two numbers here", "1|234")]
    [DataRow("de-DE", "Total 1.234,56 € and 3,5 % and 1.000.000", "1.234,56|3,5|1.000.000")]
    [DataRow("de-DE", "Total 1.234.567.890 €, server 192.168.1.10", "1.234.567.890")]
    [DataRow("fr-FR", "Total 1 234 567,89 € and 1\u202F000", "1 234 567,89|1\u202F000")]
    [DataRow("en-IN", "₹1,23,45,678.50 and 12,345", "1,23,45,678.50|12,345")]
    [DataRow("de-CH", "Price 1'234.50 and 1’000", "1'234.50|1’000")]
    [DataRow("en-US", "Temperature \u22125 and 4\u22122", "\u22125|4|2")]
    [DataRow("en-US", "Invoice 00012345", "00012345")]
    [DataRow("en-US", "Call at 10:30am, 9:45PM or 10:30h about order 1042", "1042")]
    [DataRow("ar-SA", "Total 1,234.56 and 1,234,567 and 1.234,56", "1,234.56|1,234,567|1.234,56")]
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

    [TestMethod]
    public void EachDifferentMatchIsCopiedOutOnce()
    {
        // A copy can repeat one link thousands of times. Each was copied out, and two dictionaries made to trim it, before
        // the repeats were dropped.
        string text = string.Concat(Enumerable.Repeat("see https://example.com/a(1), and ", 20_000));
        _ = ClipboardParts.Find(text, ClipboardPart.Link, EnglishUS);

        long before = GC.GetAllocatedBytesForCurrentThread();
        IReadOnlyList<string> links = ClipboardParts.Find(text, ClipboardPart.Link, EnglishUS);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.AreEqual("https://example.com/a(1)", links.Single());
        Assert.IsTrue(allocated < text.Length * 10L, $"{allocated} bytes for {text.Length} characters.");

        // A match that is the whole copy is the copy.
        string digits = new('7', 1_000);
        Assert.AreSame(digits, ClipboardParts.Find(digits, ClipboardPart.Number, EnglishUS).Single());
        Assert.AreEqual("You@Example.com", string.Join("|", ClipboardParts.Find("You@Example.com, you@example.com, YOU@EXAMPLE.COM", ClipboardPart.Email, EnglishUS)), "Kept as first written.");
    }

    private static string Expand(string snippet) => Variables.Expand(snippet, _ => null, () => Copied);
}
