// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Kind = Microsoft.PowerToys.TextExpander.Editor.Helpers.TextExpanderPreviewSegmentKind;

namespace ViewModelTests
{
    /// <summary>
    /// The snippet list's rows in Keyboard Manager's style: the questions a snippet asks, an accent chip with what it
    /// inserts as its real lines, why it never expands, and what a screen reader says.
    /// </summary>
    [TestClass]
    public class TextExpanderRows
    {
        private const string DateGlyph = "\uE787";
        private const string TimeGlyph = "\uE823";
        private const string ClipboardGlyph = "\uE77F";
        private const string PromptGlyph = "\uE70F";

        // A Saturday afternoon. Only numeric date formats are used, so the region doesn't matter.
        private static readonly DateTime Now = new(2026, 10, 17, 14, 5, 9);

        private static readonly Dictionary<string, string> Templates = new()
        {
            ["TextExpander_Row_Inserts"] = "inserts",
            ["TextExpander_Row_AsksThenInserts"] = "asks {0} then inserts",
            ["TextExpander_Row_NeverExpandsDuplicate"] = "Another \"{0}\" further down the list is used instead.",
            ["TextExpander_Row_NeverExpandsFiresFirst"] = "\"{0}\" fires first. Rename one of them.",
            ["TextExpander_Row_NeverExpandsFiresFirstWordWait"] = "\"{0}\" fires first. Rename one, or turn on Wait for space or punctuation in PowerToys Settings.",
            ["TextExpander_Row_NeverExpandsWhenYouType"] = "\"{0}\" fires first when you type \"{1}\". Rename one of them.",
            ["TextExpander_Row_NeverExpandsWhenYouTypeSpace"] = "\"{0}\" fires first when you type a space. Rename one of them.",
            ["TextExpander_Row_NeverExpandsSpoken"] = "Never expands: {0}",
            ["TextExpander_Row_SpokenNewLine"] = "new line",
            ["TextExpander_Row_SpokenTab"] = "tab",
            ["TextExpander_Row_SpokenClipboard"] = "what you copied",
            ["TextExpander_Row_SpokenPrompt"] = "asks for {0}",
            ["TextExpander_Row_SpokenCursor"] = "cursor",
            ["TextExpander_Row_SpokenLines"] = "{0}, {1} lines",
            ["TextExpander_Row_AutomationName"] = "{0}: {1}",
            ["TextExpander_Row_AutomationNameWarning"] = "{0}: {1}. {2}",
            ["TextExpander_PreviewClipboard"] = "Clipboard",
            ["TextExpander_PreviewPromptUnnamed"] = "Your answer",
        };

        private static readonly string[] SampleTriggers = { "!reply", "!reply+", "!date", "!dateiso" };
        private static readonly string[] SpaceTriggers = { "my", "my sig" };

        [TestMethod]
        public void PlainTextShowsItsRealLinesThreeAtMost()
        {
            TextExpanderRowDescription signature = Describe(";sig", "Best regards,\nAlex Wilber\nContoso Ltd.");
            Assert.AreEqual(TextExpanderRowDescriber.TextGlyph, signature.KindGlyph);
            CollectionAssert.AreEqual(new[] { Text("inserts") }, signature.Lead.ToArray());
            CollectionAssert.AreEqual(new[] { Text("Best regards,"), Break(), Text("Alex Wilber"), Break(), Text("Contoso Ltd.") }, signature.Chip.ToArray());

            // More lines end the third one with "…".
            CollectionAssert.AreEqual(
                new[] { Text("One"), Break(), Text("Two"), Break(), Text("Three \u2026") },
                Describe(";five", "One\nTwo\nThree\nFour\nFive").Chip.ToArray());

            // A blank line between paragraphs is a real line, and counts as one; blank lines at either end don't show.
            CollectionAssert.AreEqual(
                new[] { Text("Hi "), new TextExpanderPreviewSegment(Kind.Cursor), Text(","), Break(), Break(), Text("Thanks for reaching out.") },
                Describe(";hi", "Hi $|$,\n\nThanks for reaching out.").Chip.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("Thanks"), Break(), Break(), Text("Alex \u2026") },
                Describe(";thx", "\n\nThanks\n\nAlex\nContoso\n\n").Chip.ToArray());
            CollectionAssert.AreEqual(new[] { Text("Updated") }, Describe(";u", "Updated\n").Chip.ToArray());
            Assert.AreEqual(0, Describe(";blank", "\n \t\n").Chip.Count);

