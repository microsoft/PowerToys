// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

using Kind = Microsoft.PowerToys.Settings.UI.Helpers.TextExpanderPreviewSegmentKind;

namespace ViewModelTests
{
    /// <summary>The snippet list's rows: keycap, a sentence with its variables as bubbles, the saved text and the Never expands warning.</summary>
    [TestClass]
    public class TextExpanderRows
    {
        // A Saturday afternoon, so 12- and 24-hour formats differ.
        private static readonly DateTime Now = new(2026, 10, 17, 14, 5, 9);

        private static readonly Dictionary<string, string> Templates = new()
        {
            ["TextExpander_Row_Link"] = "Link to {0}",
            ["TextExpander_Row_LinkClipboard"] = "Link to {0} using {1}",
            ["TextExpander_Row_LinkPrompts"] = "Link to {0}, asks for {1}",
            ["TextExpander_Row_LinkClipboardPrompts"] = "Link to {0} using {1}, asks for {2}",
            ["TextExpander_Row_Date"] = "Today's date as {0}",
            ["TextExpander_Row_Time"] = "The current time as {0}",
            ["TextExpander_Row_Prompts"] = "Asks for {0}, then inserts the text",
            ["TextExpander_Row_PromptsLines"] = "Asks for {0}, then inserts {1} lines",
            ["TextExpander_Row_Clipboard"] = "Inserts {0}",
            ["TextExpander_Row_ClipboardLines"] = "Inserts {0}, in {1} lines",
            ["TextExpander_Row_JoinPair"] = "{0} and {1}",
            ["TextExpander_Row_JoinSeparator"] = ", ",
            ["TextExpander_Row_NeverExpandsDuplicate"] = "Never expands: the \"{0}\" further down the list is used instead.",
            ["TextExpander_Row_NeverExpandsFiresFirst"] = "Never expands: \"{0}\" fires first. Rename one of them.",
            ["TextExpander_Row_NeverExpandsFiresFirstWordWait"] = "Never expands: \"{0}\" fires first. Rename one, or turn on Wait for the whole word.",
            ["TextExpander_Row_NeverExpandsWhenYouType"] = "Never expands: \"{0}\" fires first when you type \"{1}\". Rename one of them.",
            ["TextExpander_Row_NeverExpandsWhenYouTypeSpace"] = "Never expands: \"{0}\" fires first when you type a space. Rename one of them.",
            ["TextExpander_Row_SpokenNewLine"] = "new line",
            ["TextExpander_Row_SpokenTab"] = "tab",
            ["TextExpander_Row_SpokenClipboard"] = "what you copied",
            ["TextExpander_Row_SpokenPrompt"] = "asks for {0}",
            ["TextExpander_Row_SpokenCursor"] = "cursor",
            ["TextExpander_Row_SpokenLines"] = "{0}, {1} lines",
            ["TextExpander_Row_AutomationName"] = "{0}: {1}",
            ["TextExpander_Row_AutomationNameWarning"] = "{0}: {1}. {2}",
            ["TextExpander_PreviewPromptUnnamed"] = "Your answer",
        };

        private static readonly string[] UserTriggers = { "#A", "#DIO", "#P", "#P$", "#D", "#Hi" };
        private static readonly string[] SpaceTriggers = { "my", "my sig" };
        private static readonly double[] TinyWidths = { 4.0 };
        private static readonly double[] MixedWidths = { 18.0, 40.2, 31.0 };
        private static readonly double[] LongWidths = { 30.0, 400.0 };
        private static readonly double[] BadWidths = { 30.0, double.NaN, double.PositiveInfinity, -5 };

        [TestMethod]
        public void DynamicLinksSaySiteAndWhatTheyUse()
        {
            TextExpanderRowDescription row = Describe("#A", "https://contoso.visualstudio.com/OS/_workitems/edit/{{clipboard}}");
            Assert.AreEqual("Link to contoso.visualstudio.com using what you copied", row.Sentence);
            Assert.AreEqual("contoso.visualstudio.com/OS/_workitems/edit/", row.Segments[0].Value, "The second line drops https://.");
            Assert.IsTrue(row.ShowsSegmentsBelow, "The path has words of its own.");
            Assert.AreEqual("#A: Link to contoso.visualstudio.com using what you copied", row.AutomationName, "Spoken as plain words.");
            CollectionAssert.AreEqual(
                new[] { Text("Link to contoso.visualstudio.com using "), Bubble(Kind.Clipboard, "what you copied") },
                row.SentenceParts.ToArray(),
                "The clipboard is a bubble in the sentence.");

            Assert.AreEqual(
                "Link to tracker.example.com using what you copied, asks for Ticket",
                Describe("!t", "https://tracker.example.com/{{prompt:Ticket}}?q={{clipboard}}").Sentence);
            Assert.AreEqual("Link to x.com, asks for A, B and C", Describe("!x", "https://x.com/{{prompt:A}}/{{prompt:B}}/{{prompt:C}}").Sentence);
            Assert.AreEqual("Link to www.contoso.com", Describe("!w", "www.contoso.com/daily/{{isodate}}").Sentence);
        }

