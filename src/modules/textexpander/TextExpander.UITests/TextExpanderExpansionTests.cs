// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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
    // A copied block with one email address, a phone number and two links, sample data only.
    private const string CopiedBlock = "Questions? Email you@example.com or call 555-0123.\nTrack it: https://example.com/track/1042\nReturns: https://example.com/returns";
    private const ushort VirtualKeyEnd = 0x23;

    private static readonly Snippet ReplyTo = new(";tereply", "Reply to {{clipboard:email}}");
    private static readonly Snippet TrackLink = new(";telink", "Track: {{clipboard:link}}");
    private static readonly Snippet PickACopy = new(";tepick", "Copy: {{clipboard:pick}}.");

    internal override TextExpanderOptions InitialOptions => TestContext.TestName switch
    {
        nameof(AlwaysPastePutsTheClipboardBack) => new() { InjectionBackend = "clipboard" },
        nameof(AlwaysTypeTypesTheSnippet) => new() { InjectionBackend = "type" },
        _ => new(),
    };

    internal override IReadOnlyList<Snippet>? InitialSnippets => TestContext.TestName switch
    {
        nameof(ClipboardPartsGoInAndAMatchIsPickedInTextExpandersWindow) or nameof(CancellingAPickCancelsTheSnippet) or nameof(NothingFoundLeavesTheSpotEmptyAndTypingGoesOn)
            or nameof(ALongListOfMatchesScrollsAndEachKeepsItsName) or nameof(ClosedWindowsLeaveNothingBehind) => [.. DefaultSnippets, ReplyTo, TrackLink],
        nameof(PickingACopyInsertsWhatClipboardHistoryHas) => [.. DefaultSnippets, PickACopy],
        _ => DefaultSnippets,
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

    /// <summary>
    /// Esc on a question cancels the whole snippet: nothing is inserted, and the space that released the trigger goes
    /// back, so the box shows exactly what was typed.
    /// </summary>
    [TestMethod("TextExpander.Expansion.CancellingAQuestionCancelsTheSnippet")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void CancellingAQuestionCancelsTheSnippet()
    {
        var prompt = TypeTriggerForPrompt(Note.Trigger + " ");
        Assert.IsTrue(prompt.WaitForForeground(), $"The question window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        StringAssert.Contains(prompt.Question, "Your note", "The question window doesn't ask the snippet's question.");

        Step("Pressing Esc");
        TextInput.Escape();
        Assert.IsTrue(prompt.WaitForClosed(), "The question window did not close after Esc.");
        AssertTargetShows(Note.Trigger + " ");
        AssertStaysAsTyped(() => Target.Text, Note.Trigger + " ", observeMS: 2_000, "the target");
    }

    /// <summary>
    /// A clipboard variable inserts the part of the copy it names: the one email address in a copied block goes straight in.
    /// With two links, Text Expander's window lists both in front of the app; Enter inserts the first, a click the other.
    /// </summary>
    [TestMethod("TextExpander.Expansion.ClipboardPartsGoInAndAMatchIsPickedInTextExpandersWindow")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void ClipboardPartsGoInAndAMatchIsPickedInTextExpandersWindow()
    {
        Assert.IsTrue(ClipboardHelper.SetText(CopiedBlock), "The clipboard could not be set.");
        AssertExpands(ReplyTo.Trigger + " ", "Reply to you@example.com ", waitForEngine: true);

        var choice = TypeTriggerForChoice(TrackLink.Trigger + " ");
        Assert.IsTrue(choice.WaitForForeground(), $"The choice window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        Assert.IsTrue(choice.Offers("https://example.com/track/1042") && choice.Offers("https://example.com/returns"), "The choice window doesn't list both links.");
        Step("Pressing Enter");
        TextInput.Enter();
        Assert.IsTrue(choice.WaitForClosed(), "The choice window did not close after Enter.");
        AssertTargetShows("Track: https://example.com/track/1042 ");

        choice = TypeTriggerForChoice(TrackLink.Trigger + " ");
        Step("Clicking the second link");
        choice.Pick("https://example.com/returns");
        Assert.IsTrue(choice.WaitForClosed(), "The choice window did not close after a click.");
        AssertTargetShows("Track: https://example.com/returns ");
        var clipboard = WaitHelper.WaitForStable(ClipboardHelper.GetText, text => text == CopiedBlock, timeoutMS: 10_000, requiredConsecutiveMatches: 3, pollIntervalMS: 200);
        Assert.IsTrue(clipboard.Succeeded, $"The clipboard holds '{TextExpanderFiles.Visible(clipboard.LastObservation)}', not what was copied.");
    }

    /// <summary>
    /// Question and choice windows leave nothing behind once they close. Windows App SDK kept each closed window's title
    /// bar, about 20 GDI objects and 25 handles, and each window loaded its icon again, so an engine asked often enough
    /// ran out of GDI objects. The first windows of each kind set WinUI and its caches up, so the count starts after two
    /// of each, and allows the few objects those caches still come and go by.
    /// </summary>
    [TestMethod("TextExpander.Expansion.ClosedWindowsLeaveNothingBehind")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void ClosedWindowsLeaveNothingBehind()
    {
        Assert.IsTrue(ClipboardHelper.SetText(CopiedBlock), "The clipboard could not be set.");
        for (var i = 0; i < 2; i++)
        {
            AnswerTheTicketQuestion();
            PickTheFirstLink();
        }

        using var engine = Process.GetProcessById(ProcessIds(EngineProcessName).Single());
        var (gdi, user, handles) = SettledObjects(engine);

        for (var i = 0; i < 5; i++)
        {
            AnswerTheTicketQuestion();
            PickTheFirstLink();
        }

        var (gdiAfter, userAfter, handlesAfter) = SettledObjects(engine);
        var counts = $"GDI {gdi} -> {gdiAfter}, USER {user} -> {userAfter}, handles {handles} -> {handlesAfter}";
        Assert.IsTrue(gdiAfter - gdi <= 25, $"Ten more windows left GDI objects behind: {counts}.");
        Assert.IsTrue(userAfter - user <= 6, $"Ten more windows left USER objects behind: {counts}.");
    }

    /// <summary>
    /// A copy without the part a snippet inserts leaves that spot empty, and the rest goes in. A notice at the cursor then
    /// says why, which Narrator can read, and it never takes the focus: what's typed next goes on into the app, and the
    /// user's typing fades it.
    /// </summary>
    [TestMethod("TextExpander.Expansion.NothingFoundLeavesTheSpotEmptyAndTypingGoesOn")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void NothingFoundLeavesTheSpotEmptyAndTypingGoesOn()
    {
        Assert.IsTrue(ClipboardHelper.SetText("Order 1042 shipped today."), "The clipboard could not be set.");
        AssertExpands(ReplyTo.Trigger + " ", "Reply to  ", waitForEngine: true);

        Step("Waiting for the notice at the cursor");
        IntPtr notice = EngineNotice.WaitForShown();
        Assert.AreNotEqual(IntPtr.Zero, notice, "No notice at the cursor said why the spot was left empty.");
        Assert.IsTrue(EngineNotice.IsNamed(notice, "No email address found", out string read), $"The notice doesn't say 'No email address found' to screen readers: {read}");
        Assert.AreEqual(Target.Handle, WindowControl.GetForegroundWindowHandle(), $"The notice took the focus. Foreground: {WindowControl.GetForegroundWindowInfo()}.");

        Step("Typing on right away");
        TextInput.Type("soon");
        AssertTargetShows("Reply to  soon");
        Assert.AreEqual(Target.Handle, WindowControl.GetForegroundWindowHandle(), $"The app lost the focus. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        Assert.IsTrue(EngineNotice.WaitForHidden(notice, timeoutMS: 3_000), "The notice still shows after the user typed.");
    }

    /// <summary>
    /// A long list of matches scrolls, reusing its rows, and each match keeps its name as they're reused: End goes to the
    /// last one, which Enter inserts.
    /// </summary>
    [TestMethod("TextExpander.Expansion.ALongListOfMatchesScrollsAndEachKeepsItsName")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void ALongListOfMatchesScrollsAndEachKeepsItsName()
    {
        string copied = string.Join(" ", Enumerable.Range(1, 40).Select(number => $"user{number}@example.com"));
        Assert.IsTrue(ClipboardHelper.SetText(copied), "The clipboard could not be set.");
        var choice = TypeTriggerForChoice(ReplyTo.Trigger + " ");
        Assert.IsTrue(choice.WaitForForeground(), $"The choice window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        Step("Pressing End");
        TextInput.Press(VirtualKeyEnd);
        Assert.IsTrue(choice.Offers("user40@example.com"), "The last match, scrolled to, has no name.");
        Step("Pressing Enter");
        TextInput.Enter();
        Assert.IsTrue(choice.WaitForClosed(), "The choice window did not close after Enter.");
        AssertTargetShows("Reply to user40@example.com ");
    }

    /// <summary>Esc on the choice window cancels the whole snippet, as on a question: the box shows exactly what was typed.</summary>
    [TestMethod("TextExpander.Expansion.CancellingAPickCancelsTheSnippet")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void CancellingAPickCancelsTheSnippet()
    {
        Assert.IsTrue(ClipboardHelper.SetText(CopiedBlock), "The clipboard could not be set.");
        var choice = TypeTriggerForChoice(TrackLink.Trigger + " ");
        Step("Pressing Esc");
        TextInput.Escape();
        Assert.IsTrue(choice.WaitForClosed(), "The choice window did not close after Esc.");
        AssertTargetShows(TrackLink.Trigger + " ");
        AssertStaysAsTyped(() => Target.Text, TrackLink.Trigger + " ", observeMS: 2_000, "the target");
    }

    /// <summary>
    /// {{clipboard:pick}} offers the text copies in Windows clipboard history: none inserts nothing there and the rest
    /// expands, one goes in, and two or more open Text Expander's window to pick from. With history on, two copies are made
    /// first; what's expected follows what Windows recorded, so the test holds on a desktop that records nothing. A copy
    /// that isn't the test's own is compared but never logged: on a developer's desktop it's theirs.
    /// </summary>
    [TestMethod("TextExpander.Expansion.PickingACopyInsertsWhatClipboardHistoryHas")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void PickingACopyInsertsWhatClipboardHistoryHas()
    {
        const string older = "Older test copy for Text Expander";
        const string newer = "Newer test copy for Text Expander";
        bool on = ClipboardHistoryState.IsOn();
        if (on)
        {
            Assert.IsTrue(ClipboardHelper.SetText(older), "The clipboard could not be set.");
            Thread.Sleep(1_000);
            Assert.IsTrue(ClipboardHelper.SetText(newer), "The clipboard could not be set.");
            Thread.Sleep(1_000);
        }

        IReadOnlyList<string> copies = ClipboardHistoryState.ReadTexts();
        bool ours = copies.Count >= 2 && copies[0] == newer && copies[1] == older;
        Step($"Clipboard history is {(on ? "on" : "off")}, with {copies.Count} text copies{(ours ? ", the test's two newest" : string.Empty)}");
        if (copies.Count == 0)
        {
            AssertExpands(PickACopy.Trigger + " ", "Copy: . ", waitForEngine: true);
            return;
        }

        if (copies.Count == 1)
        {
            WaitForEngineReady();
            Step($"Typing '{PickACopy.Trigger} ' into the target");
            Target.Clear();
            Target.Focus();
            TextInput.Type(PickACopy.Trigger + " ");
            AssertTargetShowsUnlogged("Copy: " + copies[0] + ". ", "the one copy in clipboard history, without a window");
            return;
        }

        var choice = TypeTriggerForChoice(PickACopy.Trigger + " ");
        Assert.IsTrue(choice.WaitForForeground(), $"The choice window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        Assert.IsTrue(choice.Offers("Pick a copy to insert"), "The choice window doesn't ask for a copy.");
        if (ours)
        {
            Assert.IsTrue(choice.Offers(newer) && choice.Offers(older), "The choice window doesn't list both copies.");
            Step("Clicking the older copy");
            choice.Pick(older);
        }
        else
        {
            Step("Pressing Enter on the newest copy");
            TextInput.Enter();
        }

        Assert.IsTrue(choice.WaitForClosed(), "The choice window did not close.");
        AssertTargetShowsUnlogged("Copy: " + (ours ? older : copies[0]) + ". ", ours ? "Copy: " + older + ". " : "the newest copy in clipboard history");
    }

    /// <summary>Waits for <paramref name="expected"/> in the target, reporting only lengths: it may hold a copy from someone's clipboard history.</summary>
    private void AssertTargetShowsUnlogged(string expected, string description)
    {
        var result = WaitHelper.WaitForStable(() => Target.Text, text => text == expected, timeoutMS: 30_000, requiredConsecutiveMatches: 3, pollIntervalMS: 200);
        Assert.IsTrue(
            result.Succeeded,
            $"The target doesn't show {description}: it holds {result.LastObservation?.Length ?? 0} characters, not {expected.Length}. What it holds isn't logged, as it may come from clipboard history.");
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
    /// Snippets deleted from the database file in a text editor are gone after Refresh: the editor stops listing them and
    /// their triggers stay as typed. A snippet with a date variable added in the same save expands, which shows the engine
    /// read the edited file, and so does one that was there all along.
    /// </summary>
    [TestMethod("TextExpander.Expansion.SnippetsDeletedInATextEditorAreGoneAfterRefresh")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Expansion")]
    public void SnippetsDeletedInATextEditorAreGoneAfterRefresh()
    {
        // The engine is ready, and expands a snippet about to be deleted.
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);

        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        var added = new Snippet(";tedate", "Edited on {{isodate}}");
        Snippet[] edited = [.. DefaultSnippets.Where(snippet => snippet != Greeting && snippet != Address), added];
        Step($"Saving the database file in place without {Greeting.Trigger} and {Address.Trigger}, and with {added.Trigger}, as a text editor does");
        TextExpanderFiles.SaveSnippetsInPlace(edited);

        editor.OpenDatabaseMenu();
        Step("Selecting Refresh");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_ReloadSnippets"), 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(editor.WaitForText("Snippets refreshed."), "Refresh did not report that it read the database again.");
        editor.WaitForCount(edited.Length);
        Assert.IsTrue(editor.WaitForRow(Greeting.Trigger, present: false), $"A snippet deleted from the file is still listed. Rows: {editor.DescribeRows()}.");
        Assert.IsTrue(editor.WaitForRow(Address.Trigger, present: false), $"A snippet deleted from the file is still listed. Rows: {editor.DescribeRows()}.");
        Assert.IsTrue(editor.WaitForRow(added.Trigger, present: true), $"The snippet added to the file isn't listed. Rows: {editor.DescribeRows()}.");

        var before = DateTime.Now;
        AssertExpandsTo(
            added.Trigger + " ",
            text => DatesAround(before).Any(date => text == $"Edited on {date} "),
            $"Edited on {Iso(before)} ",
            waitForEngine: true);
        AssertNotExpanded(Greeting.Trigger + " ");
        AssertExpandsTo(
            Letter.Trigger + " ",
            text => DatesAround(before).Any(date => text == $"Dear ,\nSent {date} "),
            $"Dear ,\\nSent {Iso(before)} ");
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

    private void AnswerTheTicketQuestion()
    {
        var prompt = TypeTriggerForPrompt(Ticket.Trigger + " ");
        Assert.IsTrue(prompt.WaitForForeground(), $"The question window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        TextInput.Type("te4217");
        Assert.IsTrue(prompt.WaitForOkEnabled(true), "OK did not turn on once there was an answer.");
        TextInput.Enter();
        Assert.IsTrue(prompt.WaitForClosed(), "The question window did not close after Enter.");
        AssertTargetShows("Ticket te4217 is resolved. ");
    }

    private void PickTheFirstLink()
    {
        var choice = TypeTriggerForChoice(TrackLink.Trigger + " ");
        Assert.IsTrue(choice.WaitForForeground(), $"The choice window did not come in front. Foreground: {WindowControl.GetForegroundWindowInfo()}.");
        TextInput.Enter();
        Assert.IsTrue(choice.WaitForClosed(), "The choice window did not close after Enter.");
        AssertTargetShows("Track: https://example.com/track/1042 ");
    }

    /// <summary>The engine's GDI objects, USER objects and handles, once what closed has had time to go.</summary>
    private (int Gdi, int User, int Handles) SettledObjects(Process engine)
    {
        Step("Letting the closed windows go");
        Thread.Sleep(5_000);
        engine.Refresh();
        return ((int)GetGuiResources(engine.Handle, 0), (int)GetGuiResources(engine.Handle, 1), engine.HandleCount);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);
}