            // Tabs stay tabs, which show as spacing.
            CollectionAssert.AreEqual(
                new[] { Text("Name"), Tab(), Text("Role"), Tab(), Text("Team") },
                Describe(";tbl", "Name\tRole\tTeam").Chip.ToArray());
        }

        [TestMethod]
        public void ValuesAreBubblesInTheChipUnlessTheSnippetIsOnlyValues()
        {
            TextExpanderRowDescription meeting = Describe(";mtg", "Meeting notes · {{date:yyyy-MM-dd}}\nProject: {{prompt:Project name}}\nAttendees: {{prompt:Attendees}}\nDecisions:");
            CollectionAssert.AreEqual(new[] { Text("asks "), Bubble(Kind.Prompt, "Project name"), Text(" "), Bubble(Kind.Prompt, "Attendees"), Text(" then inserts") }, meeting.Lead.ToArray());
            Assert.AreEqual(TextExpanderRowDescriber.TextGlyph, meeting.KindGlyph, "Questions don't change what a snippet inserts.");
            CollectionAssert.AreEqual(
                new[]
                {
                    Text("Meeting notes · "), Bubble(Kind.Date, "2026-10-17"), Break(),
                    Text("Project: "), Bubble(Kind.Prompt, "Project name"), Break(),
                    Text("Attendees: "), Bubble(Kind.Prompt, "Attendees"), Text(" \u2026"),
                },
                meeting.Chip.ToArray());

            TextExpanderRowDescription link = Describe(";link", "Here is the link we discussed: {{clipboard}}");
            Assert.AreEqual(ClipboardGlyph, link.KindGlyph);
            CollectionAssert.AreEqual(new[] { Text("Here is the link we discussed: "), new TextExpanderPreviewSegment(Kind.Clipboard) }, link.Chip.ToArray());
            Assert.AreEqual(TextExpanderRowDescriber.TextGlyph, Describe(";d", "Updated {{date:yyyy-MM-dd}}").KindGlyph, "A date among words is a bubble in text.");
            Assert.AreEqual(TextExpanderRowDescriber.TextGlyph, Describe(";dd", "{{date:yyyy-MM-dd}}\n{{date:HH:mm}}").KindGlyph, "So are dates on several lines.");

            // A snippet that is only dates and times with punctuation, or one value, shows them as text with their icon.
            TextExpanderRowDescription iso = Describe(";iso", "[{{date:yyyy-MM-dd}}] {{date:HH:mm}}");
            Assert.AreEqual(DateGlyph, iso.KindGlyph, "A date and a time are a date.");
            CollectionAssert.AreEqual(new[] { Text("[2026-10-17] 14:05") }, iso.Chip.ToArray());

            TextExpanderRowDescription now = Describe(";now", "{{date:HH:mm}}");
            Assert.AreEqual(TimeGlyph, now.KindGlyph);
            CollectionAssert.AreEqual(new[] { Text("14:05") }, now.Chip.ToArray());

            TextExpanderRowDescription paste = Describe(";p", "{{clipboard}}");
            Assert.AreEqual(ClipboardGlyph, paste.KindGlyph);
            CollectionAssert.AreEqual(new[] { Text("Clipboard") }, paste.Chip.ToArray());

            TextExpanderRowDescription topic = Describe(";t", "{{prompt:Topic}}");
            Assert.AreEqual(PromptGlyph, topic.KindGlyph);
            CollectionAssert.AreEqual(new[] { Text("asks "), Bubble(Kind.Prompt, "Topic"), Text(" then inserts") }, topic.Lead.ToArray());
            CollectionAssert.AreEqual(new[] { Text("Topic") }, topic.Chip.ToArray());

            // The clipboard picks the icon of a snippet that also asks.
            TextExpanderRowDescription ticket = Describe(";tk", "{{prompt:Ticket}}: {{clipboard}}");
            Assert.AreEqual(ClipboardGlyph, ticket.KindGlyph);
            CollectionAssert.AreEqual(new[] { Bubble(Kind.Prompt, "Ticket"), Text(": "), new TextExpanderPreviewSegment(Kind.Clipboard) }, ticket.Chip.ToArray());
        }