        [TestMethod]
        public void DateTimeQuestionAndClipboardSnippetsGetTheirSentences()
        {
            TextExpanderRowDescription date = Describe("#D", "[{{date:MM/dd/yyyy}}]");
            Assert.AreEqual("Today's date as 10/17/2026", date.Sentence);
            CollectionAssert.AreEqual(new[] { Text("Today's date as "), Bubble(Kind.Date, "10/17/2026") }, date.SentenceParts.ToArray(), "One bubble, without the brackets.");
            Assert.IsFalse(date.ShowsSegmentsBelow, "Brackets aren't words, so the sentence says it all.");
            Assert.IsFalse(Describe("!t", "{{time}}").ShowsSegmentsBelow);

            Assert.AreEqual("The current time as 02:05 PM", Describe("!t", "{{time}}").Sentence);
            Assert.AreEqual("The current time as 14:05", Describe("!t", "{{date:HH:mm}}").Sentence);
            TextExpanderRowDescription hi = Describe("!hi", "Hi {{prompt:Name}}");
            Assert.AreEqual("Asks for Name, then inserts the text", hi.Sentence);
            Assert.IsTrue(hi.ShowsSegmentsBelow, "The text around the question adds to the sentence.");

            TextExpanderRowDescription quote = Describe("!q", "Quote: {{clipboard}}");
            Assert.AreEqual("Inserts what you copied", quote.Sentence);
            Assert.IsTrue(quote.ShowsSegmentsBelow);

            TextExpanderRowDescription pasteOnly = Describe("!p", " {{clipboard}}\\n");
            Assert.AreEqual("Inserts what you copied, in 2 lines", pasteOnly.Sentence);
            Assert.IsFalse(pasteOnly.ShowsSegmentsBelow, "The clipboard on its own is already the sentence.");
            Assert.AreEqual("!p: Inserts what you copied, in 2 lines", pasteOnly.AutomationName, "The sentence already counts the lines.");

            TextExpanderRowDescription meeting = Describe("!mtg", "Meeting notes - {{date:dddd}}\\nAttendees: {{prompt:Attendees}}\\nTopic: {{prompt:Topic}}\\n$|$");
            Assert.AreEqual("Asks for Attendees and Topic, then inserts 4 lines", meeting.Sentence);
            Assert.IsTrue(meeting.ShowsSegmentsBelow);
            Assert.AreEqual("!mtg: Asks for Attendees and Topic, then inserts 4 lines", meeting.AutomationName, "The sentence already counts the lines.");
        }

        [TestMethod]
        public void PlainOrUnclearSnippetsShowTheirSavedTextInstead()
        {
            Assert.IsNull(Describe("#P", "Would you please let me know your thoughts!").Sentence);
            Assert.IsNull(Describe("!url", "https://contoso.com/a").Sentence, "A fixed link is plain text.");

            TextExpanderRowDescription mixed = Describe("!u", "Updated {{date}}");
            Assert.IsNull(mixed.Sentence, "A date inside other text says itself best.");
            Assert.IsFalse(mixed.ShowsSegmentsBelow, "Without a sentence, the saved text is the title.");
            Assert.AreEqual(0, mixed.SentenceParts.Count);

            TextExpanderRowDescription hi = Describe("#Hi", "Hello $|$, How are you doing?");
            Assert.IsNull(hi.Sentence);
            Assert.AreEqual("#Hi: Hello (cursor) , How are you doing?", hi.AutomationName, "Screen readers still hear the variables.");

            TextExpanderRowDescription unnamed = Describe("!n", "Note: {{prompt:}}");
            Assert.IsNull(unnamed.Sentence, "A question without a label can't be named, so no sentence.");
            Assert.AreEqual("!n: Note: (asks for Your answer)", unnamed.AutomationName);

            Assert.AreEqual("Inserts what you copied", Describe("!c", "https://{{clipboard}}").Sentence, "No host, so not a link.");

            TextExpanderRowDescription thanks = Describe("#THX", "Thanks in advance,\nYour Name");
            Assert.IsNull(thanks.Sentence);
            Assert.AreEqual("#THX: Thanks in advance, (new line) Your Name, 2 lines", thanks.AutomationName, "Screen readers hear how many lines there are.");
        }

