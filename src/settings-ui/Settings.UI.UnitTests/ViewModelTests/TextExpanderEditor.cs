// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.UI;

using Kind = Microsoft.PowerToys.Settings.UI.Helpers.TextExpanderPreviewSegmentKind;
using Segment = Microsoft.PowerToys.Settings.UI.Helpers.TextExpanderPreviewSegment;

namespace ViewModelTests
{
    /// <summary>The snippet editor's preview bubbles and the rules behind its replacement box.</summary>
    [TestClass]
    public class TextExpanderEditor
    {
        // A Saturday afternoon, so 12- and 24-hour formats differ.
        private static readonly DateTime Now = new(2026, 10, 17, 14, 5, 9);

        [TestMethod]
        public void PreviewShowsBubblesForValuesKnownOnlyWhenTheSnippetRuns()
        {
            AssertPreview(
                "Hi {{prompt:First name}},\\n{{clipboard}}\\tsee {{date}} at {{time}}$|$ then $|$",
                new Segment(Kind.Text, "Hi "),
                new Segment(Kind.Prompt, "First name"),
                new Segment(Kind.Text, ","),
                new Segment(Kind.LineBreak),
                new Segment(Kind.Clipboard),
                new Segment(Kind.Tab),
                new Segment(Kind.Text, "see "),
                new Segment(Kind.Date, "10/17/26"),
                new Segment(Kind.Text, " at "),
                new Segment(Kind.Time, "02:05 PM"),
                new Segment(Kind.Cursor),
                new Segment(Kind.Text, " then $|$"));
        }

        [TestMethod]
        public void PreviewEvaluatesEveryDateFormLikeTheEngine()
        {
            AssertPreview(
                "{{isodate}}|{{datetime}}|{{date:dddd}}|{{date:%Y-%m-%d}}|{{date:d}}|{{date:}}",
                new Segment(Kind.Date, "2026-10-17"),
                new Segment(Kind.Text, "|"),
                new Segment(Kind.Date, "10/17/26 02:05 PM"),
                new Segment(Kind.Text, "|"),
                new Segment(Kind.Date, "Saturday"),
                new Segment(Kind.Text, "|"),
                new Segment(Kind.Date, "2026-10-17"),
                new Segment(Kind.Text, "|"),
                new Segment(Kind.Date, "17"),
                new Segment(Kind.Text, "|"));
        }

        [TestMethod]
        public void PreviewLeavesUnknownBracesAndRealLineBreaksAsText()
        {
            AssertPreview(
                "{{dat}} {{prompt:open\r\nnext",
                new Segment(Kind.Text, "{{dat}} {{prompt:open"),
                new Segment(Kind.LineBreak),
                new Segment(Kind.Text, "next"));

            Assert.AreEqual(0, TextExpanderSnippetPreview.Build(string.Empty, Now).Count);
            Assert.IsFalse(TextExpanderSnippetPreview.HasBubbles(TextExpanderSnippetPreview.Build("plain $|$ text", Now)));
            Assert.IsTrue(TextExpanderSnippetPreview.HasBubbles(TextExpanderSnippetPreview.Build("{{clipboard}}", Now)));
        }

        [TestMethod]
        public void PreviewReadsEachValueOnceLikeTheEngine()
        {
            // The engine reads a snippet once, from the left, so each question is asked
            // separately and a placeholder inside a label is just part of the label.
            AssertPreview(
                "{{prompt:{{date}}}} {{prompt:Name}}{{prompt:Name}}",
                new Segment(Kind.Prompt, "{{date"),
                new Segment(Kind.Text, "}} "),
                new Segment(Kind.Prompt, "Name"),
                new Segment(Kind.Prompt, "Name"));

            // FUZZ-2 and QA-10: a value is never read again, so a date that prints "\n" or "$|$"
            // shows it as it is, and only the snippet's own marker places the cursor.
            AssertPreview(
                @"[{{date:\\n}}]\n{{date:'$|$'}} $|$",
                new Segment(Kind.Text, "["),
                new Segment(Kind.Date, @"\n"),
                new Segment(Kind.Text, "]"),
                new Segment(Kind.LineBreak),
                new Segment(Kind.Date, "$|$"),
                new Segment(Kind.Text, " "),
                new Segment(Kind.Cursor));
        }

