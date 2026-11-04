// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetTextTests
{
    private static readonly DateTime Now = new(2026, 3, 4, 9, 5, 0);

    [TestMethod]
    public void Expand_Answers_FillTheQuestionsInOrderAndTheCaretMarkerIsDropped()
    {
        var text = SnippetText.Expand(
            "Hi {{prompt:Name}},\n$|$Thanks for {{prompt?:Topic}}.\n{{prompt:Name}}",
            ["Your name", string.Empty, "Your name again"],
            clipboard: null,
            Now,
            CultureInfo.InvariantCulture);

        Assert.AreEqual("Hi Your name,\nThanks for .\nYour name again", text);
    }

    [TestMethod]
    public void Expand_OnlyTheFirstCaretMarker_IsDropped()
        => Assert.AreEqual("ab$|$c", SnippetText.Expand("a$|$b$|$c", [], clipboard: null, Now, CultureInfo.InvariantCulture));

    [TestMethod]
    public void Expand_Clipboard_IsReadOnceForEverySpot()
    {
        var reads = 0;
        var text = SnippetText.Expand(
            "{{clipboard}} and {{clipboard}}",
            [],
            () =>
            {
                reads++;
                return "copied text";
            },
            Now,
            CultureInfo.InvariantCulture);

        Assert.AreEqual("copied text and copied text", text);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void Expand_DatesAndTimes_ComeOutAsTextExpanderWritesThem()
        => Assert.AreEqual(
            "2026-03-04 / 2026 / 09:05",
            SnippetText.Expand("{{isodate}} / {{date:yyyy}} / {{date:%H:%M}}", [], clipboard: null, Now, CultureInfo.InvariantCulture));

    [TestMethod]
    [DataRow("{{clipboard:email}}", "you@example.com")]
    [DataRow("{{clipboard:link}}", "https://example.com/docs")]
    [DataRow("Call {{clipboard:phone}}", "Call 555-0123")]
    [DataRow("Typed as written: {{clipboard:email:2}} {{clipboard:email:last}}", "Typed as written: {{clipboard:email:2}} {{clipboard:email:last}}")]
    public void Expand_ClipboardParts_TakeTheirMatchFromTheCopy(string replacement, string expected)
    {
        var text = SnippetText.Expand(
            replacement,
            [],
            static () => "Write to you@example.com, see https://example.com/docs. Phone 555-0123.",
            Now,
            CultureInfo.InvariantCulture);

        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void ExpandWithGaps_PartThatIsNotThere_LeavesItsSpotEmptyAndSaysWhich()
    {
        var expanded = SnippetText.ExpandWithGaps("Mail {{clipboard:email}}, thanks", [], static () => "no address here");
        ClipboardGap[] gaps = [new(ClipboardPart.Email, ClipboardGapReason.NotFound)];

        Assert.AreEqual("Mail , thanks", expanded.Text);
        CollectionAssert.AreEqual(gaps, expanded.Gaps.ToArray());
    }

    [TestMethod]
    public void Expand_ClipboardPartThatIsNotThere_InsertsNothingAndTheRestExpands()
        => Assert.AreEqual(
            "Mail  on 2026-03-04",
            SnippetText.Expand("Mail {{clipboard:email}} on {{isodate}}", [], static () => "no address here", Now, CultureInfo.InvariantCulture));

    [TestMethod]
    public void Expand_WholeCopyAndPart_ReadTheClipboardOnce()
    {
        var reads = 0;
        var text = SnippetText.Expand(
            "{{clipboard}} / {{clipboard:email}}",
            [],
            () =>
            {
                reads++;
                return "Your name <you@example.com>";
            },
            Now,
            CultureInfo.InvariantCulture);

        Assert.AreEqual("Your name <you@example.com> / you@example.com", text);
        Assert.AreEqual(1, reads);
    }

    [TestMethod]
    public void Preview_UnansweredQuestionsAndClipboard_ShowPlaceholders()
        => Assert.AreEqual(
            "Hi [Name], here's [Clipboard]. [Your answer]",
            SnippetText.Preview("Hi {{prompt:Name}}, here's {{clipboard}}. {{prompt:}}", answers: null, Now, CultureInfo.InvariantCulture));

    [TestMethod]
    [DataRow("{{clipboard:email}}", "[Email from clipboard]")]
    [DataRow("{{clipboard:number}}", "[Number from clipboard]")]
    [DataRow("{{clipboard:pick}}", "[Copy from clipboard history]")]
    [DataRow("{{clipboard:pick:link}}", "[Link from clipboard history]")]
    [DataRow("{{clipboard:Link}}", "{{clipboard:Link}}")]
    [DataRow("{{clipboard:link:2}}", "{{clipboard:link:2}}")]
    public void Preview_ClipboardVariables_ShowTheirNamesAsTheEditorDoes(string replacement, string expected)
        => Assert.AreEqual(expected, SnippetText.Preview(replacement, answers: null, Now, CultureInfo.InvariantCulture));

    [TestMethod]
    public void Preview_DatesCaretAndLaterCaretMarkers_ComeOutAsWhenExpanded()
        => Assert.AreEqual(
            "2026-03-04 ab$|$c",
            SnippetText.Preview("{{isodate}} a$|$b$|$c", answers: null, Now, CultureInfo.InvariantCulture));

    [TestMethod]
    public void Preview_Answers_ReplaceTheirPlaceholders()
        => Assert.AreEqual(
            "Hi Your name, about [Topic]",
            SnippetText.Preview("Hi {{prompt:Name}}, about {{prompt?:Topic}}", ["Your name", string.Empty], Now, CultureInfo.InvariantCulture));

    [TestMethod]
    public void FindQuestions_RepeatedAndOptionalQuestions_AreAskedInOrderEachTime()
    {
        var questions = SnippetText.FindQuestions("{{prompt:Name}} {{prompt?:Topic}} {{prompt:Name}} {{date}}");

        CollectionAssert.AreEqual(
            new[] { new PromptRequest("Name", true), new PromptRequest("Topic", false), new PromptRequest("Name", true) },
            questions.ToArray());
    }

    [TestMethod]
    public void OneLine_LineBreaksTabsAndRunsOfSpaces_BecomeOneSpace()
        => Assert.AreEqual("Best regards, Your name", SnippetText.OneLine("  Best regards,\r\n\t  Your name \n"));

    [TestMethod]
    public void OneLine_LongText_IsCutWithAnEllipsis()
        => Assert.AreEqual("abcde\u2026", SnippetText.OneLine("abcdefghij", maxLength: 5));

    [TestMethod]
    public void OneLine_CutInsideASurrogatePair_DropsTheHalf()
        => Assert.AreEqual("abcd\u2026", SnippetText.OneLine("abcd\U0001F600efg", maxLength: 5));

    [TestMethod]
    [DataRow("Best regards,\nYour name", (int)SnippetKind.Ready)]
    [DataRow("Hi {{prompt:Name}}", (int)SnippetKind.AsksQuestions)]
    [DataRow("Hi {{prompt?:Name}}", (int)SnippetKind.AsksQuestions)]
    [DataRow("Earlier: {{clipboard:pick}}", (int)SnippetKind.TextExpanderOnly)]
    [DataRow("Mail: {{clipboard:pick:email}}", (int)SnippetKind.TextExpanderOnly)]
    [DataRow("{{prompt:Name}} {{clipboard:pick}}", (int)SnippetKind.TextExpanderOnly)]
    [DataRow("Site: " + AskingSpots.Link, (int)SnippetKind.Ready)]
    [DataRow("{{prompt:Name}} " + AskingSpots.Phone, (int)SnippetKind.AsksQuestions)]
    [DataRow("Today is {{date}}, copied {{clipboard}}", (int)SnippetKind.Ready)]
    [DataRow("Typed as written: {{advancedpaste:Summarize}}", (int)SnippetKind.Ready)]
    public void Kind_Snippet_SaysWhatCommandPaletteCanDoWithIt(string replacement, int kind)
        => Assert.AreEqual((SnippetKind)kind, new Snippet("!test", replacement).Kind);

    [TestMethod]
    [DataRow("Hi {{clipboard}}", false)]
    [DataRow("Mail " + AskingSpots.Email, true)]
    [DataRow("{{prompt:Name}}, site " + AskingSpots.Link, true)]
    [DataRow("Earlier: {{clipboard:pick}}", false)]
    [DataRow("Earlier: {{clipboard:pick:email}}", false)]
    [DataRow("Typed as written: {{clipboard:Email}}", false)]
    public void UsesClipboardParts_Snippet_IsTrueForAPartOfWhatsOnTheClipboardNow(string replacement, bool expected)
        => Assert.AreEqual(expected, new Snippet("!test", replacement).UsesClipboardParts);

    [TestMethod]
    public void PlanClipboard_SeveralMatches_AsksForEachSpotInTextExpandersOrder()
    {
        var plan = SnippetText.PlanClipboard(
            "Call " + AskingSpots.Phone + " or mail " + AskingSpots.Email,
            [],
            AskingSpots.Copied,
            Now,
            CultureInfo.InvariantCulture);
        string[] phoneNumbers = ["555-0123", "555-0199"];
        string[] emailAddresses = ["you@example.com", "team@example.com"];

        Assert.AreEqual(2, plan.Choices.Count);
        Assert.AreEqual(ClipboardPart.Phone, plan.Choices[0].Part);
        CollectionAssert.AreEqual(phoneNumbers, plan.Choices[0].Options.ToArray());
        Assert.AreEqual(ClipboardPart.Email, plan.Choices[1].Part);
        CollectionAssert.AreEqual(emailAddresses, plan.Choices[1].Options.ToArray());
        Assert.IsFalse(plan.Choices[0].FromHistory);
        Assert.AreEqual(0, plan.Gaps.Count);
        Assert.AreEqual(AskingSpots.Copied, plan.Copy);
    }

    [TestMethod]
    public void PlanClipboard_OneMatchOrNone_AsksNothingAndNamesEachEmptySpot()
    {
        var plan = SnippetText.PlanClipboard(
            AskingSpots.Email + " " + AskingSpots.Phone + " " + AskingSpots.Phone,
            [],
            "Write to you@example.com",
            Now,
            CultureInfo.InvariantCulture);
        ClipboardGap[] emptySpots = [new(ClipboardPart.Phone, ClipboardGapReason.NotFound), new(ClipboardPart.Phone, ClipboardGapReason.NotFound)];

        Assert.AreEqual(0, plan.Choices.Count);
        CollectionAssert.AreEqual(emptySpots, plan.Gaps.ToArray());
    }

    [TestMethod]
    public void Expand_Fill_PutsEachPickInItsSpotWithTheAnswers()
    {
        const string Replacement = "Hi {{prompt:Name}}, call " + AskingSpots.Phone + " or mail " + AskingSpots.Email;
        var plan = SnippetText.PlanClipboard(Replacement, ["Your name"], AskingSpots.Copied, Now, CultureInfo.InvariantCulture);
        var fill = new SnippetFill(["Your name"], plan, ["555-0199", "team@example.com"]);

        Assert.AreEqual("Hi Your name, call 555-0199 or mail team@example.com", SnippetText.Expand(Replacement, fill, Now, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void Preview_FillWithChoicesStillToMake_NamesThemInBrackets()
    {
        const string Replacement = "Call " + AskingSpots.Phone + " or mail " + AskingSpots.Email + " on {{isodate}}";
        var plan = SnippetText.PlanClipboard(Replacement, [], AskingSpots.Copied, Now, CultureInfo.InvariantCulture);
        var fill = new SnippetFill([], plan, ["555-0199"]);

        Assert.AreEqual(
            "Call 555-0199 or mail [Email from clipboard] on 2026-03-04",
            SnippetText.Preview(Replacement, fill, Now, CultureInfo.InvariantCulture));
    }
}
