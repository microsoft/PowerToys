// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    /// <summary>Insert variable's ready-made date formats and the editor's date format guide.</summary>
    [TestClass]
    public class TextExpanderDateFormats
    {
        // A Sunday afternoon, so 12- and 24-hour times differ. The engine's tests type the same
        // formats for the same moment and expect the same text (VariableTests).
        private static readonly DateTime Now = new(2026, 10, 18, 16, 2, 9);

        private static readonly string[] Presets = { "dddd", "MMMM d, yyyy", "dddd, MMMM d, yyyy", "MMM d", "MM/dd/yyyy", "yyyy-MM-dd", "h:mm tt", "HH:mm" };
        private static readonly string[] PresetsTyped = { "Sunday", "October 18, 2026", "Sunday, October 18, 2026", "Oct 18", "10/18/2026", "2026-10-18", "4:02 PM", "16:02" };
        private static readonly string[] Codes = { "yyyy", "yy", "MMMM", "MMM", "MM", "M", "dddd", "ddd", "dd", "d", "h", "HH", "mm", "tt" };
        private static readonly string[] CodesTyped = { "2026", "26", "October", "Oct", "10", "10", "Sunday", "Sun", "18", "18", "4", "16", "02", "PM" };

        private static readonly Dictionary<string, string> Strings = new()
        {
            ["TextExpander_DatePreset_Name"] = "{0}, format {1}",
            ["TextExpander_DateCode_Name"] = "{0}, {1}, {2}",
            ["TextExpander_DateCode_Year"] = "year",
            ["TextExpander_DateCode_Hour12"] = "hour, 12-hour clock",
            ["TextExpander_DateCode_Hour12Example"] = "{0} (12-hour)",
            ["TextExpander_DateGuide_ShowsAs"] = "Shows as: {0}",
        };

        [TestMethod]
        public void EveryReadyMadeFormatInsertsItsTokenAndShowsWhatTheEngineTypes()
        {
            CollectionAssert.AreEqual(Presets, TextExpanderDateGuide.Presets.ToArray());
            CollectionAssert.AreEqual(PresetsTyped, TextExpanderDateGuide.Presets.Select(format => TextExpanderDateGuide.Example(format, Now)).ToArray());

            const string text = "Due  please";
            foreach (string format in TextExpanderDateGuide.Presets)
            {
                string token = TextExpanderDateGuide.Token(format);
                Assert.AreEqual("{{date:" + format + "}}", token);

                // At the caret, with the caret after it: a ready-made format is finished.
                (int start, int end, int selectionStart, int selectionEnd) = TextExpanderTokenText.PlanInsert(text, 4, 4, token, selectEditablePart: false);
                string result = text[..start] + token + text[end..];
                Assert.AreEqual("Due " + token + " please", result);
                Assert.AreEqual(4 + token.Length, selectionStart);
                Assert.AreEqual(selectionStart, selectionEnd);
                Assert.IsNull(TextExpanderDateGuide.TokenAround(result, selectionStart, selectionEnd), "The guide stays closed after a ready-made format.");
            }
        }

        [TestMethod]
        public void TheGuidesCodesShowWhatTheEngineTypes()
        {
            var codes = TextExpanderDateGuide.Rows.SelectMany(row => row).ToList();
            CollectionAssert.AreEqual(Codes, codes.Select(code => code.Code).ToArray());
            CollectionAssert.AreEqual(CodesTyped, codes.Select(code => TextExpanderDateGuide.Example(code.Code, Now)).ToArray());
            Assert.AreEqual(4, TextExpanderDateGuide.Rows.Count, "Years, months, days and times.");
        }

        [TestMethod]
        public void TheGuideShowsOnlyWhileTheCaretOrSelectionIsInADateFormat()
        {
            // {{date:dddd}} runs from 3 to 16 and its format from 10.
            const string text = "On {{date:dddd}} at {{time}}, {{date}}, {{prompt:Who}}";
            Assert.AreEqual((3, 16, 10, "dddd"), TextExpanderDateGuide.TokenAround(text, 12, 12));
            Assert.AreEqual((3, 16, 10, "dddd"), TextExpanderDateGuide.TokenAround(text, 10, 14), "The format selected, as Custom format… leaves it.");
            Assert.AreEqual((3, 16, 10, "dddd"), TextExpanderDateGuide.TokenAround(text, 3, 16), "The whole variable selected, as a click on it leaves it.");
            Assert.AreEqual((3, 16, 10, "dddd"), TextExpanderDateGuide.TokenAround(text, 5, 5), "In its braces.");

            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 3, 3), "Just before it.");
            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 16, 16), "Just after it.");
            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 0, 2));
            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 1, 12), "A selection that starts outside it.");
            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 23, 23), "{{time}} has no format.");
            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 33, 33), "Nor does {{date}}.");
            Assert.IsNull(TextExpanderDateGuide.TokenAround(text, 47, 47), "A question isn't a date.");
            Assert.AreEqual((0, 9, 7, string.Empty), TextExpanderDateGuide.TokenAround("{{date:}}", 7, 7), "An empty format still opens the guide.");
        }

        [TestMethod]
        public void AChipPutsItsCodeInAtTheCaretButNeverOverTheBraces()
        {
            // {{date:dddd, MMMM}}: the format runs from 7 to 17.
            const string text = "{{date:dddd, MMMM}}";
            Assert.AreEqual((13, 13, 17), TextExpanderDateGuide.CodeInsertion(text, 13, 13, "yyyy"), "At the caret.");
            Assert.AreEqual((7, 11, 10), TextExpanderDateGuide.CodeInsertion(text, 7, 11, "ddd"), "In place of a selection in the format.");
            Assert.AreEqual((17, 17, 21), TextExpanderDateGuide.CodeInsertion(text, 0, text.Length, "yyyy"), "The whole variable selected: after the format.");
            Assert.AreEqual((17, 17, 18), TextExpanderDateGuide.CodeInsertion(text, 3, 3, "d"), "A caret in the braces: after the format.");
            Assert.IsNull(TextExpanderDateGuide.CodeInsertion("Hi {{date:dddd}}", 1, 1, "d"), "Outside a date, nothing.");

            (int start, int end, int caret) = TextExpanderDateGuide.CodeInsertion(text, 13, 13, "yyyy").Value;
            Assert.AreEqual("{{date:dddd, yyyyMMMM}}", text[..start] + "yyyy" + text[end..]);
            Assert.AreEqual(17, caret);
        }

        [TestMethod]
        public void CustomFormatSelectsTheFormatSoTheGuideOpens()
        {
            const string token = "{{date:dddd}}";
            (int start, int end, int selectionStart, int selectionEnd) = TextExpanderTokenText.PlanInsert("Hi ", 3, 3, token, selectEditablePart: true);
            string text = "Hi "[..start] + token + "Hi "[end..];

            Assert.AreEqual("dddd", text[selectionStart..selectionEnd]);
            Assert.AreEqual("dddd", TextExpanderDateGuide.TokenAround(text, selectionStart, selectionEnd)?.Format);
        }

        [TestMethod]
        public void ChipsAndFormatsAreNamedWithTheirCodeAndWhatTheyGive()
        {
            (string Code, string MeaningKey, string ExampleKey) year = TextExpanderDateGuide.Rows[0][0];
            (string Code, string MeaningKey, string ExampleKey) hour = TextExpanderDateGuide.Rows[3][0];

            Assert.AreEqual("yyyy, year, 2026", TextExpanderDateGuide.CodeName(year, Now, Resource));
            Assert.AreEqual("2026", TextExpanderDateGuide.CodeExample(year, Now, Resource));
            Assert.AreEqual("4 (12-hour)", TextExpanderDateGuide.CodeExample(hour, Now, Resource), "An hour says which clock.");
            Assert.AreEqual("h, hour, 12-hour clock, 4", TextExpanderDateGuide.CodeName(hour, Now, Resource));
            Assert.AreEqual("Oct 18, format MMM d", TextExpanderDateGuide.PresetName("MMM d", Now, Resource));
            Assert.AreEqual("Shows as: Sunday, October 18", TextExpanderDateGuide.ShowsAs("dddd, MMMM d", Now, Resource));
            Assert.AreEqual(string.Empty, TextExpanderDateGuide.ShowsAs(string.Empty, Now, Resource), "Nothing to show for an empty format.");
        }

        private static string Resource(string key) => Strings.TryGetValue(key, out string value) ? value : key;
    }
}