        [TestMethod]
        public void PreviewOfALongSnippetCostsAboutAsMuchAsReadingIt()
        {
            // PERF-3: 100 KB with 6,000 values used to allocate about 935 MB per keystroke.
            string replacement = string.Concat(Enumerable.Repeat("Dear {{prompt:Name}}, on {{date}} at {{time}}\\n", 2000));
            Assert.IsTrue(replacement.Length > 90_000);
            _ = TextExpanderSnippetPreview.Build("warm {{date}} up", Now);

            long before = GC.GetAllocatedBytesForCurrentThread();
            IReadOnlyList<Segment> segments = TextExpanderSnippetPreview.Build(replacement, Now);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(2000 * 7, segments.Count);
            Assert.IsTrue(allocated < 10_000_000, $"{allocated} bytes for {replacement.Length} characters");
        }

        [TestMethod]
        public void PreviewDescriptionPutsEachBubbleWhereItIs()
        {
            IReadOnlyList<Segment> segments = TextExpanderSnippetPreview.Build("Hi {{prompt:Name}}!{{clipboard}}ok", Now);
            Assert.AreEqual(
                "Hi (Prompt Name)! (Clipboard ) ok",
                TextExpanderSnippetPreview.Describe(segments, segment => segment.Kind + " " + segment.Value));
        }

        [TestMethod]
        public void VariablesAreTheEngineVariablesAndOnlyTheFirstCursorMarker()
        {
            const string text = "a {{date}} {{dat}} {{prompt:Name}} {{prompt?:Note}} $|$ {{clipboard}} $|$ {{date:dd}}";
            (int Start, int End)[] expected =
            {
                Range(text, "{{date}}"),
                Range(text, "{{prompt:Name}}"),
                Range(text, "{{prompt?:Note}}"),
                Range(text, "$|$"),
                Range(text, "{{clipboard}}"),
                Range(text, "{{date:dd}}"),
            };

            CollectionAssert.AreEqual(expected, TextExpanderTokenText.FindVariables(text).ToArray());
        }

        [TestMethod]
        public void BackspaceAndDeleteRemoveANeighbouringVariableWhole()
        {
            const string text = "Hi {{clipboard}}!";
            (int Start, int End) clipboard = Range(text, "{{clipboard}}");

            Assert.AreEqual(clipboard, TextExpanderTokenText.VariableToDelete(text, clipboard.End, backward: true));
            Assert.AreEqual(clipboard, TextExpanderTokenText.VariableToDelete(text, clipboard.Start, backward: false));

            // Anywhere else the keys act as usual.
            Assert.IsNull(TextExpanderTokenText.VariableToDelete(text, clipboard.Start, backward: true));
            Assert.IsNull(TextExpanderTokenText.VariableToDelete(text, clipboard.End, backward: false));
            Assert.IsNull(TextExpanderTokenText.VariableToDelete(text, clipboard.End - 1, backward: true));
        }

        [TestMethod]
        public void AClickInsideAVariableFindsItButItsEdgesDoNot()
        {
            const string text = "x {{prompt:Name}} y";
            (int Start, int End) prompt = Range(text, "{{prompt:Name}}");

            Assert.AreEqual(prompt, TextExpanderTokenText.VariableAround(text, prompt.Start + 1));
            Assert.AreEqual(prompt, TextExpanderTokenText.VariableAround(text, prompt.End - 1));
            Assert.IsNull(TextExpanderTokenText.VariableAround(text, prompt.Start));
            Assert.IsNull(TextExpanderTokenText.VariableAround(text, prompt.End));
        }

        [TestMethod]
        public void InsertedQuestionSelectsItsLabelAndCustomDateSelectsItsFormat()
        {
            Assert.AreEqual((14, 5), TextExpanderTokenText.SelectionAfterInsert("{{prompt:Label}}", 5));
            Assert.AreEqual((15, 5), TextExpanderTokenText.SelectionAfterInsert("{{prompt?:Label}}", 5));
            Assert.AreEqual((7, 4), TextExpanderTokenText.SelectionAfterInsert("{{date:dddd}}", 0));
            Assert.AreEqual((16, 0), TextExpanderTokenText.SelectionAfterInsert("{{clipboard}}", 3));
            Assert.AreEqual((3, 0), TextExpanderTokenText.SelectionAfterInsert("$|$", 0));
        }

