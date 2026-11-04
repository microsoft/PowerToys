// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Kind = Microsoft.PowerToys.TextExpander.Editor.Helpers.TextExpanderPreviewSegmentKind;

namespace ViewModelTests
{
    /// <summary>
    /// Clipboard variables in the snippet editor: Insert variable's Clipboard menu, the Clipboard panel that clicking or F2
    /// opens on one, and what bubbles, rows and screen readers call each.
    /// </summary>
    [TestClass]
    public class TextExpanderClipboard
    {
        private static readonly DateTime Now = new(2026, 10, 17, 14, 5, 9);

        private static readonly string[] ReplyTrigger = { ";reply" };

        private static readonly Lazy<Dictionary<string, string>> English = new(() => RepositoryFiles.Xml(RepositoryFiles.EditorResources)
            .Descendants("data")
            .ToDictionary(data => (string)data.Attribute("name"), data => data.Element("value").Value));

        [TestMethod]
        [DataRow("{{clipboard}}", "Clipboard")]
        [DataRow("{{clipboard:email}}", "Email from clipboard")]
        [DataRow("{{clipboard:link:2}}", "Link 2 from clipboard")]
        [DataRow("{{clipboard:phone:last}}", "Last phone number from clipboard")]
        [DataRow("{{clipboard:number:all}}", "All numbers from clipboard")]
        [DataRow("{{clipboard:link:pick}}", "Link you pick from clipboard")]
        [DataRow("{{clipboard:pick}}", "Copy you pick")]
        [DataRow("{{clipboard:pick:email:3}}", "Email 3 from a copy you pick")]
        public void EachVariableHasABubbleThatSaysWhatItInserts(string token, string label)
        {
            IReadOnlyList<TextExpanderPreviewSegment> segments = TextExpanderSnippetPreview.Build("Send " + token + " today", Now);
            TextExpanderPreviewSegment bubble = segments.Single(segment => segment.IsBubble);
            Assert.AreEqual(Kind.Clipboard, bubble.Kind);
            Assert.IsTrue(ClipboardVariable.TryParse(token, out ClipboardVariable variable));
            Assert.AreEqual(variable, bubble.Clipboard);

            (string glyph, string shown) = TextExpanderInlines.ValueText(bubble, Resource);
            Assert.AreEqual("\uE77F", glyph);
            Assert.AreEqual(label, shown);
            Assert.AreEqual(
                token == "{{clipboard}}" ? "Clipboard, filled in when the snippet runs" : label + ", filled in when the snippet runs",
                TextExpanderClipboardText.Description(variable, Resource));
        }

        [TestMethod]
        public void EveryPartAndChoiceHasItsWords()
        {
            foreach (ClipboardPart part in TextExpanderClipboardText.Parts)
            {
                foreach (ClipboardMatch match in Enum.GetValues<ClipboardMatch>())
                {
                    foreach (bool pickCopy in new[] { false, true })
                    {
                        string label = TextExpanderClipboardText.Label(new ClipboardVariable(pickCopy, part, match, 4), Resource);
                        Assert.IsFalse(label.Contains("TextExpander_", StringComparison.Ordinal), $"{part} {match} {pickCopy}: {label}");
                        Assert.IsFalse(label.Contains('{', StringComparison.Ordinal), label);
                    }
                }

                Assert.IsTrue(English.Value.ContainsKey("TextExpander_ClipboardPart_" + part), part.ToString());
                Assert.IsTrue(English.Value.ContainsKey("TextExpander_ClipboardFlyout_Part_" + part), part.ToString());
            }

            foreach (string key in new[] { "First", "Second", "Third", "Numbered", "Last", "All", "Pick" })
            {
                Assert.IsTrue(English.Value.ContainsKey("TextExpander_ClipboardFlyout_Match" + key), key);
            }
        }

