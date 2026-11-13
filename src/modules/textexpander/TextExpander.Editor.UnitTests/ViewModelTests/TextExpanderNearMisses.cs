// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.PowerToys.TextExpander;
using Microsoft.PowerToys.TextExpander.Editor.Helpers;
using Microsoft.PowerToys.TextExpander.Editor.UnitTests;
using Microsoft.PowerToys.TextExpander.Editor.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>
    /// Text that looks meant as a variable but is typed as written, such as {{Date}}, and the variable it likely means,
    /// which the editor warns about.
    /// </summary>
    [TestClass]
    public class TextExpanderNearMisses
    {
        private static readonly DateTime Now = new(2026, 10, 17, 14, 5, 9);

        private static readonly string[] FabTrigger = { ";fab" };

        private static readonly Lazy<Dictionary<string, string>> English = new(() => RepositoryFiles.Xml(RepositoryFiles.EditorResources)
            .Descendants("data")
            .ToDictionary(data => (string)data.Attribute("name"), data => data.Element("value").Value));

        [TestMethod]
        [DataRow("Sent {{Date}}", "{{Date}}", "{{date}}")]
        [DataRow("Sent {{ date }}", "{{ date }}", "{{date}}")]
        [DataRow("Sent {date}", "{date}", "{{date}}")]
        [DataRow("Sent {{date}", "{{date}", "{{date}}")]
        [DataRow("Sent {date}}", "{date}}", "{{date}}")]
        [DataRow("{{ISODATE}} {{Time}}", "{{ISODATE}}", "{{isodate}}")]
        [DataRow("{{iso date}}", "{{iso date}}", "{{isodate}}")]
        [DataRow("{{data}}", "{{data}}", "{{date}}")]
        [DataRow("Due {{dates}}", "{{dates}}", "{{date}}")]
        [DataRow("{{DATE:yyyy-MM-dd}}", "{{DATE:yyyy-MM-dd}}", "{{date:yyyy-MM-dd}}")]
        [DataRow("{{time:HH:mm}}", "{{time:HH:mm}}", "{{date:HH:mm}}")]
        [DataRow("{{datetime:yyyy}}", "{{datetime:yyyy}}", "{{date:yyyy}}")]
        [DataRow("Hi {{Prompt:First name}}", "{{Prompt:First name}}", "{{prompt:First name}}")]
        [DataRow("Hi {{promt:First name}}", "{{promt:First name}}", "{{prompt:First name}}")]
        [DataRow("Hi {{prompt? : Note}}", "{{prompt? : Note}}", "{{prompt?:Note}}")]
        [DataRow("{{Clipboard}}", "{{Clipboard}}", "{{clipboard}}")]
        [DataRow("{{clipbaord}}", "{{clipbaord}}", "{{clipboard}}")]
        [DataRow("{{clipboard:Link}}", "{{clipboard:Link}}", "{{clipboard:link}}")]
        [DataRow("{{clipboard: lnik : 2}}", "{{clipboard: lnik : 2}}", "{{clipboard:link}}")]
        [DataRow("{{clipboard:all}}", "{{clipboard:all}}", "{{clipboard}}")]
        [DataRow("{{clipboard:link:2}}", "{{clipboard:link:2}}", "{{clipboard:link}}")]
        [DataRow("{{clipboard:link:0}}", "{{clipboard:link:0}}", "{{clipboard:link}}")]
        [DataRow("{{clipboard:email:pick}}", "{{clipboard:email:pick}}", "{{clipboard:email}}")]
        [DataRow("{{clipboard:number:all}}", "{{clipboard:number:all}}", "{{clipboard:number}}")]
        [DataRow("{{clipboard:pick:link:last}}", "{{clipboard:pick:link:last}}", "{{clipboard:pick:link}}")]
        [DataRow("{{clipboard:pick:pick}}", "{{clipboard:pick:pick}}", "{{clipboard:pick}}")]
        [DataRow("Dear {{cursor}},", "{{cursor}}", "$|$")]
        [DataRow("On {{today}}", "{{today}}", "{{date}}")]
        [DataRow("At {{now}}", "{{now}}", "{{datetime}}")]
        [DataRow("{{paste}}", "{{paste}}", "{{clipboard}}")]
        public void ALikelyVariableIsFoundWithWhatItMeans(string replacement, string written, string suggestion)
        {
            TextExpanderNearMiss miss = TextExpanderNearMiss.Find(replacement)[0];
            Assert.AreEqual(written, miss.Written);
            Assert.AreEqual(written, replacement[miss.Start..miss.End]);
            Assert.AreEqual(suggestion, miss.Suggestion);
        }

        [TestMethod]
        [DataRow("{{clipboard:xyz}}")]
        [DataRow("{{clipboard:link:xyzzy}}")]
        [DataRow("{{prompt}}")]
        [DataRow("{{Prompt?: }}")]
        public void AVariableNameWithAMistakeAfterItIsFoundWithoutAGuess(string replacement)
        {
            IReadOnlyList<TextExpanderNearMiss> misses = TextExpanderNearMiss.Find(replacement);
            Assert.AreEqual(1, misses.Count);
            Assert.AreEqual(replacement, misses[0].Written);
            Assert.IsNull(misses[0].Suggestion);
        }

        [TestMethod]
        [DataRow("{{date}} {{time}} {{clipboard:link}} {{clipboard:pick:email}} {{prompt:Name}} $|$")]
        [DataRow("{{date:'{{Date}}' yyyy}}")]
        [DataRow("Hi {{name}}, your {{order_id}} {name} {x}")]
        [DataRow("{\"key\": 1} and function() { return x; }")]
        [DataRow("{{{date}}}")]
        [DataRow("{{date_of_birth}} {{dates and times}}")]
        [DataRow("{{ab}} {ab}")]
        [DataRow("print(f\"{now} {today} {cursor}\")")]
        [DataRow("")]
        public void VariablesAndOtherBracesAreNotNearMisses(string replacement)
            => Assert.AreEqual(0, TextExpanderNearMiss.Find(replacement).Count, string.Join(" | ", TextExpanderNearMiss.Find(replacement).Select(miss => miss.Written)));

        [TestMethod]
        public void EveryNearMissIsFoundInOrderAndEachSuggestionIsAVariable()
        {
            IReadOnlyList<TextExpanderNearMiss> misses = TextExpanderNearMiss.Find("{{Date}} and {{date}} and {{TIME}}\n{date}");
            Assert.AreEqual("{{Date}}|{{TIME}}|{date}", string.Join("|", misses.Select(miss => miss.Written)));
            Assert.AreEqual("{{date}}|{{time}}|{{date}}", string.Join("|", misses.Select(miss => miss.Suggestion)));
        }

        [TestMethod]
        public void TextFullOfBracesIsReadInLinearTime()
        {
            var clock = Stopwatch.StartNew();
            _ = TextExpanderNearMiss.Find(new string('{', 200_000));
            _ = TextExpanderNearMiss.Find(string.Concat(Enumerable.Repeat("{{Date", 40_000)));
            _ = TextExpanderNearMiss.Find(string.Concat(Enumerable.Repeat("{a}", 70_000)));
            Assert.IsTrue(clock.Elapsed < TimeSpan.FromSeconds(10), $"Took {clock.Elapsed}.");
        }

        [TestMethod]
        public void TheCursorIsSuggestedOnlyWhereTheSnippetHasNone()
        {
            // Only the first $|$ places the cursor: another would be typed as written, or move it.
            Assert.AreEqual(0, TextExpanderNearMiss.Find("{{cursor}} Dear $|$").Count);
            Assert.AreEqual(0, TextExpanderNearMiss.Find("Dear $|$ {{cursor}}").Count);
            IReadOnlyList<TextExpanderNearMiss> both = TextExpanderNearMiss.Find("{{cursor}} and {{caret}}");
            Assert.AreEqual(1, both.Count, string.Join(" | ", both.Select(miss => miss.Written)));
            Assert.AreEqual("$|$", both[0].Suggestion);
        }

        [TestMethod]
        public void ALongOneIsFoundWithBothItsBracesOrNotAtAll()
        {
            // Around the length the search stops at, so its second brace is never read as the start of another.
            for (int length = 95; length <= 135; length++)
            {
                string text = "Hi {{Prompt:" + new string('a', length) + "}}, thanks";
                foreach (TextExpanderNearMiss miss in TextExpanderNearMiss.Find(text))
                {
                    Assert.AreEqual(3, miss.Start, $"A label of {length}: {miss.Written}");
                    Assert.AreEqual(text.IndexOf("}}", StringComparison.Ordinal) + 2, miss.End, $"A label of {length}.");
                }
            }
        }

        [TestMethod]
        public void TheRowSaysWhatIsTypedAsWrittenAndWhatItLikelyMeans()
        {
            TextExpanderRowDescription row = Describe(";sent", "Sent on {{Date}}.");
            Assert.AreEqual("{{Date}} isn't a variable. Did you mean {{date}}?", row.VariableWarning);
            Assert.IsNull(row.Warning, "It still expands.");
            Assert.AreEqual(";sent: Sent on {{Date}}. Typed as written: {{Date}} isn't a variable. Did you mean {{date}}?", row.AutomationName);

            Assert.AreEqual("{{clipboard:xyz}} isn't a variable Text Expander knows.", Describe(";c", "{{clipboard:xyz}}").VariableWarning);
            Assert.IsNull(Describe(";d", "Sent on {{date}} at {{time}}.").VariableWarning);

            // The first one: fixing it shows the next.
            Assert.AreEqual("{{Time}} isn't a variable. Did you mean {{time}}?", Describe(";t", "{{Time}} and {{Date}}").VariableWarning);

            // A snippet that never expands too: a screen reader hears both.
            TextExpanderRowDescription both = TextExpanderRowDescriber.Describe(";fab", "{{Date}}", hasLaterDuplicate: true, new TextExpanderTriggerSet(FabTrigger), false, Now, Resource);
            Assert.AreEqual(
                ";fab: {{Date}}. Never expands: Another \";fab\" further down the list is used instead. Typed as written: {{Date}} isn't a variable. Did you mean {{date}}?",
                both.AutomationName);

            var snippet = new TextExpanderSnippetViewModel(1, ";sent", "Sent on {{Date}}.") { Description = row };
            Assert.IsTrue(snippet.HasVariableWarning);
            Assert.AreEqual(row.VariableWarning, snippet.VariableWarning);
            Assert.IsFalse(new TextExpanderSnippetViewModel(2, ";d", "{{date}}") { Description = Describe(";d", "{{date}}") }.HasVariableWarning);
        }

        [TestMethod]
        public void TheEditorShowsTheFirstOneUnderTheReplacementBoxWithAButtonToUseWhatItMeans()
        {
            XDocument page = RepositoryFiles.Xml(RepositoryFiles.EditorPage);

            // Right under the box and its privacy note, so Tab from the box reaches Use next.
            XElement box = RepositoryFiles.WithName(page, "ReplacementBox");
            XElement note = RepositoryFiles.WithName(page, "NearMissNote");
            Assert.AreSame(note, box.ElementsAfterSelf().ElementAt(1), "The privacy note, then this one.");
            Assert.AreEqual("Collapsed", (string)note.Attribute("Visibility"));
            XElement use = RepositoryFiles.WithName(note, "NearMissUseButton");
            Assert.AreEqual("NearMissUse_Click", (string)use.Attribute("Click"));
            Assert.AreEqual("Button_TextExpander_UseVariable", (string)use.Attribute(XName.Get("AutomationProperties.AutomationId")));

            // Rows: a badge and what it is, after the never-expands note.
            XElement badge = RepositoryFiles.WithUid(RepositoryFiles.SnippetList(), "TextExpander_Row_TypedAsWritten");
            XElement rowNote = badge.Ancestors().First(element => element.Name.LocalName == "Grid");
            Assert.AreEqual("{x:Bind HasVariableWarning, Mode=OneWay}", (string)rowNote.Attribute("Visibility"));
            Assert.IsTrue(rowNote.Descendants().Any(element => (string)element.Attribute("Text") == "{x:Bind VariableWarning, Mode=OneWay}"));

            string code = RepositoryFiles.Text(RepositoryFiles.EditorPage + ".cs");
            StringAssert.Matches(code, new Regex(@"ShowPreview\(replacement\);\s+ShowNearMiss\(replacement\);"), "It follows the typing.");
            StringAssert.Contains(code, "_replacementEditor.ReplaceVariable(miss.Start, miss.End, miss.Written, suggestion)", "Use is one undo step, and only on the text it was for.");

            foreach ((string key, int slots) in new[] { ("TextExpander_NearMiss", 2), ("TextExpander_NearMissUnknown", 1), ("TextExpander_NearMiss_Use", 1), ("TextExpander_Row_NearMiss", 2), ("TextExpander_Row_NearMissUnknown", 1), ("TextExpander_Row_TypedAsWrittenSpoken", 1) })
            {
                for (int slot = 0; slot < slots; slot++)
                {
                    StringAssert.Contains(English.Value[key], "{" + slot + "}", key);
                }
            }
        }

        private static TextExpanderRowDescription Describe(string trigger, string replacement)
            => TextExpanderRowDescriber.Describe(trigger, replacement, false, new TextExpanderTriggerSet(new[] { trigger }), false, Now, Resource);

        private static string Resource(string key) => English.Value.TryGetValue(key, out string value) ? value : key;
    }
}