        [TestMethod]
        public void LinksKeepTheirHostAndTheEndOfTheirPath()
        {
            Assert.AreEqual("contoso.com:8080", TextExpanderRowDescriber.ExtractHost("https://user@contoso.com:8080/a"));
            Assert.AreEqual("www.contoso.com", TextExpanderRowDescriber.ExtractHost("www.contoso.com/a"));
            Assert.IsNull(TextExpanderRowDescriber.ExtractHost("https://?q=1"));
            Assert.IsNull(TextExpanderRowDescriber.ExtractHost("ftp://contoso.com"));

            Assert.AreEqual("contoso.com/a/b see you", TextExpanderRowDescriber.ShortenLink("HTTPS://contoso.com/a/b see you", 60));
            string shortened = TextExpanderRowDescriber.ShortenLink("https://contoso.sharepoint.com/sites/Team/Shared%20Documents/Forms/AllItems.aspx/report", 60);
            Assert.AreEqual("contoso.sharepoint.com/\u2026/Forms/AllItems.aspx/report", shortened);
            Assert.IsTrue(shortened.Length <= 60);
            Assert.IsTrue(TextExpanderRowDescriber.ShortenLink("https://contoso.com/" + new string('x', 80), 40).EndsWith("xxxx", StringComparison.Ordinal), "The end stays visible.");
        }

        [TestMethod]
        public void NeverExpandsFollowsTheEngineInEachMode()
        {
            // #P$: $ is a symbol, so #P fires first in both modes.
            TextExpanderRowDescription instant = Describe("#P$", "R. Ortiz / M. Chen / [$|$]", UserTriggers, wordMode: false);
            Assert.AreEqual("Never expands: \"#P\" fires first. Rename one of them.", instant.Warning);
            Assert.IsTrue(instant.AutomationName.EndsWith(". Never expands: \"#P\" fires first. Rename one of them.", StringComparison.Ordinal));

            TextExpanderRowDescription word = Describe("#P$", "R. Ortiz / M. Chen / [$|$]", UserTriggers, wordMode: true);
            Assert.AreEqual("Never expands: \"#P\" fires first when you type \"$\". Rename one of them.", word.Warning);

            // #DIO: blocked by #D only without the word wait, and the warning says so.
            Assert.AreEqual(
                "Never expands: \"#D\" fires first. Rename one, or turn on Wait for the whole word.",
                Describe("#DIO", "12345678", UserTriggers, wordMode: false).Warning);
            Assert.IsNull(Describe("#DIO", "12345678", UserTriggers, wordMode: true).Warning);

            // A trigger inside a word doesn't fire with the word wait, as in the engine.
            string[] btw = { "btw", "abtw!" };
            Assert.IsNull(Describe("abtw!", "x", btw, wordMode: true).Warning);
            Assert.IsNotNull(Describe("abtw!", "x", btw, wordMode: false).Warning);
            Assert.AreEqual(
                "Never expands: \"my\" fires first when you type a space. Rename one of them.",
                Describe("my sig", "x", SpaceTriggers, wordMode: true).Warning);

            Assert.IsNull(Describe("#P", "Would you please let me know your thoughts!", UserTriggers, wordMode: false).Warning);
        }

        [TestMethod]
        public void OnlyTheLastOfTwoIdenticalTriggersExpands()
        {
            TextExpanderRowDescription first = TextExpanderRowDescriber.Describe("#A", "one", hasLaterDuplicate: true, Set("#A"), false, Now, Resource);
            Assert.AreEqual("Never expands: the \"#A\" further down the list is used instead.", first.Warning);
            Assert.IsNull(TextExpanderRowDescriber.Describe("#A", "two", hasLaterDuplicate: false, Set("#A"), false, Now, Resource).Warning);
        }

