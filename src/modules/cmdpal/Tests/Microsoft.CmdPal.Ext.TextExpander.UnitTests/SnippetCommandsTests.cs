// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetCommandsTests
{
    [TestMethod]
    public void Insert_TextExpanderOff_SaysSoAndKeepsCommandPaletteOpen()
    {
        using var environment = new TestEnvironment();
        var command = new InsertSnippetCommand(new Snippet("!mysig", "Best regards,\nYour name"), environment.Source, environment.Inserter);

        var result = command.Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(CommandResultKind.KeepOpen, ((ToastArgs)result.Args!).Result!.Kind);
        Assert.AreEqual(0, environment.Desktop.HideRequests);
        Assert.AreEqual(0, environment.Clipboard.Writes.Count);
    }

    [TestMethod]
    public void Insert_ClipboardSpot_UsesTheCopyFromBeforeThePasteAndPutsItBack()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!quote", "Quote: {{clipboard}}"));
        environment.Clipboard.Text = "copied text";

        var result = new InsertSnippetCommand(environment.SnippetFor("!quote"), environment.Source, environment.Inserter).Invoke();

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual(("Quote: copied text", true), environment.Clipboard.Writes.Single());
        Assert.AreEqual("copied text", environment.Clipboard.Text);
    }

    [TestMethod]
    public void Insert_AppOutOfReach_OffersToCopyTheSnippetInstead()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!myemail", "you@example.com"));
        environment.Desktop.CanDriveTarget = false;

        var result = new InsertSnippetCommand(environment.SnippetFor("!myemail"), environment.Source, environment.Inserter).Invoke();

        var toast = (ToastArgs)result.Args!;
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(InsertSnippetCommand.MessageFor(Insertion.InsertOutcome.TargetOutOfReach), toast.Message);
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);

        _ = ((CopySnippetCommand)toast.Command!).Invoke();
        Assert.AreEqual(("you@example.com", false), environment.Clipboard.Writes.Single());
    }

    [TestMethod]
    public void Copy_Lines_HaveCrLfLineBreaksOnTheClipboard()
    {
        // As the clipboard has text: the console ran pasted lines with a lone LF between them as one.
        using var environment = new TestEnvironment();
        var command = new CopySnippetCommand(() => "cd ~/project\nls", environment.Clipboard);

        _ = command.Invoke();

        Assert.AreEqual(("cd ~/project\r\nls", false), environment.Clipboard.Writes.Single());
    }

    [TestMethod]
    public void Insert_TextExpanderSetToAlwaysType_TypesWithoutTouchingTheClipboard()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!myemail", "you@example.com"));
        environment.Folder.SetModuleSettings(injectionBackend: "type");

        var result = new InsertSnippetCommand(environment.SnippetFor("!myemail"), environment.Source, environment.Inserter).Invoke();

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual(0, environment.Clipboard.Saves);
        Assert.AreEqual("you@example.com", environment.Desktop.Typed());
    }

    [TestMethod]
    public void Insert_AlwaysTypeLinesWithCopiedTextIntoATerminal_OffersToCopyTheSnippetInstead()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!quote", "Quote:\n{{clipboard}}"), ("!steps", "cd ~/project\nls"));
        environment.Folder.SetModuleSettings(injectionBackend: "type");
        environment.Desktop.AppIsWindowsTerminal = true;
        environment.Clipboard.Text = "copied text";

        var result = new InsertSnippetCommand(environment.SnippetFor("!quote"), environment.Source, environment.Inserter).Invoke();

        var toast = (ToastArgs)result.Args!;
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(InsertSnippetCommand.MessageFor(Insertion.InsertOutcome.TerminalCopiedLines), toast.Message);
        Assert.AreEqual(CommandResultKind.KeepOpen, toast.Result!.Kind);
        Assert.AreEqual(string.Empty, environment.Desktop.Typed());
        Assert.AreEqual(0, environment.Clipboard.Writes.Count);

        // Lines the user wrote are typed, as Always type says.
        result = new InsertSnippetCommand(environment.SnippetFor("!steps"), environment.Source, environment.Inserter).Invoke();

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual("cd ~/project\nls", environment.Desktop.Typed());
    }

    [TestMethod]
    public void Copy_TextExpanderOff_CopiesNothing()
    {
        using var environment = new TestEnvironment();

        var result = environment.Actions.CopyCommand(new Snippet("!myemail", "you@example.com"), answers: null).Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(0, environment.Clipboard.Writes.Count);
    }

    [TestMethod]
    public void TextExpanderOnly_Invoked_ExplainsAndKeepsCommandPaletteOpen()
    {
        var result = new TextExpanderOnlyCommand().Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(CommandResultKind.KeepOpen, ((ToastArgs)result.Args!).Result!.Kind);
    }

    [TestMethod]
    public void PrimaryCommand_SnippetTakingPartOfTheCopy_OpensItsPickPageWithoutReadingTheClipboard()
    {
        using var environment = new TestEnvironment();
        var snippet = new Snippet("!reply", "Reply to " + AskingSpots.Email);

        var command = environment.Actions.PrimaryCommand(snippet);

        Assert.IsInstanceOfType<SnippetPicksPage>(command);
        Assert.AreEqual(0, environment.Actions.MoreCommands(snippet).Length);
        Assert.AreEqual(0, environment.Clipboard.Reads, "Listing a snippet never reads the clipboard.");
    }

    [TestMethod]
    public void Insert_SpotLeftEmptyAndClipboardNotPutBack_WarnsAboutTheClipboard()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", "Reply to " + AskingSpots.Email));
        environment.Clipboard.Text = "Nothing like an address here";
        environment.Clipboard.FailingRestores = Insertion.SnippetInserter.RestoreAttempts;
        var item = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions).GetItems().Single();

        var result = ((IInvokableCommand)item.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(Resources.toast_clipboard_not_restored, ((ToastArgs)result.Args!).Message);
    }

    [TestMethod]
    public void Insert_WholeCopyWithNoTextOnTheClipboard_PastesTheRestAndSaysSo()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!quote", "Quote: {{clipboard}}"));
        environment.Clipboard.Text = null;

        var result = new InsertSnippetCommand(environment.SnippetFor("!quote"), environment.Source, environment.Inserter).Invoke();

        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(Resources.toast_missing_clipboard, ((ToastArgs)result.Args!).Message);
        Assert.AreEqual(CommandResultKind.Dismiss, ((ToastArgs)result.Args!).Result!.Kind);
        Assert.AreEqual("Quote: ", environment.Clipboard.Writes.Single().Text);
    }

    [TestMethod]
    [DataRow("\"C:\\Program Files\\PowerToys\\PowerToys.exe\" \"%1\"", "C:\\Program Files\\PowerToys\\PowerToys.exe")]
    [DataRow("C:\\Tools\\PowerToys\\PowerToys.exe %1", "C:\\Tools\\PowerToys\\PowerToys.exe")]
    [DataRow("\"C:\\Program Files\\Other\\Other.exe\" \"%1\"", null)]
    [DataRow("PowerToys.exe \"%1\"", null)]
    [DataRow("..\\PowerToys\\PowerToys.exe", null)]
    [DataRow("", null)]
    public void PowerToysFromProtocolCommand_RegisteredCommand_FindsPowerToysExe(string command, string? expected)
        => Assert.AreEqual(expected, OpenTextExpanderSettingsCommand.PowerToysFromProtocolCommand(command));

    [TestMethod]
    public void HivesFor_Elevated_TrustsOnlyTheMachinesRegistration()
    {
        // Anything running as the user can write HKCU, and an elevated Command Palette would start its program elevated.
        var elevated = OpenTextExpanderSettingsCommand.HivesFor(elevated: true);
        Assert.AreEqual(1, elevated.Length);
        Assert.AreEqual(Microsoft.Win32.RegistryHive.LocalMachine, elevated[0]);

        var user = OpenTextExpanderSettingsCommand.HivesFor(elevated: false);
        Assert.AreEqual(2, user.Length);
        Assert.AreEqual(Microsoft.Win32.RegistryHive.CurrentUser, user[0]);
        Assert.AreEqual(Microsoft.Win32.RegistryHive.LocalMachine, user[1]);
    }
}
