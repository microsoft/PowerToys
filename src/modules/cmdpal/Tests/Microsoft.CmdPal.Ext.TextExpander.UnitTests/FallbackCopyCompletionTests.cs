// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Insertion;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class FallbackCopyCompletionTests
{
    [TestMethod]
    public void FallbackCopy_PasteFailed_ClearsPromptAnswersOnlyAfterCopySucceeds()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        environment.Desktop.CanDriveTarget = false;
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";

        var result = ((IInvokableCommand)page.Command.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = (ToastArgs)result.Args!;
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
        Assert.AreEqual("Your name", answer.Text, "A failed paste must keep the answer for retrying.");
        Assert.AreEqual(0, environment.Clipboard.Writes.Count);
        environment.Clipboard.AfterWrite = () => Assert.AreEqual("Your name", answer.Text, "The answer is cleared only after the clipboard write succeeds.");

        var copied = ((CopySnippetCommand)toast.Command!).Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, copied.Kind);
        Assert.AreEqual(Resources.copied_toast, ((ToastArgs)copied.Args!).Message);
        Assert.AreEqual(("Hi Your name", false), environment.Clipboard.Writes.Single());
        Assert.AreEqual(string.Empty, answer.Text);
        Assert.AreEqual("Hi [Name]", page.Command.Subtitle);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FallbackCopy_TargetChangedAndClipboardRestoreFailed_KeepsAnswersUntilCopied(bool restoreEmptiesClipboard)
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        environment.Clipboard.Text = "Something the user copied";
        environment.Clipboard.AfterWrite = () => environment.Desktop.ForegroundWindow = FakeDesktop.OtherWindow;
        if (restoreEmptiesClipboard)
        {
            environment.Clipboard.FailingRestores = SnippetInserter.RestoreAttempts;
        }
        else
        {
            environment.Clipboard.UnopenableRestores = SnippetInserter.RestoreAttempts;
        }

        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";

        var result = ((IInvokableCommand)page.Command.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = (ToastArgs)result.Args!;
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
        StringAssert.Contains(toast.Message, Resources.toast_target_changed);
        StringAssert.Contains(toast.Message, Resources.toast_clipboard_not_restored);
        Assert.AreEqual(0, environment.Desktop.Sent.Count, "The focus change prevented every snippet input.");
        Assert.AreEqual("Your name", answer.Text, "A restore failure must not complete the failed insertion.");
        Assert.AreEqual(SnippetInserter.RestoreAttempts, environment.Clipboard.Restores);
        if (restoreEmptiesClipboard)
        {
            Assert.IsNull(environment.Clipboard.Text, "The failed restore left no recoverable snippet on the clipboard.");
        }
        else
        {
            Assert.AreEqual("Hi Your name", environment.Clipboard.Text);
        }

        Assert.IsInstanceOfType<CopySnippetCommand>(toast.Command);
        var copied = ((CopySnippetCommand)toast.Command!).Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, copied.Kind);
        Assert.AreEqual(Resources.copied_toast, ((ToastArgs)copied.Args!).Message);
        Assert.AreEqual(("Hi Your name", false), environment.Clipboard.Writes.Last());
        Assert.AreEqual("Hi Your name", environment.Clipboard.Text);
        Assert.AreEqual(string.Empty, answer.Text);
    }

    [TestMethod]
    public void Paste_SucceededWithClipboardRestoreFailure_CompletesAndWarns()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        environment.Clipboard.Text = "Something the user copied";
        environment.Clipboard.FailingRestores = SnippetInserter.RestoreAttempts;
        var page = new SnippetAnswersPage(environment.SnippetFor("!greet"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";

        var result = ((IInvokableCommand)page.Command.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        var toast = (ToastArgs)result.Args!;
        Assert.AreEqual(Resources.toast_clipboard_not_restored, toast.Message);
        Assert.AreEqual(CommandResultKind.Dismiss, toast.Result!.Kind);
        Assert.IsNull(toast.Command);
        Assert.IsTrue(environment.Desktop.Pasted);
        Assert.IsNull(environment.Clipboard.Text);
        Assert.AreEqual(string.Empty, answer.Text, "A completed paste still clears answers when restoration fails.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void FallbackCopy_PickedSnippetCopyFailed_KeepsPromptAnswersForRetry(bool writeRefused)
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", "Hi {{prompt:Name}}, reply to " + AskingSpots.Email));
        environment.Desktop.CanDriveTarget = false;
        environment.Clipboard.Text = AskingSpots.Copied;
        var page = new SnippetAnswersPage(environment.SnippetFor("!reply"), environment.Actions);
        var answer = page.Parameters.OfType<AnswerRun>().Single();
        answer.Text = "Your name";
        var picks = (SnippetPicksPage)page.Command.Command!;
        var item = picks.GetItems()[1];

        var result = ((IInvokableCommand)item.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual("Your name", answer.Text);
        var copy = (CopySnippetCommand)((ToastArgs)result.Args!).Command!;
        environment.Clipboard.NextWrite = writeRefused ? ClipboardWrite.Refused : ClipboardWrite.Failed;

        var failed = copy.Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, failed.Kind);
        Assert.AreEqual(Resources.copy_failed_toast, ((ToastArgs)failed.Args!).Message);
        Assert.AreEqual(CommandResultKind.KeepOpen, ((ToastArgs)failed.Args!).Result!.Kind);
        Assert.AreEqual("Your name", answer.Text, "A failed fallback copy must keep the answer for retrying.");
        Assert.AreEqual(("Hi Your name, reply to team@example.com", false), environment.Clipboard.Writes.Single());

        environment.Clipboard.NextWrite = ClipboardWrite.Written;
        _ = copy.Invoke();

        Assert.AreEqual("Hi Your name, reply to team@example.com", environment.Clipboard.Text);
        Assert.AreEqual(string.Empty, answer.Text);
    }

    [TestMethod]
    public void FallbackCopy_Retried_CompletesOnceAfterSuccessfulClipboardWrite()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        environment.Desktop.CanDriveTarget = false;
        var completions = 0;
        var command = new InsertSnippetCommand(
            environment.SnippetFor("!greet"),
            environment.Source,
            environment.Inserter,
            answers: () => ["Your name"],
            inserted: () =>
            {
                Assert.AreEqual("Hi Your name", environment.Clipboard.Text);
                completions++;
            });

        var result = command.Invoke();

        Assert.AreEqual(0, completions, "A failed paste must not complete the command.");
        var copy = (CopySnippetCommand)((ToastArgs)result.Args!).Command!;
        environment.Clipboard.NextWrite = ClipboardWrite.Refused;
        _ = copy.Invoke();
        Assert.AreEqual(0, completions, "A refused copy must not complete the command.");

        environment.Clipboard.NextWrite = ClipboardWrite.Failed;
        _ = copy.Invoke();
        Assert.AreEqual(0, completions, "A failed copy must not complete the command.");

        environment.Clipboard.NextWrite = ClipboardWrite.Written;
        environment.Clipboard.AfterWrite = () => Assert.AreEqual(0, completions, "Completion waits for the successful clipboard write to return.");
        _ = copy.Invoke();

        Assert.AreEqual(1, completions);
    }
}