        [TestMethod]
        public void RowsReadClipboardPartsAloudByName()
        {
            TextExpanderRowDescription row = TextExpanderRowDescriber.Describe(
                ";reply",
                "Thanks! I'll send it to {{clipboard:email}} today. Copied: {{clipboard}}",
                false,
                new TextExpanderTriggerSet(ReplyTrigger),
                wordMode: false,
                Now,
                Resource);

            Assert.AreEqual("\uE77F", row.KindGlyph);
            Assert.AreEqual(";reply: Thanks! I'll send it to (Email from clipboard) today. Copied: (what you copied)", row.AutomationName);
            Assert.AreEqual("Text Clipboard Text Clipboard", string.Join(" ", row.Chip.Select(segment => segment.Kind)));
        }

        [TestMethod]
        public void ClickingOrF2FindsTheClipboardVariableAtTheCaret()
        {
            const string text = "To {{clipboard:link:2}} and {{prompt:Name}} {{clipboard}}";
            int link = text.IndexOf("{{clipboard:link:2}}", StringComparison.Ordinal);
            int whole = text.LastIndexOf("{{clipboard}}", StringComparison.Ordinal);

            Assert.IsTrue(TextExpanderClipboardText.TryFindAtSelection(text, link + 3, link + 3, out int start, out int end, out ClipboardVariable variable), "The caret inside it.");
            Assert.AreEqual((link, link + "{{clipboard:link:2}}".Length), (start, end));
            Assert.AreEqual(new ClipboardVariable(Part: ClipboardPart.Link, Match: ClipboardMatch.Numbered, Index: 2), variable);

            Assert.IsTrue(TextExpanderClipboardText.TryFindAtSelection(text, whole + "{{clipboard}}".Length, whole, out start, out _, out variable), "It, selected backwards.");
            Assert.AreEqual(whole, start);
            Assert.AreEqual(default, variable);

            Assert.IsFalse(TextExpanderClipboardText.TryFindAtSelection(text, link, link, out _, out _, out _), "A caret just before it isn't in it.");
            int prompt = text.IndexOf("{{prompt", StringComparison.Ordinal);
            Assert.IsFalse(TextExpanderClipboardText.TryFindAtSelection(text, prompt + 3, prompt + 3, out _, out _, out _), "A question opens its own panel.");
            Assert.IsFalse(TextExpanderClipboardText.TryFindAtSelection("{{clipboard:Link}}", 3, 3, out _, out _, out _), "Text that only looks like one isn't one.");
        }

        [TestMethod]
        public void TheClipboardMenuAndPanelAreBuiltAsTheMockupShows()
        {
            XDocument page = RepositoryFiles.Xml(RepositoryFiles.EditorPage);
            XElement flyout = RepositoryFiles.WithName(page, "ClipboardFlyout");
            foreach (string id in new[] { "TextExpander_ClipboardCopy", "TextExpander_ClipboardPart", "TextExpander_ClipboardMatch", "TextExpander_ClipboardIndex", "Button_TextExpander_ClipboardUpdate", "Button_TextExpander_ClipboardCancel" })
            {
                Assert.IsTrue(flyout.Descendants().Any(element => (string)element.Attribute(XName.Get("AutomationProperties.AutomationId")) == id), id);
            }

            Assert.AreEqual("99", (string)RepositoryFiles.WithName(flyout, "ClipboardIndexBox").Attribute("Maximum"), "{{clipboard:link:99}} is the highest.");

            string code = RepositoryFiles.Text(RepositoryFiles.EditorPage + ".cs");
            StringAssert.Contains(code, "_replacementEditor.ClipboardEditRequested += ReplacementEditor_ClipboardEditRequested;");
            StringAssert.Contains(code, "\"TextExpander_Variable_ClipboardNow\"");
            StringAssert.Contains(code, "Style = (Style)Resources[\"MenuHeadingItemStyle\"]");
            StringAssert.Contains(code, "_replacementEditor.ReplaceVariable(", "Update changes the variable as one undo step.");

            string editor = RepositoryFiles.Text(@"src\modules\textexpander\TextExpander.Editor\Helpers\TextExpanderReplacementEditor.cs");
            StringAssert.Contains(editor, "ClipboardEditRequested?.Invoke(", "Clicking a clipboard variable or pressing F2 on it opens the panel.");
        }

        private static string Resource(string key) => English.Value.TryGetValue(key, out string value) ? value : key;
    }
}
