// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// Expansion as people use it: typing a trigger in an ordinary app, undoing it, the cursor marker, questions, both
/// insertion methods, the editor's exclusion, and changes from the editor and Settings taking effect while the engine runs.
/// </summary>
[TestClass]
public sealed class TextExpanderExpansionTests : TextExpanderTestBase
{
    internal override TextExpanderOptions InitialOptions => TestContext.TestName switch
    {
        nameof(AlwaysPastePutsTheClipboardBack) => new() { InjectionBackend = "clipboard" },
        nameof(AlwaysTypeTypesTheSnippet) => new() { InjectionBackend = "type" },
        _ => new(),
    };

    /// <summary>
    /// With Wait for space or punctuation on, a trigger expands when a space or punctuation follows it, which stays
    /// after the snippet, and not before.
    /// </summary>
    [TestMethod("TextExpander.Expansion.ExpandsAfterASpaceOrPunctuation")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void ExpandsAfterASpaceOrPunctuation()
    {
        Assert.IsTrue(WaitForEngine(running: true), "PowerToys.TextExpander isn't running.");
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);

        AssertNotExpanded(Greeting.Trigger, observeMS: 2_000);
        Step("Typing a period after the trigger");
        TextInput.Type(".");
        AssertTargetShows(Greeting.Replacement + ".");
    }

    /// <summary>Ctrl+Z right after an expansion puts back exactly what was typed.</summary>
    [TestMethod("TextExpander.Expansion.CtrlZPutsTheTriggerBack")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void CtrlZPutsTheTriggerBack()
    {
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);

        Step("Pressing Ctrl+Z");
        TextInput.Undo();
        AssertTargetShows(Greeting.Trigger + " ");
    }