        [TestMethod]
        public void QuestionsAreAskedOnceInOrderBetweenTheVerbs()
        {
            CollectionAssert.AreEqual(new[] { Text("inserts") }, Describe(";plain", "Thanks!").Lead.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("asks "), Bubble(Kind.Prompt, "B"), Text(" "), Bubble(Kind.Prompt, "A"), Text(" "), Bubble(Kind.Prompt, "C"), Text(" then inserts") },
                Describe(";q", "{{prompt:B}} {{prompt:A}}\n{{prompt:B}} {{prompt?:C}}").Lead.ToArray());
            CollectionAssert.AreEqual(
                new[] { Text("asks "), Bubble(Kind.Prompt, "Your answer"), Text(" then inserts") },
                Describe(";n", "Note: {{prompt:}} and {{prompt:}}").Lead.ToArray(),
                "A question without a label is one answer.");

            // A translation may put the questions anywhere in its words.
            var reordered = new Dictionary<string, string>(Templates) { ["TextExpander_Row_AsksThenInserts"] = "after {0}, inserts" };
            TextExpanderRowDescription moved = TextExpanderRowDescriber.Describe(";t", "{{prompt:Topic}}!", false, Set(";t"), false, Now, key => reordered.TryGetValue(key, out string value) ? value : key);
            CollectionAssert.AreEqual(new[] { Text("after "), Bubble(Kind.Prompt, "Topic"), Text(", inserts") }, moved.Lead.ToArray());
        }

        [TestMethod]
        public void NeverExpandsSaysWhyAndTheRowsNameSaysItOnce()
        {
            TextExpanderRowDescription first = TextExpanderRowDescriber.Describe(";fab", "Fabrikam, Inc.", hasLaterDuplicate: true, Set(";fab"), false, Now, Resource);
            Assert.AreEqual("Another \";fab\" further down the list is used instead.", first.Warning);

            // E06: the snippet's own final period isn't doubled before the warning.
            Assert.AreEqual(";fab: Fabrikam, Inc. Never expands: Another \";fab\" further down the list is used instead.", first.AutomationName);
            Assert.IsNull(TextExpanderRowDescriber.Describe(";fab", "Fabrikam, Inc.", hasLaterDuplicate: false, Set(";fab"), false, Now, Resource).Warning);
            Assert.AreEqual(
                ";x: Never expands: Another \";x\" further down the list is used instead.",
                TextExpanderRowDescriber.Describe(";x", string.Empty, hasLaterDuplicate: true, Set(";x"), false, Now, Resource).AutomationName,
                "Without text to read, the name is the trigger and the warning.");

            // The reachability rules are the engine's, in each mode.
            Assert.AreEqual("\"!reply\" fires first. Rename one of them.", Describe("!reply+", "R. Ortiz", SampleTriggers, wordMode: false).Warning);
            Assert.AreEqual("\"!reply\" fires first when you type \"+\". Rename one of them.", Describe("!reply+", "R. Ortiz", SampleTriggers, wordMode: true).Warning);
            Assert.AreEqual(
                "\"!date\" fires first. Rename one, or turn on Wait for space or punctuation in PowerToys Settings.",
                Describe("!dateiso", "ABC-123", SampleTriggers, wordMode: false).Warning);
            Assert.IsNull(Describe("!dateiso", "ABC-123", SampleTriggers, wordMode: true).Warning);
            Assert.IsNull(Describe("!reply", "Thanks for your message!", SampleTriggers, wordMode: false).Warning);

            // A trigger inside a word doesn't fire with the word wait, as in the engine.
            string[] btw = { "btw", "abtw!" };
            Assert.IsNull(Describe("abtw!", "x", btw, wordMode: true).Warning);
            Assert.IsNotNull(Describe("abtw!", "x", btw, wordMode: false).Warning);
            Assert.AreEqual("\"my\" fires first when you type a space. Rename one of them.", Describe("my sig", "x", SpaceTriggers, wordMode: true).Warning);
        }

        [TestMethod]
        public void TheRowsNameReadsItsWholeTextWithItsLineCount()
        {
            Assert.AreEqual(";sig: Best regards, (new line) Alex Wilber, 2 lines", Describe(";sig", "Best regards,\nAlex Wilber").AutomationName);
            Assert.AreEqual(";hello: Hello (cursor) , welcome to the team!", Describe(";hello", "Hello $|$, welcome to the team!").AutomationName);
            Assert.AreEqual(
                ";mtg: Project: (asks for Project name) (new line) Attendees: (asks for Attendees), 2 lines",
                Describe(";mtg", "Project: {{prompt:Project name}}\nAttendees: {{prompt:Attendees}}").AutomationName,
                "Each question is heard where it's asked.");
            Assert.AreEqual(";p: (what you copied)", Describe(";p", "{{clipboard}}").AutomationName);
            Assert.AreEqual(";x", Describe(";x", string.Empty).AutomationName);
            StringAssert.EndsWith(Describe(";five", "One\nTwo\nThree\nFour\nFive").AutomationName, "Four (new line) Five, 5 lines", "Every line is read, not only the three the chip shows.");
        }

        [TestMethod]
        public void AVeryLongSnippetIsDescribedFromItsStart()
        {
            // The chip and the row's name stop where they're full, however long the text goes on.
            string replacement = "First   line\n\n" + string.Join("\n", Enumerable.Repeat("more  text", 100_000));
            TextExpanderRowDescription row = Describe(";long", replacement);

            CollectionAssert.AreEqual(new[] { Text("First   line"), Break(), Break(), Text("more  text \u2026") }, row.Chip.ToArray());
            StringAssert.StartsWith(row.AutomationName, ";long: First line (new line) (new line) more text (new line) more text");
            StringAssert.EndsWith(row.AutomationName, "\u2026, 100002 lines");
            Assert.AreEqual(";long: ".Length + 170 + ", 100002 lines".Length, row.AutomationName.Length, "What's read aloud stops at 170 characters.");
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
        public void RowsUseTheBoundaryDefaultAndRefreshWhenItChanges()
        {
            using var folder = new TempFolder();
            File.WriteAllText(Path.Combine(folder.Path, "snippets.json"), SnippetJson.Of("!date=[{{date:yyyy-MM-dd}}]", "!dateiso=ABC-123", "!link=one", "!link=two"));
            bool wait = true;
            TextExpanderViewModel model = CreateViewModel(folder.Path, () => wait);

            TextExpanderSnippetViewModel dateIso = model.Snippets.Single(s => s.Trigger == "!dateiso");
            Assert.IsTrue(model.RequireWordBoundary);
            Assert.IsFalse(dateIso.HasWarning, "The default wait lets !dateiso be reached.");
            Assert.IsTrue(model.Snippets.First(s => s.Trigger == "!link").HasWarning, "The first !link is never used.");
            Assert.IsFalse(model.Snippets.Last(s => s.Trigger == "!link").HasWarning);
            Assert.AreEqual(DateGlyph, model.Snippets.Single(s => s.Trigger == "!date").KindGlyph, "Only a date in brackets: the date's icon.");

            // Settings turns the wait off, then on again.
            wait = false;
            model.RefreshSettings();
            Assert.IsTrue(dateIso.HasWarning, "Without the wait, !date fires before !dateiso is complete.");

            wait = true;
            model.RefreshSettings();
            Assert.IsFalse(dateIso.HasWarning, "Restoring the wait lets !dateiso be reached again.");
            Assert.AreEqual("!dateiso", dateIso.Trigger);
        }

        [TestMethod]
        public void EveryRowGetsItsDescriptionAndTheListItsHeading()
        {
            using var folder = new TempFolder();
            File.WriteAllText(Path.Combine(folder.Path, "snippets.json"), SnippetJson.Of(";mtg=Project: {{prompt:Project name}}", ";fab=one", ";fab=two"));
            TextExpanderViewModel model = CreateViewModel(folder.Path);

            TextExpanderSnippetViewModel meeting = model.Snippets[0];
            CollectionAssert.AreEqual(new[] { Text("asks "), Bubble(Kind.Prompt, "Project name"), Text(" then inserts") }, meeting.LeadSegments.ToArray());
            Assert.AreEqual(TextExpanderRowDescriber.TextGlyph, meeting.KindGlyph);
            CollectionAssert.AreEqual(new[] { Text("Project: "), Bubble(Kind.Prompt, "Project name") }, meeting.ChipSegments.ToArray());
            Assert.IsTrue(model.Snippets[1].HasWarning, "The first ;fab is never used.");
            Assert.IsFalse(model.Snippets[2].HasWarning);

            // The list reads a row's name from its item's text.
            Assert.AreEqual(meeting.AutomationName, meeting.ToString());
            StringAssert.StartsWith(meeting.AutomationName, ";mtg: Project:");
            Assert.IsTrue(model.CanAddSnippets);
            Assert.IsTrue(model.ShowsSnippetCount);

            // An empty library shows its empty state instead of the heading and count (E35).
            File.WriteAllText(Path.Combine(folder.Path, "snippets.json"), SnippetJson.Of());
            model.LoadSnippets();
            Assert.IsTrue(model.HasNoSnippets);
            Assert.IsFalse(model.ShowsSnippetCount);
            Assert.IsTrue(model.CanAddSnippets);

            // A file that can't be read can't be added to.
            using (new FileStream(Path.Combine(folder.Path, "snippets.json"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                model.LoadSnippets();
            }

            Assert.IsTrue(model.HasSnippetError);
            Assert.IsFalse(model.CanAddSnippets);
            Assert.IsTrue(model.ShowsSnippetCount, "The heading stays, with the count unavailable.");
            Assert.IsFalse(new TextExpanderViewModel(EditorSettings.Store(EditorSettings.For(Path.Combine(folder.Path, "snippets.json"))).Object, true, Resource).CanAddSnippets, "Nor can one opened as administrator.");
        }

        private static TextExpanderPreviewSegment Text(string value) => new(Kind.Text, value);

        private static TextExpanderPreviewSegment Bubble(Kind kind, string value) => new(kind, value);

        private static TextExpanderPreviewSegment Break() => new(Kind.LineBreak);

        private static TextExpanderPreviewSegment Tab() => new(Kind.Tab);

        private static TextExpanderRowDescription Describe(string trigger, string replacement, string[] triggers = null, bool wordMode = false)
        {
            using var culture = new CultureScope("en-US");
            return TextExpanderRowDescriber.Describe(trigger, replacement, false, Set(triggers ?? new[] { trigger }), wordMode, Now, Resource);
        }

        private static TextExpanderTriggerSet Set(params string[] triggers) => new(triggers);

        private static string Resource(string key) => Templates.TryGetValue(key, out string value) ? value : key;

        private static TextExpanderViewModel CreateViewModel(string directory, Func<bool> requireWordBoundary = null)
        {
            // What the editor reads back after Settings changes Wait for space or punctuation.
            Func<TextExpanderSettings> latest = requireWordBoundary == null ? null : () =>
            {
                TextExpanderSettings settings = EditorSettings.For(Path.Combine(directory, "snippets.json"));
                settings.Properties.RequireWordBoundary = requireWordBoundary();
                return settings;
            };
            return new TextExpanderViewModel(EditorSettings.Store(EditorSettings.For(Path.Combine(directory, "snippets.json")), latest).Object, false, Resource);
        }
    }
}
