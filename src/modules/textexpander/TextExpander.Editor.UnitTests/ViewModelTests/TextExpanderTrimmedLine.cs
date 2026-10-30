// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// WinUI draws an element that falls past a trimmed line's ellipsis at the start of the line, over the text.
    /// So a trimmed line is drawn all as text, with each bubble's icon and label in its place.
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

        private static readonly TextExpanderInlineKind[] BubbleKinds =
        {
            TextExpanderInlineKind.Text,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.Text,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.LineBreak,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.Text,
            TextExpanderInlineKind.Bubble,
            TextExpanderInlineKind.CursorBar,
        };

        [TestMethod]
        public void TextOnlyLinesHaveNoElementsAndKeepTheBubblesLabels()
        {
            var plan = TextExpanderInlines.Plan(EveryKind, textOnly: true, Resource);

            Assert.IsFalse(plan.Any(piece => piece.IsElement), string.Join(", ", plan.Where(piece => piece.IsElement).Select(piece => piece.Kind)));

            // Each value is its bubble's icon and label, in order, without the box; thin spaces stand in for its margins.
            CollectionAssert.AreEqual(Glyphs, plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueIcon).Select(piece => piece.Text.TrimStart('\u2009')).ToArray());
            CollectionAssert.AreEqual(Labels, plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueLabel).Select(piece => piece.Text.Trim('\u00A0', '\u2009')).ToArray());
            Assert.IsTrue(plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueIcon).All(piece => piece.Text.StartsWith('\u2009')));
            Assert.IsTrue(plan.Where(piece => piece.Kind == TextExpanderInlineKind.ValueLabel).All(piece => piece.Text.EndsWith('\u2009')));
            var bubbles = TextExpanderInlines.Plan(EveryKind, textOnly: false, Resource);
            CollectionAssert.AreEqual(Labels, bubbles.Where(piece => piece.Kind == TextExpanderInlineKind.Bubble).Select(piece => piece.Text).ToArray());

            // The snippet's own text, line break and tab are unchanged, and the cursor is a character.
            string text = string.Concat(plan.Where(piece => piece.Kind == TextExpanderInlineKind.Text).Select(piece => piece.Text));
            Assert.AreEqual("Hi , see \t", text);
            Assert.AreEqual(1, plan.Count(piece => piece.Kind == TextExpanderInlineKind.LineBreak));
            Assert.AreEqual("|", plan.Single(piece => piece.Kind == TextExpanderInlineKind.CursorText).Text);
        }

        [TestMethod]
        public void BubbleLinesAreUnchanged()
        {
            // Real line breaks and tabs, as the chip and the preview show them.
            var plan = TextExpanderInlines.Plan(EveryKind, textOnly: false, Resource);
            CollectionAssert.AreEqual(BubbleKinds, plan.Select(piece => piece.Kind).ToArray());
            Assert.AreEqual("\t", plan[7].Text);
        }

        private static string Resource(string key) => key switch
        {
            "TextExpander_PreviewClipboard" => "Clipboard",
            "TextExpander_PreviewPromptUnnamed" => "Your answer",
            _ => key,
        };
    }
}
