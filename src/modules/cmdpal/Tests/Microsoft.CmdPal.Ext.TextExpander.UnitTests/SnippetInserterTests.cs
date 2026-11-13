// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.PowerToys.TextExpander;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetInserterTests
{
    private const string Signature = "Best regards,\nYour name";

    // As the clipboard has text: the console ran pasted lines with a lone LF between them as one.
    private const string SignatureOnTheClipboard = "Best regards,\r\nYour name";
    private const string UsersCopy = "Something the user copied";

    private readonly FakeClipboard _clipboard = new() { Text = UsersCopy };
    private readonly FakeDesktop _desktop = new();
    private readonly FakeClock _clock = new();

    private SnippetInserter Inserter => new(_clipboard, _desktop, _clock);

    // Text Expander's defaults, but for the insertion method.
    private (InsertOutcome Outcome, bool ClipboardLost) Insert(string text, InjectionBackend backend = InjectionBackend.Auto)
        => Inserter.Insert(text, backend, InjectionPolicy.DefaultClipboardThresholdChars);

    [TestMethod]
    public void Insert_Paste_PastesKeptOutOfHistoryAndPutsTheUsersClipboardBack()
    {
        string? readByApp = null;
        _clock.Tick = now =>
        {
            _clipboard.OpenedBy = now is >= 30 and < 35 ? FakeDesktop.AppProcess : 0;
            readByApp ??= _clipboard.OpenedBy != 0 ? _clipboard.Text : null;
        };

        var outcome = Insert(Signature);

        Assert.AreEqual((InsertOutcome.Inserted, false), outcome);
        Assert.AreEqual(1, _desktop.HideRequests);
        Assert.AreEqual((SignatureOnTheClipboard, true), _clipboard.Writes.Single());
        Assert.IsTrue(_desktop.Pasted);
        Assert.AreEqual(SignatureOnTheClipboard, readByApp, "The app must read the snippet, not the user's copy.");
        Assert.AreEqual(UsersCopy, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_UserCopiesDuringThePaste_KeepsTheirNewCopy()
    {
        _clock.Tick = now =>
        {
            if (now == 100)
            {
                _clipboard.UserCopies("A newer copy");
            }
        };

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(0, _clipboard.Restores);
        Assert.AreEqual("A newer copy", _clipboard.Text);
    }

    [TestMethod]
    public void Insert_SomeoneCopiesRightAfterThePastesWrite_KeepsTheirCopyAndTypesTheSnippet()
    {
        // Their copy came between the write and the reading of the clipboard's number, so the number was theirs: the
        // restore took the clipboard for the extension's and put the user's old copy over it.
        _clipboard.AfterWrite = () => _clipboard.UserCopies("A newer copy");

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(0, _clipboard.Restores, "Nothing is put back over their copy.");
        Assert.AreEqual("A newer copy", _clipboard.Text);
        Assert.IsFalse(_desktop.Pasted);
        Assert.AreEqual(Signature, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_ClipboardThatCannotBePutBack_IsLeftAloneAndTheSnippetIsTyped()
    {
        _clipboard.CanBeSaved = false;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(0, _clipboard.Writes.Count);
        Assert.AreEqual(UsersCopy, _clipboard.Text);
        Assert.AreEqual(Signature, _desktop.Typed());
        Assert.IsFalse(_desktop.Pasted);
    }

    [TestMethod]
    public void Insert_AlwaysType_NeverTouchesTheClipboardAndPacesTheTyping()
    {
        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature, InjectionBackend.Typing));

        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _clipboard.Writes.Count);
        Assert.AreEqual(Signature, _desktop.Typed());
        Assert.AreEqual(Signature.Length, _desktop.Sent.Count, "One batch per character.");
        Assert.IsTrue(_clock.TotalSlept >= Signature.Length * SnippetInserter.KeyDelayMs);
    }

    [TestMethod]
    public void Insert_WriteThatDoesNotStick_PutsTheClipboardBackAndTypes()
    {
        _clipboard.NextWrite = ClipboardWrite.Failed;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(UsersCopy, _clipboard.Text);
        Assert.AreEqual(Signature, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_CommandPaletteStaysInFront_DoesNothingAndSaysSo()
    {
        _desktop.WindowAfterHide = FakeDesktop.CommandPaletteWindow;

        Assert.AreEqual((InsertOutcome.NoTarget, false), Insert(Signature));
        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _desktop.Sent.Count);
        Assert.IsTrue(_clock.ElapsedMilliseconds >= SnippetInserter.TargetWaitMs);
    }

    [TestMethod]
    public void Insert_AppRunsAsAdministrator_SendsNothing()
    {
        _desktop.CanDriveTarget = false;

        Assert.AreEqual((InsertOutcome.TargetOutOfReach, false), Insert(Signature));
        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _desktop.Sent.Count);
    }

    [TestMethod]
    public void Insert_AnotherWindowComesToTheFrontBeforeThePaste_PutsTheClipboardBackWithoutPasting()
    {
        _clipboard.AfterWrite = () => _desktop.ForegroundWindow = FakeDesktop.OtherWindow;

        Assert.AreEqual((InsertOutcome.TargetChanged, false), Insert(Signature));
        Assert.IsFalse(_desktop.Pasted);
        Assert.AreEqual(UsersCopy, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_AnotherWindowComesToTheFrontWhileTyping_StopsTyping()
    {
        _clock.Tick = now =>
        {
            if (now >= 60)
            {
                _desktop.ForegroundWindow = FakeDesktop.OtherWindow;
            }
        };

        Assert.AreEqual((InsertOutcome.TargetChanged, false), Insert(Signature, InjectionBackend.Typing));
        Assert.IsTrue(_desktop.Typed().Length is > 0 and < 5, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_AppStillBusyAtTheCap_KeepsTheSnippetOnTheClipboardAndSaysSo()
    {
        _desktop.TakingMessages = false;

        Assert.AreEqual((InsertOutcome.Inserted, true), Insert(Signature));
        Assert.AreEqual(0, _clipboard.Restores);
        Assert.AreEqual(SignatureOnTheClipboard, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_RestoreFailsOnce_IsTriedAgain()
    {
        _clipboard.FailingRestores = 1;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(2, _clipboard.Restores);
        Assert.AreEqual(UsersCopy, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_RestoreKeepsFailing_SaysTheClipboardWasNotPutBack()
    {
        _clipboard.FailingRestores = SnippetInserter.RestoreAttempts;

        Assert.AreEqual((InsertOutcome.Inserted, true), Insert(Signature));
    }

    [TestMethod]
    public void Insert_UserCopiesWhileTheRestoreWaitsToOpenTheClipboard_KeepsTheirNewCopy()
    {
        _clipboard.WhileOpening = () => _clipboard.UserCopies("A newer copy");

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(1, _clipboard.Restores);
        Assert.AreEqual("A newer copy", _clipboard.Text);
    }

    [TestMethod]
    public void Insert_UserCopiesWhileAnotherAppHoldsTheClipboard_KeepsTheirNewCopy()
    {
        _clipboard.UnopenableRestores = 1;
        _clipboard.WhileOpening = () => _clipboard.UserCopies("A newer copy");

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(1, _clipboard.Restores, "The retry mustn't take the newer copy for the extension's own change.");
        Assert.AreEqual("A newer copy", _clipboard.Text);
    }

    [TestMethod]
    public void Insert_ClipboardHeldByAnotherAppForEveryRestore_SaysTheClipboardWasNotPutBack()
    {
        _clipboard.UnopenableRestores = SnippetInserter.RestoreAttempts;

        Assert.AreEqual((InsertOutcome.Inserted, true), Insert(Signature));
        Assert.AreEqual(SnippetInserter.RestoreAttempts, _clipboard.Restores);
    }

    [TestMethod]
    public void Insert_EmptyClipboard_IsPutBackEmpty()
    {
        _clipboard.Text = null;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.IsNull(_clipboard.Text);
    }

    [TestMethod]
    public void Insert_ModifierStillHeld_IsLetGoOfInTheSameBatchAsCtrlV()
    {
        _desktop.Held.Add(KeyStrokes.RightControl);

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.AreEqual(KeyStroke.KeyUp(KeyStrokes.RightControl), _desktop.Sent.Single()[0]);
        Assert.IsTrue(_clock.ElapsedMilliseconds >= SnippetInserter.ModifierReleaseWaitMs);
    }

    [TestMethod]
    public void Insert_EmptySnippet_HidesCommandPaletteAndSendsNothing()
    {
        Assert.AreEqual((InsertOutcome.Empty, false), Insert(string.Empty));
        Assert.AreEqual(1, _desktop.HideRequests);
        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _desktop.Sent.Count);
    }

    [TestMethod]
    public void Insert_PasteCutOffPartWay_LetsGoOfCtrlAndWaitsForTheAppBeforePuttingTheClipboardBack()
    {
        // SendInput can stop part way through a batch: the Ctrl+V left Ctrl down, so the user's next keys were shortcuts,
        // and the clipboard went back at once, under a V that may still have been on its way.
        _desktop.SendCounts.Enqueue(2);

        Assert.AreEqual((InsertOutcome.Failed, false), Insert(Signature));
        CollectionAssert.AreEqual(new[] { KeyStroke.KeyUp(KeyStrokes.Control) }, _desktop.Sent[1].ToArray(), "Ctrl is let go of.");
        Assert.IsTrue(_clock.ElapsedMilliseconds >= PasteSettle.SettleMs, "The clipboard waits for the app to read.");
        Assert.AreEqual(UsersCopy, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_NoPasteInputAccepted_RestoresTheClipboardWithoutWaitingForABusyApp()
    {
        _desktop.SendCounts.Enqueue(0);
        _desktop.TakingMessages = false;

        Assert.AreEqual((InsertOutcome.Failed, false), Insert(Signature));

        Assert.AreEqual(1, _clipboard.Restores);
        Assert.AreEqual(UsersCopy, _clipboard.Text);
        Assert.IsFalse(_desktop.Pasted);
        Assert.AreEqual(0, _desktop.Accepted.SelectMany(batch => batch).Count(), "No snippet input was accepted.");
        Assert.IsTrue(_clock.ElapsedMilliseconds < PasteSettle.SettleMs, "A busy target has no paste queued to wait for.");
    }

    [TestMethod]
    [DataRow(1u)]
    [DataRow(2u)]
    public void Insert_NonzeroPartialPasteInput_WaitsForABusyAppAndPreservesTheFailure(uint accepted)
    {
        _desktop.SendCounts.Enqueue(accepted);
        _desktop.TakingMessages = false;

        Assert.AreEqual((InsertOutcome.Failed, true), Insert(Signature));

        Assert.AreEqual(0, _clipboard.Restores, "A nonzero partial batch may have queued the paste.");
        Assert.AreEqual(SignatureOnTheClipboard, _clipboard.Text);
        Assert.IsTrue(_clock.ElapsedMilliseconds >= PasteSettle.BusyCapMs);
        CollectionAssert.AreEqual(new[] { KeyStroke.KeyUp(KeyStrokes.Control) }, _desktop.Sent[1].ToArray(), "Cleanup input does not make the paste complete.");
    }

    [TestMethod]
    public void Insert_TypedLineBreakCutOff_LetsGoOfEnter()
    {
        _desktop.SendCounts.Enqueue(1);

        Assert.AreEqual((InsertOutcome.Failed, false), Insert("\nNext line", InjectionBackend.Typing));
        CollectionAssert.AreEqual(new[] { KeyStroke.KeyUp(KeyStrokes.Return) }, _desktop.Sent[1].ToArray());
    }

    [TestMethod]
    public void Insert_AutomaticShortSnippet_IsTypedAsTextExpanderTypesIt()
    {
        // Settings' Insertion method is Text Expander's: Automatic types what's shorter than its minimum to paste.
        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("Hi"));
        Assert.AreEqual(0, _clipboard.Writes.Count);
        Assert.AreEqual("Hi", _desktop.Typed());
    }

    [TestMethod]
    public void Insert_AutomaticWithAHigherMinimum_TypesWhatsShorter()
    {
        Assert.AreEqual((InsertOutcome.Inserted, false), Inserter.Insert("Kind regards", InjectionBackend.Auto, clipboardThresholdChars: 40));
        Assert.AreEqual(0, _clipboard.Writes.Count);
        Assert.AreEqual("Kind regards", _desktop.Typed());
    }

    [TestMethod]
    public void Insert_AutomaticOneLineInWindowsTerminal_IsTypedSoTheSpacesAtTheEndStay()
    {
        // Windows Terminal trims the spaces at the end of a one-line paste, so "git commit -m " lost its space.
        _desktop.AppIsWindowsTerminal = true;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("git commit -m "));
        Assert.AreEqual(0, _clipboard.Writes.Count);
        Assert.AreEqual("git commit -m ", _desktop.Typed());
    }

    [TestMethod]
    public void Insert_LinesInWindowsTerminal_ArePastedSoItCanWarnAboutThem()
    {
        _desktop.AppIsWindowsTerminal = true;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert(Signature));
        Assert.IsTrue(_desktop.Pasted);
    }

    [TestMethod]
    public void Insert_AlwaysPasteInWindowsTerminal_StillPastes()
    {
        _desktop.AppIsWindowsTerminal = true;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("git commit -m ", InjectionBackend.Clipboard));
        Assert.IsTrue(_desktop.Pasted);
    }

    [TestMethod]
    public void Insert_OneLineFromACopyInWindowsTerminal_IsPastedSoTerminalFiltersIt()
    {
        // Typed, a Ctrl+O hidden in a copied link would run the line in bash. Terminal filters a paste.
        _desktop.AppIsWindowsTerminal = true;

        Assert.AreEqual((InsertOutcome.Inserted, false), Inserter.Insert("curl https://example.com/install ", InjectionBackend.Auto, InjectionPolicy.DefaultClipboardThresholdChars, fromClipboard: true));
        Assert.IsTrue(_desktop.Pasted);
        Assert.AreEqual(string.Empty, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_OneLineWithAControlCharacterInWindowsTerminal_IsPasted()
    {
        _desktop.AppIsWindowsTerminal = true;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("name\tvalue"));
        Assert.IsTrue(_desktop.Pasted);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Insert_TypedIntoATerminal_LeavesOutControlCharacters(bool windowsTerminal, bool console)
    {
        _desktop.AppIsWindowsTerminal = windowsTerminal;
        _desktop.AppIsConsole = console;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("echo \u001B[2Jhi\u000F\u007F\u0085 there\tnow", InjectionBackend.Typing));
        Assert.AreEqual("echo [2Jhi there\tnow", _desktop.Typed());
    }

    [TestMethod]
    public void Insert_TypedIntoAnotherApp_KeepsControlCharacters()
    {
        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("a\u000Fb", InjectionBackend.Typing));
        Assert.AreEqual("a\u000Fb", _desktop.Typed());
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Insert_LinesATerminalCantGetAsAPaste_AreNotTyped(bool windowsTerminal, bool console)
    {
        // Typed, each line would run as it went in, past the warning Terminal shows for a paste with lines.
        _desktop.AppIsWindowsTerminal = windowsTerminal;
        _desktop.AppIsConsole = console;
        _clipboard.CanBeSaved = false;

        Assert.AreEqual((InsertOutcome.TerminalNeedsPaste, false), Insert("cd ~/project\nrm -rf build"));
        Assert.AreEqual(0, _desktop.Sent.Count);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Insert_TerminalFallbackRefused_PreservesTheClipboardRestoreFailure(bool windowsTerminal, bool console)
    {
        _desktop.AppIsWindowsTerminal = windowsTerminal;
        _desktop.AppIsConsole = console;
        _clipboard.NextWrite = ClipboardWrite.Failed;
        _clipboard.FailingRestores = SnippetInserter.RestoreAttempts;

        Assert.AreEqual((InsertOutcome.TerminalNeedsPaste, true), Insert(Signature));

        Assert.IsNull(_clipboard.Text);
        Assert.AreEqual(SnippetInserter.RestoreAttempts, _clipboard.Restores);
        Assert.AreEqual(0, _desktop.Sent.Count);
    }

    [TestMethod]
    public void Insert_OneLineATerminalCantGetAsAPaste_IsTyped()
    {
        _desktop.AppIsConsole = true;
        _clipboard.CanBeSaved = false;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("git status --short"));
        Assert.AreEqual("git status --short", _desktop.Typed());
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Insert_AlwaysTypeLinesWithCopiedTextIntoATerminal_AreNotTyped(bool windowsTerminal, bool console)
    {
        // Typed, each line would run as it went in, and a copy can hide a command.
        _desktop.AppIsWindowsTerminal = windowsTerminal;
        _desktop.AppIsConsole = console;

        Assert.AreEqual((InsertOutcome.TerminalCopiedLines, false), Inserter.Insert("Hi,\nyou@example.com", InjectionBackend.Typing, InjectionPolicy.DefaultClipboardThresholdChars, fromClipboard: true));
        Assert.AreEqual(0, _desktop.Sent.Count);
        Assert.AreEqual(0, _clipboard.Writes.Count);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Insert_AlwaysTypeLinesTheUserWroteIntoATerminal_AreTyped(bool windowsTerminal, bool console)
    {
        _desktop.AppIsWindowsTerminal = windowsTerminal;
        _desktop.AppIsConsole = console;

        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("cd ~/project\nls", InjectionBackend.Typing));
        Assert.AreEqual("cd ~/project\nls", _desktop.Typed());
        Assert.AreEqual(0, _clipboard.Writes.Count);
    }

    [TestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    public void Insert_AutomaticLinesIntoATerminal_ArePastedWhoeverWroteThem(bool windowsTerminal, bool console)
    {
        _desktop.AppIsWindowsTerminal = windowsTerminal;
        _desktop.AppIsConsole = console;

        Assert.AreEqual((InsertOutcome.Inserted, false), Inserter.Insert("Hi,\nyou@example.com", InjectionBackend.Auto, InjectionPolicy.DefaultClipboardThresholdChars, fromClipboard: true));
        Assert.AreEqual((InsertOutcome.Inserted, false), Insert("cd ~/project\nls"));
        Assert.AreEqual(2, _clipboard.Writes.Count);
        Assert.AreEqual(string.Empty, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_AlwaysTypeLinesWithCopiedTextIntoAnotherApp_AreTyped()
    {
        Assert.AreEqual((InsertOutcome.Inserted, false), Inserter.Insert("Hi,\nyou@example.com", InjectionBackend.Typing, InjectionPolicy.DefaultClipboardThresholdChars, fromClipboard: true));
        Assert.AreEqual("Hi,\nyou@example.com", _desktop.Typed());
    }

    [TestMethod]
    public void MessageFor_LinesATerminalCantGetAsAPaste_SaysWhy()
    {
        StringAssert.Contains(InsertSnippetCommand.MessageFor(InsertOutcome.TerminalNeedsPaste), "terminal");
    }

    [TestMethod]
    public void MessageFor_CopiedLinesUnderAlwaysType_SaysWhy()
    {
        var message = InsertSnippetCommand.MessageFor(InsertOutcome.TerminalCopiedLines);

        StringAssert.Contains(message, "terminal");
        StringAssert.Contains(message, "Always type");
        Assert.AreNotEqual(InsertSnippetCommand.MessageFor(InsertOutcome.TerminalNeedsPaste), message);
    }
}