        [TestMethod]
        public void TheNeverExpandsCheckAllocatesNothingForLongTriggers()
        {
            // PERF-2: 1,000 triggers of 256 characters used to allocate about 6 GB, one string per
            // substring checked. Short triggers of many lengths make every length get looked up.
            var triggers = new TextExpanderTriggerSet(
                Enumerable.Range(0, 200).Select(i => new string('a', 251) + i.ToString("D5", System.Globalization.CultureInfo.InvariantCulture))
                    .Concat(Enumerable.Range(1, 30).Select(length => new string('x', length))));
            string[] longTriggers = triggers.Triggers.Where(trigger => trigger.Length > 200).ToArray();
            _ = TextExpanderTriggerReach.FindBlocker("warm up", triggers, wordMode: false);

            long before = GC.GetAllocatedBytesForCurrentThread();
            foreach (string trigger in longTriggers)
            {
                Assert.IsNull(TextExpanderTriggerReach.FindBlocker(trigger, triggers, wordMode: false));
                Assert.IsNull(TextExpanderTriggerReach.FindBlocker(trigger, triggers, wordMode: true));
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allocated < 16 * 1024, $"{allocated} bytes");

            // Still finds the trigger the engine would fire: the longest one ending at the space.
            Assert.AreEqual(("xxx", ' '), TextExpanderTriggerReach.FindBlocker("-xxx yz", triggers, wordMode: true));
            Assert.AreEqual(("x", 'x'), TextExpanderTriggerReach.FindBlocker("axxxyz", triggers, wordMode: false), "Without the wait, x fires as soon as it's typed.");
        }