        [TestMethod]
        public void InsertVariableNeverLandsInsideAnotherVariable()
        {
            const string text = "Dear {{prompt:Label}}, hi";
            (int Start, int End) prompt = Range(text, "{{prompt:Label}}");

            // Right after inserting a question its label is selected; a second variable goes after it.
            (int Start, int Length) label = TextExpanderTokenText.SelectionAfterInsert("{{prompt:Label}}", prompt.Start);
            Assert.AreEqual((prompt.End, prompt.End), TextExpanderTokenText.InsertionRange(text, label.Start, label.Start + label.Length));
            Assert.AreEqual((prompt.End, prompt.End), TextExpanderTokenText.InsertionRange(text, prompt.Start + 3, prompt.Start + 3));

            // Otherwise it replaces the selection.
            Assert.AreEqual((0, 4), TextExpanderTokenText.InsertionRange(text, 0, 4));
            Assert.AreEqual((prompt.End, prompt.End), TextExpanderTokenText.InsertionRange(text, prompt.End, prompt.End));
        }

        [TestMethod]
        public void PromptTokensBuildAndParseExactly()
        {
            Assert.AreEqual("{{prompt:Question}}", TextExpanderPromptToken.Build(" Question ", requireAnswer: true));
            Assert.AreEqual("{{prompt?:Question}}", TextExpanderPromptToken.Build("Question", requireAnswer: false));

            Assert.IsTrue(TextExpanderPromptToken.TryParse("{{prompt:Question}}", out string requiredQuestion, out bool required));
            Assert.AreEqual("Question", requiredQuestion);
            Assert.IsTrue(required);

            Assert.IsTrue(TextExpanderPromptToken.TryParse("{{prompt?:Anything?}}", out string optionalQuestion, out bool optionalRequired));
            Assert.AreEqual("Anything?", optionalQuestion);
            Assert.IsFalse(optionalRequired);

            Assert.IsFalse(TextExpanderPromptToken.TryParse("before {{prompt:Question}}", out _, out _));
            Assert.IsFalse(TextExpanderPromptToken.TryParse("{{prompt:Question}more}}", out _, out _));
            Assert.AreEqual(TextExpanderPromptValidationError.QuestionRequired, TextExpanderPromptToken.Validate(" "));
            Assert.AreEqual(TextExpanderPromptValidationError.InvalidQuestion, TextExpanderPromptToken.Validate("Bad } question"));

            const string text = "Before {{prompt:Question}} after";
            (int Start, int End) range = Range(text, "{{prompt:Question}}");
            Assert.IsTrue(TextExpanderPromptToken.TryFindAtSelection(text, range.Start, range.End, out int selectedStart, out int selectedEnd));
            Assert.AreEqual(range, (selectedStart, selectedEnd));
            Assert.IsTrue(TextExpanderPromptToken.TryFindAtSelection(text, range.Start + 2, range.Start + 2, out _, out _));
            Assert.IsFalse(TextExpanderPromptToken.TryFindAtSelection(text, range.End, range.End, out _, out _));
        }

        [TestMethod]
        public void NewPromptStartsAtTheCaretOrReplacesTheSelection()
        {
            TextExpanderPromptEditSession atCaret = TextExpanderPromptEditSession.BeginNew("Hello ", 6, 6);
            Assert.AreEqual("Hello {{prompt:Question}}", atCaret.InitialText);
            Assert.AreEqual((6, 25), (atCaret.TokenStart, atCaret.TokenEnd));
            Assert.AreEqual(string.Empty, atCaret.Question);
            Assert.IsTrue(atCaret.RequireAnswer);

            TextExpanderPromptEditSession overSelection = TextExpanderPromptEditSession.BeginNew("Hello name!", 6, 10);
            Assert.AreEqual("Hello {{prompt:Question}}!", overSelection.InitialText);

            // A selection inside another variable is preserved and the provisional prompt goes after it.
            const string existing = "Hi {{clipboard}} there";
            (int Start, int End) clipboard = Range(existing, "{{clipboard}}");
            TextExpanderPromptEditSession afterVariable = TextExpanderPromptEditSession.BeginNew(existing, clipboard.Start + 2, clipboard.End - 2);
            Assert.AreEqual("Hi {{clipboard}}{{prompt:Question}} there", afterVariable.InitialText);
        }

