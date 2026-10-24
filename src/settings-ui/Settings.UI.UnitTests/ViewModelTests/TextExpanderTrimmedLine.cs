// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// A row's saved text is trimmed to one line, and WinUI draws an element that falls past the
    /// ellipsis at the start of the line, over the text. So that line is all text.
    /// </summary>
    [TestClass]
    public class TextExpanderTrimmedLine
    {
        private static readonly TextExpanderPreviewSegment[] EveryKind =
        {
            new(TextExpanderPreviewSegmentKind.Text, "Hi "),
            new(TextExpanderPreviewSegmentKind.Prompt, "First name"),
            new(TextExpanderPreviewSegmentKind.Prompt, " "),
            new(TextExpanderPreviewSegmentKind.Text, ", see "),
            new(TextExpanderPreviewSegmentKind.Clipboard),
            new(TextExpanderPreviewSegmentKind.LineBreak),
            new(TextExpanderPreviewSegmentKind.Date, "10/18/2026"),
            new(TextExpanderPreviewSegmentKind.Tab),
            new(TextExpanderPreviewSegmentKind.Time, "04:02 PM"),
            new(TextExpanderPreviewSegmentKind.Cursor),
        };

        private static readonly string[] Labels = { "First name", "Your answer", "Clipboard", "10/18/2026", "04:02 PM" };

        private static readonly string[] Glyphs = { "\uE70F", "\uE70F", "\uE77F", "\uE787", "\uE823" };

        private static readonly string[] Markers = { "\u21B5", "\u2192" };

        private static readonly TextExpanderInlineKind[] BubbleKinds =
        {
            TextExpanderInlineKind.Text,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.Text,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.BreakMarker,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.BreakMarker,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.CursorBar,
        };

        [TestMethod]
        public void TextOnlyLinesHaveNoElementsAndKeepTheBubblesLabels()
        {
            var plan = TextExpanderInlines.Plan(EveryKind, breaksAsMarkers: true, textOnly: true, Resource);

            Assert.IsFalse(plan.Any(piece => piece.IsElement), string.Join(", ", plan.Where(piece => piece.IsElement).Select(piece => piece.Kind)));

            // Each value is its bubble's icon and label, in order, without the box; thin spaces stand in for its margins.
            CollectionAssert.AreEqual(Glyphs, plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueIcon).Select(piece => piece.Text.TrimStart('\u2009')).ToArray());
            CollectionAssert.AreEqual(Labels, plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueLabel).Select(piece => piece.Text.Trim('\u00A0', '\u2009')).ToArray());
            Assert.IsTrue(plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueIcon).All(piece => piece.Text.StartsWith('\u2009')));
            Assert.IsTrue(plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueLabel).All(piece => piece.Text.EndsWith('\u2009')));
            var bubbles = TextExpanderInlines.Plan(EveryKind, breaksAsMarkers: true, textOnly: false, Resource);
            CollectionAssert.AreEqual(Labels, bubbles.Where(piece => piece.Kind == TextExpanderInlineKind.Bubble).Select(piece => piece.Text).ToArray());

            // The snippet's own text is unchanged, the markers are characters, and so is the cursor.
            string text = string.Concat(plan.Where(piece => piece.Kind == TextExpanderInlineKind.Text).Select(piece => piece.Text));
            Assert.AreEqual("Hi , see \u2009\u21B5\u2009\u2009\u2192\u2009", text);
            Assert.AreEqual("|", plan.Single(piece => piece.Kind == TextExpanderInlineKind.CursorText).Text);
        }

        [TestMethod]
        public void BubbleLinesAreUnchanged()
        {
            var plan = TextExpanderInlines.Plan(EveryKind, breaksAsMarkers: true, textOnly: false, Resource);
            CollectionAssert.AreEqual(BubbleKinds, plan.Select(piece => piece.Kind).ToArray());
            CollectionAssert.AreEqual(Markers, plan.Where(piece => piece.Kind == TextExpanderInlineKind.BreakMarker).Select(piece => piece.Text).ToArray());

            // Without markers, as in the preview and the title lines: real line breaks and tabs.
            var lines = TextExpanderInlines.Plan(EveryKind, breaksAsMarkers: false, textOnly: false, Resource);
            Assert.AreEqual(1, lines.Count(piece => piece.Kind == TextExpanderInlineKind.LineBreak));
            Assert.IsTrue(lines.Any(piece => piece.Kind == TextExpanderInlineKind.Text && piece.Text == "\t"));
            Assert.AreEqual(5, lines.Count(piece => piece.Kind == TextExpanderInlineKind.Bubble));
        }

        [TestMethod]
        public void TheRowsOneLineSavedTextIsTextOnly()
        {
            string page = FindPage();
            if (page == null)
            {
                Assert.Inconclusive("The page's XAML isn't next to the tests here.");
            }

            XElement list = XDocument.Load(page).Descendants()
                .Single(element => element.Name.LocalName == "ItemsRepeater" && ((string)element.Attribute("ItemsSource") ?? string.Empty).Contains("FilteredSnippets", StringComparison.Ordinal));
            var lines = list.Descendants().Where(element => element.Name.LocalName == "TextExpanderBubbleText").ToList();

            // Every line trimmed to one line is all text, so no bubble can land past its ellipsis.
            var oneLine = lines.Where(line => (string)line.Attribute("MaxLines") == "1").ToList();
            Assert.IsTrue(oneLine.Count > 0, "The saved text under a sentence is one trimmed line.");
            foreach (XElement line in oneLine)
            {
                Assert.AreEqual("True", (string)line.Attribute("TextOnly"), (string)line.Attribute("Segments"));
            }

            // The sentence wraps instead, and keeps its bubbles.
            XElement sentence = lines.Single(line => ((string)line.Attribute("Segments") ?? string.Empty).Contains("SentenceParts", StringComparison.Ordinal));
            Assert.IsNull(sentence.Attribute("TextOnly"));
        }

        private static string Resource(string key) => key switch
        {
            "TextExpander_PreviewClipboard" => "Clipboard",
            "TextExpander_PreviewPromptUnnamed" => "Your answer",
            _ => key,
        };

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
