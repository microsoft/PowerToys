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
public sealed class SnippetPicksPageTests
{
    private const string Reply = "Reply to " + AskingSpots.Email;

    [TestMethod]
    public void GetItems_TextExpanderOffByPolicy_DoesntReadTheClipboard()
    {
        // A pick page opened from an item listed before policy turned Text Expander off read the copy and showed its
        // matches; nothing could be pasted, but the clipboard was still read.
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply));
        environment.Clipboard.Text = AskingSpots.Copied;
        var page = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions);
        environment.Policies.Values[TextExpanderPolicy.TextExpanderValueName] = 0;

        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual(0, environment.Clipboard.Reads);
        StringAssert.Contains(page.EmptyContent!.Subtitle, "organization");
    }

    [TestMethod]
    public void GetItems_SeveralMatches_ListsThemToPick()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply));
        environment.Clipboard.Text = AskingSpots.Copied;
        var page = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions);

        var items = page.GetItems();
        string[] matches = ["you@example.com", "team@example.com"];
        string[] previews = ["Reply to you@example.com", "Reply to team@example.com"];

        CollectionAssert.AreEqual(matches, items.Select(item => item.Title).ToArray());
        CollectionAssert.AreEqual(previews, items.Select(item => item.Subtitle).ToArray());
        Assert.AreEqual(Resources.picks_title_email, page.PlaceholderText);
        Assert.AreEqual("!reply", page.Title);
        Assert.AreEqual(1, environment.Clipboard.Reads);
    }

    [TestMethod]
    public void Pick_LastChoice_PastesTheSnippetWithItAndPutsTheCopyBack()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply));
        environment.Clipboard.Text = AskingSpots.Copied;
        var item = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions).GetItems()[1];

        var result = ((IInvokableCommand)item.Command!).Invoke(null);

        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual(("Reply to team@example.com", true), environment.Clipboard.Writes.Single());
        Assert.AreEqual(AskingSpots.Copied, environment.Clipboard.Text);
    }

    [TestMethod]
    public void Pick_EarlierChoice_OpensThePageForTheNextWithTheSameCopy()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!contact", "Mail " + AskingSpots.Email + ", call " + AskingSpots.Phone));
        environment.Clipboard.Text = AskingSpots.Copied;
        var first = new SnippetPicksPage(environment.SnippetFor("!contact"), environment.Actions).GetItems();

        var next = (SnippetPicksPage)first[1].Command!;
        environment.Clipboard.Text = "Copied later: other@example.com";
        var phones = next.GetItems();
        _ = ((IInvokableCommand)phones[0].Command!).Invoke(null);
        string[] phoneNumbers = ["555-0123", "555-0199"];

        Assert.AreEqual("Mail team@example.com, call [Phone number from clipboard]", first[1].Subtitle);
        Assert.AreEqual(Resources.picks_title_phone, next.PlaceholderText);
        CollectionAssert.AreEqual(phoneNumbers, phones.Select(item => item.Title).ToArray());
        Assert.AreEqual("Mail team@example.com, call 555-0123", environment.Clipboard.Writes.Single().Text);
        Assert.AreEqual(1, environment.Clipboard.Reads, "Later pages use the copy the first page read.");
    }

    [TestMethod]
    public void GetItems_OneMatch_ShowsWhatGoesInAndEnterPastesIt()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply));
        environment.Clipboard.Text = "From you@example.com";
        var page = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions);

        var item = page.GetItems().Single();
        var result = ((IInvokableCommand)item.Command!).Invoke(null);

        Assert.AreEqual("Reply to you@example.com", item.Title);
        Assert.AreEqual(string.Empty, item.Subtitle);
        Assert.AreEqual(Resources.picks_nothing_to_pick, page.PlaceholderText);
        Assert.AreEqual(CommandResultKind.Dismiss, result.Kind);
        Assert.AreEqual("Reply to you@example.com", environment.Clipboard.Writes.Single().Text);
    }

    [TestMethod]
    public void GetItems_NoMatch_PastesTheRestAndSaysThatSpotWasLeftEmpty()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply + ". Thanks!"));
        environment.Clipboard.Text = "Nothing like an address here";

        var item = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions).GetItems().Single();
        var result = ((IInvokableCommand)item.Command!).Invoke(null);

        Assert.AreEqual("Reply to . Thanks!", item.Title);
        Assert.AreEqual(Resources.toast_missing_email, item.Subtitle);
        Assert.AreEqual(CommandResultKind.ShowToast, result.Kind);
        Assert.AreEqual(Resources.toast_missing_email, ((ToastArgs)result.Args!).Message);
        Assert.AreEqual(CommandResultKind.Dismiss, ((ToastArgs)result.Args!).Result!.Kind);
        Assert.AreEqual("Reply to . Thanks!", environment.Clipboard.Writes.Single().Text);
    }

    [TestMethod]
    public void GetItems_NothingToPasteAtAll_SaysSoInsteadOfAnEmptyTitle()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!email", AskingSpots.Email));
        environment.Clipboard.Text = null;

        var item = new SnippetPicksPage(environment.SnippetFor("!email"), environment.Actions).GetItems().Single();

        Assert.AreEqual(Resources.picks_comes_out_empty, item.Title);
        Assert.AreEqual(Resources.toast_missing_email, item.Subtitle);
    }

    [TestMethod]
    public void GetItems_OpenedAgain_ReadsTheClipboardAgain()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply));
        environment.Clipboard.Text = "From you@example.com";
        var page = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions);
        _ = page.GetItems();

        environment.Clipboard.Text = AskingSpots.Copied;
        var items = page.GetItems();

        Assert.AreEqual(2, items.Length);
        Assert.AreEqual(2, environment.Clipboard.Reads);
    }

    [TestMethod]
    public void Copy_Picked_CopiesTheSnippetWithThePick()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!reply", Reply));
        environment.Clipboard.Text = AskingSpots.Copied;
        var item = new SnippetPicksPage(environment.SnippetFor("!reply"), environment.Actions).GetItems()[1];

        var copy = (CopySnippetCommand)((CommandContextItem)item.MoreCommands.Single()).Command!;
        _ = copy.Invoke();

        Assert.AreEqual(("Reply to team@example.com", false), environment.Clipboard.Writes.Single());
    }
}