        [TestMethod]
        public void EnterAndValidLightDismissCommitWithoutASecondAction()
        {
            TextExpanderPromptEditSession enter = TextExpanderPromptEditSession.BeginNew("Hi !", 3, 3);
            TextExpanderPromptEditDecision enterDecision = enter.Resolve(TextExpanderPromptEditCloseReason.Enter, "Name", requireAnswer: true);
            Assert.AreEqual(TextExpanderPromptEditDecisionKind.Commit, enterDecision.Kind);
            Assert.AreEqual("Hi {{prompt:Name}}!", enterDecision.Text);
            Assert.AreEqual((18, 18), (enterDecision.SelectionStart, enterDecision.SelectionEnd));

            TextExpanderPromptEditSession outside = TextExpanderPromptEditSession.BeginNew("Note: ", 6, 6);
            TextExpanderPromptEditDecision outsideDecision = outside.Resolve(TextExpanderPromptEditCloseReason.LightDismiss, "Details", requireAnswer: false);
            Assert.AreEqual(TextExpanderPromptEditDecisionKind.Commit, outsideDecision.Kind);
            Assert.AreEqual("Note: {{prompt?:Details}}", outsideDecision.Text);
        }

        [TestMethod]
        public void InvalidLightDismissKeepsThePromptOpen()
        {
            TextExpanderPromptEditSession session = TextExpanderPromptEditSession.BeginNew("Hi ", 3, 3);

            TextExpanderPromptEditDecision empty = session.Resolve(TextExpanderPromptEditCloseReason.LightDismiss, string.Empty, requireAnswer: true);
            Assert.AreEqual(TextExpanderPromptEditDecisionKind.KeepOpen, empty.Kind);
            Assert.AreEqual(TextExpanderPromptValidationError.QuestionRequired, empty.Error);
            Assert.AreEqual(session.InitialText, empty.Text);

            TextExpanderPromptEditDecision malformed = session.Resolve(TextExpanderPromptEditCloseReason.LightDismiss, "Bad } question", requireAnswer: true);
            Assert.AreEqual(TextExpanderPromptEditDecisionKind.KeepOpen, malformed.Kind);
            Assert.AreEqual(TextExpanderPromptValidationError.InvalidQuestion, malformed.Error);
        }

        [TestMethod]
        public void EscapeRemovesANewPromptAndRestoresTheOriginalSelection()
        {
            TextExpanderPromptEditSession session = TextExpanderPromptEditSession.BeginNew("Replace this text", 8, 12);
            TextExpanderPromptEditDecision decision = session.Resolve(TextExpanderPromptEditCloseReason.Cancel, "Ignored", requireAnswer: true);

            Assert.AreEqual(TextExpanderPromptEditDecisionKind.Cancel, decision.Kind);
            Assert.AreEqual("Replace this text", decision.Text);
            Assert.AreEqual((8, 12), (decision.SelectionStart, decision.SelectionEnd));
        }

        [TestMethod]
        public void ExistingPromptReopensAndEscapeReselectsTheUnchangedToken()
        {
            const string text = "Hi {{prompt?:First name}}!";
            (int Start, int End) prompt = Range(text, "{{prompt?:First name}}");

            Assert.IsTrue(TextExpanderPromptEditSession.TryBeginExisting(text, prompt.Start + 4, prompt.Start + 4, out TextExpanderPromptEditSession fromCaret));
            Assert.AreEqual("First name", fromCaret.Question);
            Assert.IsFalse(fromCaret.RequireAnswer);

            Assert.IsTrue(TextExpanderPromptEditSession.TryBeginExisting(text, prompt.Start, prompt.End, out TextExpanderPromptEditSession fromSelection));
            TextExpanderPromptEditDecision decision = fromSelection.Resolve(TextExpanderPromptEditCloseReason.Cancel, "Changed", requireAnswer: true);
            Assert.AreEqual(text, decision.Text);
            Assert.AreEqual(prompt, (decision.SelectionStart, decision.SelectionEnd));

            Assert.IsFalse(TextExpanderPromptEditSession.TryBeginExisting("{{date}}", 2, 2, out _));
        }

