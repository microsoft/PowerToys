// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// Rows too long for their lines. The sentence and the title keep their bubbles and cursor bar
    /// while they fit, and show as text once WinUI trims them, since an element past the ellipsis
    /// is drawn over the start of the line.
    /// </summary>
    [TestClass]
    public class TextExpanderLongRows
    {
        private const string LongCursorLine = "Thanks for the update on the quarterly planning review. I will follow up with the full notes, the decisions we made and the action items $|$ by Friday afternoon.";

        private const string LongQuestions = "{{prompt:Name of the customer's primary contact person}}, {{prompt:Their direct phone number}}, {{prompt:Preferred meeting time and time zone}}";

        private static readonly DateTime Now = new(2026, 10, 18, 16, 2, 9);

        private static readonly string[] Questions = { "Name of the customer's primary contact person", "Their direct phone number", "Preferred meeting time and time zone" };

        private static readonly Dictionary<string, string> Templates = new()
        {
            ["TextExpander_Row_Prompts"] = "Asks for {0}, then inserts the text",
            ["TextExpander_Row_JoinPair"] = "{0} and {1}",
            ["TextExpander_Row_JoinSeparator"] = ", ",
            ["TextExpander_Row_SpokenCursor"] = "cursor",
            ["TextExpander_Row_AutomationName"] = "{0}: {1}",
            ["TextExpander_PreviewPromptUnnamed"] = "Your answer",
        };

        [TestMethod]
        public void ALongTitleWithTheCursorCanShowAsText()
        {
            TextExpanderRowDescription row = Describe("!zlong", LongCursorLine);
            Assert.IsNull(row.Sentence);

            // With bubbles, the cursor is its bar: an element, which a trimmed line draws over its start.
            var bubbles = TextExpanderInlines.Plan(row.TitleSegments, breaksAsMarkers: false, textOnly: false, Resource);
            Assert.AreEqual(1, bubbles.Count(piece => piece.Kind == TextExpanderInlineKind.CursorBar));

            // As text, nothing is an element, and the text and the cursor stay where they are.
            var text = TextExpanderInlines.Plan(row.TitleSegments, breaksAsMarkers: false, textOnly: true, Resource);
            Assert.IsFalse(text.Any(piece => piece.IsElement));
            Assert.AreEqual(LongCursorLine.Replace("$|$", "|", StringComparison.Ordinal), string.Concat(text.Select(piece => piece.Text)));
        }

        [TestMethod]
        public void ALongSentenceCanShowAsTextWithTheSameLabels()
        {
            TextExpanderRowDescription row = Describe("!zasks", LongQuestions);
            const string Sentence = "Asks for Name of the customer's primary contact person, Their direct phone number and Preferred meeting time and time zone, then inserts the text";
            Assert.AreEqual(Sentence, row.Sentence);

            var bubbles = TextExpanderInlines.Plan(row.SentenceParts, breaksAsMarkers: false, textOnly: false, Resource);
            CollectionAssert.AreEqual(Questions, bubbles.Where(piece => piece.Kind == TextExpanderInlineKind.Bubble).Select(piece => piece.Text).ToArray());

            var text = TextExpanderInlines.Plan(row.SentenceParts, breaksAsMarkers: false, textOnly: true, Resource);
            Assert.IsFalse(text.Any(piece => piece.IsElement));
            CollectionAssert.AreEqual(Questions, text.Where(piece => piece.Kind == TextExpanderInlineKind.ValueLabel).Select(piece => piece.Text.Trim('\u00A0', '\u2009')).ToArray());

            // The row's name comes from the sentence, however the line is drawn.
            Assert.AreEqual("!zasks: " + Sentence, row.AutomationName);
        }

        [TestMethod]
        public void ATrimmedLineShowsAsTextOnlyWhenItHasElements()
        {
            var fallback = new TextExpanderTrimFallback();
            Assert.IsFalse(fallback.ShowsText);

            Assert.IsFalse(fallback.OnTrimmed(isTrimmed: false, hasElements: true, width: 400), "It fits.");
            Assert.IsFalse(fallback.OnTrimmed(isTrimmed: true, hasElements: false, width: 400), "Plain text trims safely.");
            Assert.IsFalse(fallback.ShowsText);

            Assert.IsTrue(fallback.OnTrimmed(isTrimmed: true, hasElements: true, width: 400));
            Assert.IsTrue(fallback.ShowsText);

            // Drawn as text it may fit, or still be trimmed; either way it stays text.
            Assert.IsFalse(fallback.OnTrimmed(isTrimmed: false, hasElements: false, width: 400));
            Assert.IsFalse(fallback.OnTrimmed(isTrimmed: true, hasElements: false, width: 400));
            Assert.IsTrue(fallback.ShowsText);
        }

        [TestMethod]
        public void ATextLineTriesItsBubblesAgainOnlyWithClearlyMoreRoom()
        {
            var fallback = new TextExpanderTrimFallback();
            fallback.OnTrimmed(isTrimmed: true, hasElements: true, width: 400);

            Assert.IsFalse(fallback.OnWidthChanged(300));
            Assert.IsFalse(fallback.OnWidthChanged(400 + TextExpanderTrimFallback.RetryWidthStep - 1));
            Assert.IsTrue(fallback.ShowsText);

            Assert.IsTrue(fallback.OnWidthChanged(400 + TextExpanderTrimFallback.RetryWidthStep));
            Assert.IsFalse(fallback.ShowsText);

            // New content starts with its bubbles.
            fallback.OnTrimmed(isTrimmed: true, hasElements: true, width: 500);
            fallback.Reset();
            Assert.IsFalse(fallback.ShowsText);
        }

        [TestMethod]
        public void EveryTrimmedLineInARowKeepsElementsAwayFromTheEllipsis()
        {
            string page = FindPage();
            if (page == null)
            {
                Assert.Inconclusive("The page's XAML isn't next to the tests here.");
            }

            XElement list = XDocument.Load(page).Descendants()
                .Single(element => element.Name.LocalName == "ItemsRepeater" && ((string)element.Attribute("ItemsSource") ?? string.Empty).Contains("FilteredSnippets", StringComparison.Ordinal));
            var trimmed = list.Descendants().Where(element => element.Name.LocalName == "TextExpanderBubbleText" && element.Attribute("TextTrimming") != null).ToList();
            Assert.AreEqual(3, trimmed.Count, "The sentence, the title and the saved text under the sentence.");
            foreach (XElement line in trimmed)
            {
                Assert.IsTrue((string)line.Attribute("TextOnly") == "True" || (string)line.Attribute("TextOnlyWhenTrimmed") == "True", (string)line.Attribute("Segments"));
            }

            // The sentence and the title keep their bubbles and cursor bar while they fit.
            foreach (string segments in new[] { "SentenceParts", "TitleSegments" })
            {
                XElement line = trimmed.Single(element => ((string)element.Attribute("Segments")).Contains(segments, StringComparison.Ordinal));
                Assert.AreEqual("True", (string)line.Attribute("TextOnlyWhenTrimmed"), segments);
                Assert.IsNull(line.Attribute("TextOnly"), segments);
            }
        }

        private static TextExpanderRowDescription Describe(string trigger, string replacement)
            => TextExpanderRowDescriber.Describe(trigger, replacement, false, new TextExpanderTriggerSet(new[] { trigger }), false, Now, Resource);

        private static string Resource(string key) => Templates.TryGetValue(key, out string value) ? value : key;

        private static string FindPage()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                string page = Path.Combine(folder.FullName, "src", "settings-ui", "Settings.UI", "SettingsXAML", "Views", "TextExpanderPage.xaml");
                if (File.Exists(page))
                {
                    return page;
                }
            }

            return null;
        }
    }
}
