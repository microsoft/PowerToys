// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetInserterTests
{
    private const string Signature = "Best regards,\nYour name";
    private const string UsersCopy = "Something the user copied";

    private readonly FakeClipboard _clipboard = new() { Text = UsersCopy };
    private readonly FakeDesktop _desktop = new();
    private readonly FakeClock _clock = new();

    private SnippetInserter Inserter => new(_clipboard, _desktop, _clock);

    [TestMethod]
    public void Insert_Paste_PastesKeptOutOfHistoryAndPutsTheUsersClipboardBack()
    {
        string? readByApp = null;
        _clock.Tick = now =>
        {
            _clipboard.OpenedBy = now is >= 30 and < 35 ? FakeDesktop.AppProcess : 0;
            readByApp ??= _clipboard.OpenedBy != 0 ? _clipboard.Text : null;
        };

        var outcome = Inserter.Insert(Signature, alwaysType: false);

        Assert.AreEqual(InsertOutcome.Inserted, outcome);
        Assert.AreEqual(1, _desktop.HideRequests);
        Assert.AreEqual((Signature, true), _clipboard.Writes.Single());
        Assert.IsTrue(_desktop.Pasted);
        Assert.AreEqual(Signature, readByApp, "The app must read the snippet, not the user's copy.");
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

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(0, _clipboard.Restores);
        Assert.AreEqual("A newer copy", _clipboard.Text);
    }

    [TestMethod]
    public void Insert_ClipboardThatCannotBePutBack_IsLeftAloneAndTheSnippetIsTyped()
    {
        _clipboard.CanBeSaved = false;

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(0, _clipboard.Writes.Count);
        Assert.AreEqual(UsersCopy, _clipboard.Text);
        Assert.AreEqual(Signature, _desktop.Typed());
        Assert.IsFalse(_desktop.Pasted);
    }

    [TestMethod]
    public void Insert_AlwaysType_NeverTouchesTheClipboardAndPacesTheTyping()
    {
        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: true));

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

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(UsersCopy, _clipboard.Text);
        Assert.AreEqual(Signature, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_CommandPaletteStaysInFront_DoesNothingAndSaysSo()
    {
        _desktop.WindowAfterHide = FakeDesktop.CommandPaletteWindow;

        Assert.AreEqual(InsertOutcome.NoTarget, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _desktop.Sent.Count);
        Assert.IsTrue(_clock.ElapsedMilliseconds >= SnippetInserter.TargetWaitMs);
    }

    [TestMethod]
    public void Insert_AppRunsAsAdministrator_SendsNothing()
    {
        _desktop.CanDriveTarget = false;

        Assert.AreEqual(InsertOutcome.TargetOutOfReach, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _desktop.Sent.Count);
    }

    [TestMethod]
    public void Insert_AnotherWindowComesToTheFrontBeforeThePaste_PutsTheClipboardBackWithoutPasting()
    {
        _clipboard.AfterWrite = () => _desktop.ForegroundWindow = FakeDesktop.OtherWindow;

        Assert.AreEqual(InsertOutcome.TargetChanged, Inserter.Insert(Signature, alwaysType: false));
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

        Assert.AreEqual(InsertOutcome.TargetChanged, Inserter.Insert(Signature, alwaysType: true));
        Assert.IsTrue(_desktop.Typed().Length is > 0 and < 5, _desktop.Typed());
    }

    [TestMethod]
    public void Insert_AppStillBusyAtTheCap_KeepsTheSnippetOnTheClipboardAndSaysSo()
    {
        _desktop.TakingMessages = false;

        Assert.AreEqual(InsertOutcome.ClipboardNotRestored, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(0, _clipboard.Restores);
        Assert.AreEqual(Signature, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_RestoreFailsOnce_IsTriedAgain()
    {
        _clipboard.FailingRestores = 1;

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(2, _clipboard.Restores);
        Assert.AreEqual(UsersCopy, _clipboard.Text);
    }

    [TestMethod]
    public void Insert_RestoreKeepsFailing_SaysTheClipboardWasNotPutBack()
    {
        _clipboard.FailingRestores = SnippetInserter.RestoreAttempts;

        Assert.AreEqual(InsertOutcome.ClipboardNotRestored, Inserter.Insert(Signature, alwaysType: false));
    }

    [TestMethod]
    public void Insert_UserCopiesWhileTheRestoreWaitsToOpenTheClipboard_KeepsTheirNewCopy()
    {
        _clipboard.WhileOpening = () => _clipboard.UserCopies("A newer copy");

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(1, _clipboard.Restores);
        Assert.AreEqual("A newer copy", _clipboard.Text);
    }

    [TestMethod]
    public void Insert_UserCopiesWhileAnotherAppHoldsTheClipboard_KeepsTheirNewCopy()
    {
        _clipboard.UnopenableRestores = 1;
        _clipboard.WhileOpening = () => _clipboard.UserCopies("A newer copy");

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(1, _clipboard.Restores, "The retry mustn't take the newer copy for the extension's own change.");
        Assert.AreEqual("A newer copy", _clipboard.Text);
    }

    [TestMethod]
    public void Insert_ClipboardHeldByAnotherAppForEveryRestore_SaysTheClipboardWasNotPutBack()
    {
        _clipboard.UnopenableRestores = SnippetInserter.RestoreAttempts;

        Assert.AreEqual(InsertOutcome.ClipboardNotRestored, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(SnippetInserter.RestoreAttempts, _clipboard.Restores);
    }

    [TestMethod]
    public void Insert_EmptyClipboard_IsPutBackEmpty()
    {
        _clipboard.Text = null;

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.IsNull(_clipboard.Text);
    }

    [TestMethod]
    public void Insert_ModifierStillHeld_IsLetGoOfInTheSameBatchAsCtrlV()
    {
        _desktop.Held.Add(KeyStrokes.RightControl);

        Assert.AreEqual(InsertOutcome.Inserted, Inserter.Insert(Signature, alwaysType: false));
        Assert.AreEqual(KeyStroke.KeyUp(KeyStrokes.RightControl), _desktop.Sent.Single()[0]);
        Assert.IsTrue(_clock.ElapsedMilliseconds >= SnippetInserter.ModifierReleaseWaitMs);
    }

    [TestMethod]
    public void Insert_EmptySnippet_HidesCommandPaletteAndSendsNothing()
    {
        Assert.AreEqual(InsertOutcome.Empty, Inserter.Insert(string.Empty, alwaysType: false));
        Assert.AreEqual(1, _desktop.HideRequests);
        Assert.AreEqual(0, _clipboard.Saves);
        Assert.AreEqual(0, _desktop.Sent.Count);
    }
}