        [TestMethod]
        public void ExistingPromptPreservesAndCanChangeItsAnswerRequirement()
        {
            const string optionalText = "{{prompt?:Details}}";
            Assert.IsTrue(TextExpanderPromptEditSession.TryBeginExisting(optionalText, 2, 2, out TextExpanderPromptEditSession optional));
            Assert.IsFalse(optional.RequireAnswer);
            Assert.AreEqual(
                "{{prompt?:Updated}}",
                optional.Resolve(TextExpanderPromptEditCloseReason.Enter, "Updated", optional.RequireAnswer).Text);

            TextExpanderPromptEditDecision madeRequired = optional.Resolve(TextExpanderPromptEditCloseReason.Enter, "Updated", requireAnswer: true);
            Assert.AreEqual("{{prompt:Updated}}", madeRequired.Text);

            const string spaced = "{{prompt:  Keep these spaces  }}";
            Assert.IsTrue(TextExpanderPromptEditSession.TryBeginExisting(spaced, 2, 2, out TextExpanderPromptEditSession unchanged));
            Assert.AreEqual(spaced, unchanged.Resolve(TextExpanderPromptEditCloseReason.LightDismiss, unchanged.Question, unchanged.RequireAnswer).Text);
        }

        [TestMethod]
        public void CompletedNewPromptIsOneUndoStep()
        {
            const string original = "Hi ";
            TextExpanderPromptEditSession session = TextExpanderPromptEditSession.BeginNew(original, original.Length, original.Length);
            TextExpanderPromptEditDecision decision = session.Resolve(TextExpanderPromptEditCloseReason.Enter, "Name", requireAnswer: true);
            var history = new TextExpanderEditHistory(original);

            history.Record(decision.Text);

            Assert.IsTrue(history.TryUndo(out string undone, out _, out _));
            Assert.AreEqual(original, undone);
            Assert.IsFalse(history.CanUndo);
        }

        [TestMethod]
        public void EditorLineBreaksBecomeTheNewlinesSnippetsUse()
        {
            // RichEdit ends each paragraph with \r, and the story with one more nobody typed.
            Assert.AreEqual("one\ntwo", TextExpanderTokenText.FromEditor("one\rtwo\r", 8));
            Assert.AreEqual("one\n", TextExpanderTokenText.FromEditor("one\r\r", 5));
            Assert.AreEqual("one\ntwo", TextExpanderTokenText.FromEditor("one\rtwo", 8));
            Assert.AreEqual(string.Empty, TextExpanderTokenText.FromEditor("\r", 1));

            Assert.AreEqual("a\rb\rc\rd", TextExpanderTokenText.ToEditor("a\r\nb\nc\rd"));
            Assert.AreEqual("a\nb\nc\nd", TextExpanderTokenText.FromEditor(TextExpanderTokenText.ToEditor("a\r\nb\nc\rd") + "\r", 8));
        }

        [TestMethod]
        public void UndoStepsBackAWordAtATime()
        {
            var history = new TextExpanderEditHistory();
            Type(history, string.Empty, "hello world");

            Assert.IsTrue(history.TryUndo(out string text, out int start, out int end));
            Assert.AreEqual("hello ", text);
            Assert.AreEqual((6, 6), (start, end));

            Assert.IsTrue(history.TryUndo(out text, out start, out end));
            Assert.AreEqual(string.Empty, text);
            Assert.AreEqual((0, 0), (start, end));
            Assert.IsFalse(history.CanUndo);

            Assert.IsTrue(history.TryRedo(out text, out start, out end));
            Assert.AreEqual("hello ", text);
            Assert.AreEqual((6, 6), (start, end));
            Assert.IsTrue(history.TryRedo(out text, out _, out _));
            Assert.AreEqual("hello world", text);
            Assert.IsFalse(history.CanRedo);
        }

        [TestMethod]
        public void UndoPutsBackARunOfBackspacesAndSelectsIt()
        {
            var history = new TextExpanderEditHistory("hello");
            history.Record("hell");
            history.Record("hel");
            history.Record("he");

            Assert.IsTrue(history.TryUndo(out string text, out int start, out int end));
            Assert.AreEqual("hello", text);
            Assert.AreEqual((2, 5), (start, end));
            Assert.IsFalse(history.CanUndo);
        }