    /// <summary>A snippet's line break and date variable come out as text, and the caret stops at its $|$ marker.</summary>
    [TestMethod("TextExpander.Expansion.MultilineSnippetLeavesTheCaretAtTheMarker")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void MultilineSnippetLeavesTheCaretAtTheMarker()
    {
        var before = DateTime.Now;
        AssertExpandsTo(
            Letter.Trigger + " ",
            text => DatesAround(before).Any(date => text == $"Dear ,\nSent {date} "),
            $"Dear ,\\nSent {Iso(before)} ",
            waitForEngine: true);

        Step("Typing a name where the snippet left the caret");
        TextInput.Type("Lee");
        AssertTargetShows(text => DatesAround(before).Any(date => text == $"Dear Lee,\nSent {date} "), $"Dear Lee,\\nSent {Iso(before)} ");
    }

    /// <summary>
    /// A required question opens Text Expander's window in front: OK is off and Enter explains, until there's an answer,
    /// which the snippet then inserts.
    /// </summary>
    [TestMethod("TextExpander.Expansion.QuestionWindowNeedsAnAnswer")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void QuestionWindowNeedsAnAnswer()
    {
        var prompt = TypeTriggerForPrompt(Ticket.Trigger + " ");
        Assert.IsTrue(prompt.WaitForForeground(), $"The question window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        StringAssert.Contains(prompt.Question, "Ticket number", "The question window doesn't ask the snippet's question.");
        Assert.IsTrue(prompt.WaitForOkEnabled(false), "OK should be off while the answer is blank.");

        Step("Pressing Enter without an answer");
        TextInput.Enter();
        Assert.IsTrue(prompt.Session.WaitFor(() => prompt.ErrorShown, 5_000, pollIntervalMS: 200), "Enter without an answer did not say an answer is needed.");
        Assert.IsTrue(prompt.IsOpen, "The question window closed without an answer.");

        Step("Typing the answer");
        TextInput.Type("te4217");
        Assert.IsTrue(prompt.WaitForOkEnabled(true), "OK did not turn on once there was an answer.");
        Step("Pressing Enter");
        TextInput.Enter();
        Assert.IsTrue(prompt.WaitForClosed(), "The question window did not close after Enter.");
        AssertTargetShows("Ticket te4217 is resolved. ");
    }

    /// <summary>Esc cancels a question, and the snippet goes in with that place left blank.</summary>
    [TestMethod("TextExpander.Expansion.CancelledQuestionLeavesItsPlaceBlank")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void CancelledQuestionLeavesItsPlaceBlank()
    {
        var prompt = TypeTriggerForPrompt(Note.Trigger + " ");
        Assert.IsTrue(prompt.WaitForForeground(), $"The question window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        StringAssert.Contains(prompt.Question, "Your note", "The question window doesn't ask the snippet's question.");

        Step("Pressing Esc");
        TextInput.Escape();
        Assert.IsTrue(prompt.WaitForClosed(), "The question window did not close after Esc.");
        AssertTargetShows("Note: . ");
    }

    /// <summary>With Always paste, the snippet goes in through the clipboard, and what the user had copied is put back.</summary>
    [TestMethod("TextExpander.Expansion.AlwaysPastePutsTheClipboardBack")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void AlwaysPastePutsTheClipboardBack()
    {
        const string copied = "Copied before the expansion";
        Assert.IsTrue(ClipboardHelper.SetText(copied), "The clipboard could not be set.");

        AssertExpands(Address.Trigger + " ", Address.Replacement + " ", waitForEngine: true);

        var clipboard = WaitHelper.WaitForStable(ClipboardHelper.GetText, text => text == copied, timeoutMS: 10_000, requiredConsecutiveMatches: 3, pollIntervalMS: 200);
        Assert.IsTrue(clipboard.Succeeded, $"The clipboard holds '{TextExpanderFiles.Visible(clipboard.LastObservation)}' after the expansion, not what was copied.");
    }

    /// <summary>With Always type, the snippet is typed character by character, without the clipboard.</summary>
    [TestMethod("TextExpander.Expansion.AlwaysTypeTypesTheSnippet")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void AlwaysTypeTypesTheSnippet()
    {
        const string copied = "Copied before the expansion";
        Assert.IsTrue(ClipboardHelper.SetText(copied), "The clipboard could not be set.");

        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);
        Assert.AreEqual(copied, ClipboardHelper.GetText(), "Typing the snippet changed the clipboard.");
    }

    /// <summary>The engine leaves the snippet editor alone, so a trigger typed into a replacement stays as typed.</summary>
    [TestMethod("TextExpander.Expansion.DoesNotExpandInTheSnippetEditor")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void DoesNotExpandInTheSnippetEditor()
    {
        // The same trigger expands in an ordinary app, so the engine is ready.
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);

        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.TypeReplacement(Greeting.Trigger + " ");
        AssertStaysAsTyped(() => editor.ReplacementText, Greeting.Trigger + " ", observeMS: 3_000, "the snippet editor's replacement box");
    }

    /// <summary>A snippet saved in the editor expands right away: the engine picks up the database change by itself.</summary>
    [TestMethod("TextExpander.Expansion.SnippetSavedInTheEditorExpandsRightAway")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void SnippetSavedInTheEditorExpandsRightAway()
    {
        var added = new Snippet(";tefresh", "Saved in the editor a moment ago");
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.SetTrigger(added.Trigger);
        editor.TypeReplacement(added.Replacement);
        Assert.IsTrue(editor.WaitForReplacementText(added.Replacement), $"The replacement box shows '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.Save();
        Assert.IsTrue(editor.WaitForRow(added.Trigger, present: true), $"The new snippet isn't listed. Rows: {editor.DescribeRows()}.");

        AssertExpands(added.Trigger + " ", added.Replacement + " ", waitForEngine: true);
    }

    /// <summary>
    /// Options changed in Settings apply while the engine runs, without a restart: with Wait for space or punctuation
    /// off a trigger expands as soon as it's typed, and with Case-sensitive snippets off its capitalization doesn't matter.
    /// </summary>
    [TestMethod("TextExpander.Expansion.SettingsChangesApplyWhileTheEngineRuns")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void SettingsChangesApplyWhileTheEngineRuns()
    {
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);
        var engine = ProcessIds(EngineProcessName);
        Assert.AreEqual(1, engine.Length, "Expected one Text Expander engine.");

        var settings = OpenTextExpanderPage();
        Step("Turning off Wait for space or punctuation");
        SetSwitch(settings, "Toggle_TextExpander_RequireWordBoundary", on: false);
        AssertOptionSaved("require_word_boundary", false);
        AssertExpands(Greeting.Trigger, Greeting.Replacement, waitForEngine: true);

        Step("Turning off Case-sensitive snippets");
        SetSwitch(settings, "Toggle_TextExpander_CaseSensitive", on: false);
        AssertOptionSaved("case_sensitive", false);
        AssertExpands(Greeting.Trigger.ToUpperInvariant(), Greeting.Replacement, waitForEngine: true);

        CollectionAssert.AreEqual(engine, ProcessIds(EngineProcessName), "The engine restarted to apply the options.");
    }

    private static string Iso(DateTime time) => time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // The date can change while the snippet expands.
    private static IEnumerable<string> DatesAround(DateTime before) => new[] { Iso(before), Iso(DateTime.Now) }.Distinct();

    private void AssertTargetShows(string expected) => AssertTargetShows(text => text == expected, TextExpanderFiles.Visible(expected));

    private void AssertTargetShows(Func<string?, bool> isExpected, string description)
    {
        // Generous: on a busy machine the engine can take many seconds to finish an insertion or an undo.
        var result = WaitHelper.WaitForStable(() => Target.Text, isExpected, timeoutMS: 30_000, requiredConsecutiveMatches: 3, pollIntervalMS: 200);
        Assert.IsTrue(result.Succeeded, $"The target should show '{description}', but shows '{TextExpanderFiles.Visible(result.LastObservation)}'.");
        Step($"Target shows '{description}'");
    }
}