        [TestMethod]
        public void RowsAreDescribedWhenLoadedAndWhenTheWordWaitChanges()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpanderRows-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "snippets.txt"), "#D=[{{date:MM/dd/yyyy}}]\n#DIO=12345678\n#A=one\n#A=two\n");
                TextExpanderViewModel model = CreateViewModel(directory);

                TextExpanderSnippetViewModel dio = model.Snippets.Single(s => s.Trigger == "#DIO");
                Assert.IsTrue(dio.HasWarning);
                Assert.IsTrue(model.Snippets.First(s => s.Trigger == "#A").HasWarning, "The first #A is never used.");
                Assert.IsFalse(model.Snippets.Last(s => s.Trigger == "#A").HasWarning);
                Assert.IsTrue(model.Snippets.Single(s => s.Trigger == "#D").HasSentence);

                model.RequireWordBoundary = true;
                Assert.IsFalse(dio.HasWarning, "With the word wait, #DIO can be reached.");
                Assert.AreEqual("#DIO", dio.Trigger);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void TheKeycapColumnFitsTheWidestTriggerWithinItsLimits()
        {
            double chrome = TextExpanderKeycapLayout.Chrome;
            Assert.AreEqual(TextExpanderKeycapLayout.MinWidth, TextExpanderKeycapLayout.ColumnWidth(Array.Empty<double>()), "An empty list still has a key-sized column.");
            Assert.AreEqual(TextExpanderKeycapLayout.MinWidth, TextExpanderKeycapLayout.ColumnWidth(TinyWidths));
            Assert.AreEqual(Math.Ceiling(40.2 + chrome), TextExpanderKeycapLayout.ColumnWidth(MixedWidths), "The widest keycap sets the column, rounded up.");
            Assert.AreEqual(TextExpanderKeycapLayout.MaxWidth, TextExpanderKeycapLayout.ColumnWidth(LongWidths), "Long triggers are trimmed at the cap.");
            Assert.AreEqual(Math.Ceiling(30 + chrome), TextExpanderKeycapLayout.ColumnWidth(BadWidths), "Bad measurements are ignored.");
        }

        [TestMethod]
        public void EveryRowUsesTheSameKeycapColumnWidthIncludingRowsLoadedLater()
        {
            string directory = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpanderRows-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string file = Path.Combine(directory, "snippets.txt");
                File.WriteAllText(file, "#A=one\n#DIO=two\n");
                TextExpanderViewModel model = CreateViewModel(directory);

                model.KeycapColumnWidth = 57;
                Assert.IsTrue(model.Snippets.All(s => s.KeycapColumnWidth == 57));

                File.WriteAllText(file, "#A=one\n#DIO=two\n#P$=three\n");
                model.LoadSnippets();
                Assert.AreEqual(3, model.Snippets.Count);
                Assert.IsTrue(model.Snippets.All(s => s.KeycapColumnWidth == 57), "New rows start at the current width.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [TestMethod]
        public void SentencesShowEachVariableAsABubbleInTheTemplatesSlots()
        {
            TextExpanderRowDescription one = Describe("!test", "{{prompt:what type}}");
            Assert.AreEqual("Asks for what type, then inserts the text", one.Sentence);
            CollectionAssert.AreEqual(
                new[] { Text("Asks for "), Bubble(Kind.Prompt, "what type"), Text(", then inserts the text") },
                one.SentenceParts.ToArray());

            // Several questions keep the localized joining words between their bubbles.
            CollectionAssert.AreEqual(
                new[] { Text("Asks for "), Bubble(Kind.Prompt, "A"), Text(" and "), Bubble(Kind.Prompt, "B"), Text(", then inserts the text") },
                Describe("!ab", "{{prompt:A}} {{prompt:B}}").SentenceParts.ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    Text("Link to x.com, asks for "), Bubble(Kind.Prompt, "A"), Text(", "), Bubble(Kind.Prompt, "B"), Text(" and "), Bubble(Kind.Prompt, "C"),
                },
                Describe("!x", "https://x.com/{{prompt:A}}/{{prompt:B}}/{{prompt:C}}").SentenceParts.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("Link to tracker.example.com using "), Bubble(Kind.Clipboard, "what you copied"), Text(", asks for "), Bubble(Kind.Prompt, "Ticket") },
                Describe("!t", "https://tracker.example.com/{{prompt:Ticket}}?q={{clipboard}}").SentenceParts.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("Inserts "), Bubble(Kind.Clipboard, "what you copied"), Text(", in 2 lines") },
                Describe("!p", "{{clipboard}}\\n").SentenceParts.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("The current time as "), Bubble(Kind.Time, "02:05 PM") },
                Describe("!t", "{{time}}").SentenceParts.ToArray());

            // A translation may put the slots in another order.
            var reordered = new Dictionary<string, string>(Templates) { ["TextExpander_Row_LinkClipboardPrompts"] = "{2} → {0} ({1})" };
            TextExpanderRowDescription moved = TextExpanderRowDescriber.Describe(
                "!t", "https://tracker.example.com/{{prompt:Ticket}}?q={{clipboard}}", false, Set("!t"), false, Now, key => reordered.TryGetValue(key, out string v) ? v : key);
            CollectionAssert.AreEqual(
                new[] { Bubble(Kind.Prompt, "Ticket"), Text(" → tracker.example.com ("), Bubble(Kind.Clipboard, "what you copied"), Text(")") },
                moved.SentenceParts.ToArray());
        }

        [TestMethod]
        public void TheSavedTextGoesUnderTheSentenceOnlyWhenItHasWordsOfItsOwn()
        {
            Assert.IsFalse(Describe("!test", "{{prompt:what type}}").ShowsSegmentsBelow, "Only a question.");
            Assert.IsFalse(Describe("!c", "{{clipboard}}").ShowsSegmentsBelow, "Only the clipboard.");
            Assert.IsFalse(Describe("#D", "[{{date:MM/dd/yyyy}}]").ShowsSegmentsBelow, "Only a date in brackets.");
            Assert.IsFalse(Describe("!dt", "{{date}} - {{time}}").ShowsSegmentsBelow, "Dates and punctuation.");
            Assert.IsTrue(Describe("!hi", "Hi {{prompt:Name}}, thanks for reaching out!").ShowsSegmentsBelow, "Words around a question.");
            Assert.IsTrue(Describe("#A", "https://contoso.visualstudio.com/OS/_workitems/edit/{{clipboard}}").ShowsSegmentsBelow, "The link's path.");
            Assert.IsTrue(Describe("!no", "Ticket {{prompt:Number}}: 42").ShowsSegmentsBelow, "Digits count as words.");
        }

        [TestMethod]
        public void PlainSnippetsShowUpToTwoRealLinesWithoutChips()
        {
            // #P: a message, a blank line, then the sign-off. The blank line is skipped, and the
            // rest is cut after two lines with an ellipsis.
            TextExpanderRowDescription p = Describe("#P", "Would you please let me know your thoughts!\\n\\nHave a good one,\\nYour Name");
            CollectionAssert.AreEqual(
                new[] { Text("Would you please let me know your thoughts!"), Break(), Text("Have a good one,"), Text("\u2026") },
                p.TitleSegments.ToArray());
            Assert.AreEqual("#P: Would you please let me know your thoughts! (new line) (new line) Have a good one, (new line) Your Name, 4 lines", p.AutomationName);

            // Exactly two lines: both, with no ellipsis.
            CollectionAssert.AreEqual(
                new[] { Text("Thanks in advance,"), Break(), Text("Your Name") },
                Describe("#THX", "Thanks in advance,\nYour Name").TitleSegments.ToArray());

            // One line stays one line; tabs stay tabs, which show as spacing.
            CollectionAssert.AreEqual(new[] { Text("12345678") }, Describe("#DIO", "12345678").TitleSegments.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("Name"), Tab(), Text("Email") },
                Describe("!tab", "Name\\tEmail").TitleSegments.ToArray());

            // Leading blank lines don't use up the row either, and bubbles stay inline.
            CollectionAssert.AreEqual(
                new[] { Text("Updated "), Bubble(Kind.Date, "10/17/26") },
                Describe("!u", "\\n\\nUpdated {{date}}").TitleSegments.ToArray());

            // A sentence row has no title text; nothing in a row is a chip any more.
            Assert.AreEqual(0, Describe("!c", "{{clipboard}}").TitleSegments.Count);
            Assert.IsFalse(p.TitleSegments.Any(s => s.Value.Contains("lines", StringComparison.Ordinal)), "The line count is spoken, not shown.");
        }

        [TestMethod]
        public void ARowFindsItsSnippetInItsTagBecauseTheListSetsNoDataContext()
        {
            // The regression: the list is an ItemsRepeater, which sets no DataContext on rows built
            // with x:Bind, so a row's DataContext is the page's view model. Selecting a row did
            // nothing; only the menu items, which carry their snippet in Tag, still worked.
            string directory = Path.Combine(Path.GetTempPath(), "PowerToys-TextExpanderRows-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "snippets.txt"), "#sig=Regards\n#mail=you@example.com\n");
                TextExpanderViewModel page = CreateViewModel(directory);
                TextExpanderSnippetViewModel row = page.FilteredSnippets.Single(s => s.Trigger == "#mail");

                Assert.AreSame(row, TextExpanderRowSnippet.Resolve(row, page), "The row's Tag names its snippet while its DataContext is the page's.");
                Assert.AreSame(row, TextExpanderRowSnippet.Resolve(null, row), "A list that does set DataContext still works.");
                Assert.AreSame(row, TextExpanderRowSnippet.Resolve(row, page.FilteredSnippets.Single(s => s.Trigger == "#sig")), "The Tag wins: x:Bind keeps it current as rows are reused.");
                Assert.IsNull(TextExpanderRowSnippet.Resolve(null, page), "Without its Tag a row can't tell which snippet it is, so nothing opens rather than the wrong one.");
                Assert.IsNull(TextExpanderRowSnippet.Resolve("!mysig", page), "The example buttons carry a trigger, not a row.");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static TextExpanderPreviewSegment Text(string value) => new(Kind.Text, value);

        private static TextExpanderPreviewSegment Bubble(Kind kind, string value) => new(kind, value);

        private static TextExpanderPreviewSegment Break() => new(Kind.LineBreak);

        private static TextExpanderPreviewSegment Tab() => new(Kind.Tab);

        private static TextExpanderRowDescription Describe(string trigger, string replacement, string[] triggers = null, bool wordMode = false)
            => TextExpanderRowDescriber.Describe(trigger, replacement, false, Set(triggers ?? new[] { trigger }), wordMode, Now, Resource);

        private static TextExpanderTriggerSet Set(params string[] triggers) => new(triggers);

        private static string Resource(string key) => Templates.TryGetValue(key, out string value) ? value : key;

        private static TextExpanderViewModel CreateViewModel(string directory)
        {
            var settings = new TextExpanderSettings();
            settings.Properties.SnippetsPath.Value = directory;
            var utils = new Mock<SettingsUtils>(new FileSystem(), null);
            utils.Setup(u => u.GetSettingsOrDefault<TextExpanderSettings>(TextExpanderSettings.ModuleName, SettingsUtils.DefaultFileName)).Returns(settings);
            var repository = new Mock<ISettingsRepository<GeneralSettings>>();
            repository.SetupGet(r => r.SettingsConfig).Returns(new GeneralSettings());
            return new TextExpanderViewModel(utils.Object, repository.Object, _ => 0, null, false, Resource);
        }
    }
}