        [TestMethod]
        public void PastesAndVariablesAreSeparateStepsAndANewChangeClearsRedo()
        {
            var history = new TextExpanderEditHistory();
            Type(history, string.Empty, "ab");
            history.Record("ab{{clipboard}}");
            history.EndRun();
            history.Record("ab{{clipboard}}c");

            Assert.IsTrue(history.TryUndo(out string text, out _, out _));
            Assert.AreEqual("ab{{clipboard}}", text);
            Assert.IsTrue(history.TryUndo(out text, out int start, out int end));
            Assert.AreEqual("ab", text);
            Assert.AreEqual((2, 2), (start, end));

            history.Record("abX");
            Assert.IsFalse(history.CanRedo);
            Assert.IsTrue(history.TryUndo(out text, out _, out _));
            Assert.AreEqual("ab", text);
        }

        [TestMethod]
        public void PlainTextUsesTheBoxTextColorNeverTheAutomaticColor()
        {
            // Stand-ins for TextConstants.AutoColor and UndefinedColor, which need the WinUI runtime.
            Color automatic = Rgb(0, 0, 0, alpha: 0);
            Color undefined = Rgb(1, 2, 3, alpha: 0);
            Color white = Rgb(255, 255, 255);
            Color lightThemeText = Rgb(0, 0, 0, alpha: 0xE4);

            // The box's own text color for its theme and state, as XAML keeps it on the document.
            Assert.AreEqual(white, TextExpanderEditorColors.PlainText(white, automatic, undefined, lightThemeText));
            Assert.AreEqual(Rgb(0, 0, 0), TextExpanderEditorColors.PlainText(lightThemeText, automatic, undefined, white), "Made opaque; XAML applies the opacity.");

            // RichEdit's automatic color is the Win32 window text color, black in dark mode too.
            Assert.AreEqual(white, TextExpanderEditorColors.PlainText(automatic, automatic, undefined, white));
            Assert.AreEqual(white, TextExpanderEditorColors.PlainText(undefined, automatic, undefined, white));
            Assert.AreEqual(white, TextExpanderEditorColors.PlainText(Rgb(9, 9, 9, alpha: 0), automatic, undefined, white));
        }

        [TestMethod]
        public void VariablesGetAccentTextOnAFaintTintAndNothingInHighContrast()
        {
            // Dark theme: accent text over a translucent box over the dialog.
            (Color Text, Color Background)? dark = TextExpanderEditorColors.Variables(false, Rgb(153, 235, 255), Rgb(30, 30, 30, alpha: 179), Rgb(32, 32, 32));
            Assert.AreEqual(Rgb(153, 235, 255), dark.Value.Text);
            Assert.AreEqual(Rgb(48, 60, 62), dark.Value.Background);

            (Color Text, Color Background)? light = TextExpanderEditorColors.Variables(false, Rgb(0, 62, 146), Rgb(255, 255, 255), Rgb(243, 243, 243));
            Assert.AreEqual(Rgb(0, 62, 146), light.Value.Text);
            Assert.AreEqual(Rgb(219, 228, 240), light.Value.Background);

            Assert.IsNull(TextExpanderEditorColors.Variables(true, Rgb(153, 235, 255), Rgb(30, 30, 30), Rgb(32, 32, 32)), "High contrast keeps the system colors.");

            // QA-25: without colors of their own, variables are underlined so they still stand out.
            Assert.IsTrue(TextExpanderEditorColors.UnderlinesVariables(TextExpanderEditorColors.Variables(true, Rgb(153, 235, 255), Rgb(30, 30, 30), Rgb(32, 32, 32))));
            Assert.IsFalse(TextExpanderEditorColors.UnderlinesVariables(dark));
            Assert.IsFalse(TextExpanderEditorColors.UnderlinesVariables(light));
        }

        private static Color Rgb(byte r, byte g, byte b, byte alpha = 255) => new() { A = alpha, R = r, G = g, B = b };

        private static (int Start, int End) Range(string text, string part)
        {
            int start = text.IndexOf(part, StringComparison.Ordinal);
            return (start, start + part.Length);
        }

        private static void Type(TextExpanderEditHistory history, string before, string typed)
        {
            string text = before;
            foreach (char character in typed)
            {
                text += character;
                history.Record(text);
            }
        }

        private static void AssertPreview(string replacement, params Segment[] expected)
        {
            CollectionAssert.AreEqual(expected, TextExpanderSnippetPreview.Build(replacement, Now).ToArray());
        }
    }
}
