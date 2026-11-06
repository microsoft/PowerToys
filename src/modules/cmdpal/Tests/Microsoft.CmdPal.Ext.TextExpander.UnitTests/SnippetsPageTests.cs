// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetsPageTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void GetItems_TextExpanderOff_ShowsThatItIsOffWithAWayToTurnItOn()
    {
        using var environment = new TestEnvironment();
        environment.Folder.WriteSnippets([("!mysig", "Best regards,\nYour name")]);
        var page = new SnippetsPage(environment.Actions);

        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual("Text Expander is off", page.EmptyContent?.Title);
        Assert.IsInstanceOfType<OpenTextExpanderSettingsCommand>(page.EmptyContent?.Command);
    }

    [TestMethod]
    public void GetItems_TextExpanderOffByPolicy_SaysTheOrganizationManagesIt()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        environment.Policies.Values[TextExpanderPolicy.TextExpanderValueName] = 0;
        var page = new SnippetsPage(environment.Actions);

        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual("Text Expander is off", page.EmptyContent?.Title);
        StringAssert.Contains(page.EmptyContent!.Subtitle, "organization");
        Assert.IsInstanceOfType<NoOpCommand>(page.EmptyContent?.Command);
    }

    [TestMethod]
    public void EmptyContent_TextExpanderOff_IsReadyBeforeTheList()
    {
        // Command Palette shows the empty state's command in its command bar only if it has the item when the list comes
        // in. Set while it fetched the list, the item came too late, and Open Text Expander settings didn't show.
        using var environment = new TestEnvironment();
        environment.Folder.WriteSnippets([("!mysig", "Best regards,\nYour name")]);
        var page = new SnippetsPage(environment.Actions);
        var changes = new List<string>();
        page.PropChanged += (_, args) => changes.Add(args.PropertyName);

        var opened = page.EmptyContent;
        Assert.AreEqual("Text Expander is off", opened?.Title);
        Assert.IsInstanceOfType<OpenTextExpanderSettingsCommand>(opened?.Command);

        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreSame(opened, page.EmptyContent);
        CollectionAssert.DoesNotContain(changes, nameof(page.EmptyContent), "Nothing changed, so Command Palette keeps the item it has.");
    }

    [TestMethod]
    public void GetItems_TurnedOffWhileThePageIsOpen_SaysTheEmptyStateChanged()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        var page = new SnippetsPage(environment.Actions);
        Assert.AreEqual(1, page.GetItems().Length);
        var changes = new List<string>();
        page.PropChanged += (_, args) => changes.Add(args.PropertyName);

        environment.Folder.SetEnabled(false);

        Assert.AreEqual(0, page.GetItems().Length);
        CollectionAssert.Contains(changes, nameof(page.EmptyContent));
        Assert.AreEqual("Text Expander is off", page.EmptyContent?.Title);
        Assert.IsInstanceOfType<OpenTextExpanderSettingsCommand>(page.EmptyContent?.Command);
    }

    [TestMethod]
    public void GetItems_Snippets_ShowTheTriggerAndAOneLinePreview()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com"));

        var items = new SnippetsPage(environment.Actions).GetItems();
        string[] triggers = ["!mysig", "!myemail"];

        CollectionAssert.AreEqual(triggers, items.Select(item => item.Title).ToArray());
        Assert.AreEqual("Best regards, Your name", items[0].Subtitle);
        StringAssert.Contains(items[0].Details!.Body, "Best regards,\nYour name");
        Assert.IsInstanceOfType<InsertSnippetCommand>(items[0].Command);
        Assert.IsInstanceOfType<CopySnippetCommand>(items[0].MoreCommands.OfType<CommandContextItem>().Single().Command);
    }

    [TestMethod]
    public void GetItems_SnippetsThatNeedMore_AskOrSayWhy()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"), ("!earlier", "{{clipboard:pick}}"));

        var items = new SnippetsPage(environment.Actions).GetItems();

        Assert.IsInstanceOfType<SnippetAnswersPage>(items[0].Command);
        Assert.AreEqual("Asks questions", items[0].Tags.Single().Text);
        Assert.AreEqual("Hi [Name]", items[0].Subtitle);
        Assert.IsInstanceOfType<TextExpanderOnlyCommand>(items[1].Command);
        Assert.AreEqual("Text Expander only", items[1].Tags.Single().Text);
        Assert.AreEqual(0, items[1].MoreCommands.Length);
    }

    [TestMethod]
    public async Task GetItems_Search_FiltersByTriggerAndText()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com"), ("!addr", "1 Main Street"));
        var page = new SnippetsPage(environment.Actions);
        _ = page.GetItems();

        await UpdatePageAndWaitForItems(page, () => page.SearchText = "example");

        Assert.AreEqual("!myemail", page.GetItems().Single().Title);

        page.SearchText = "nothing like this";
        Assert.AreEqual(0, page.GetItems().Length);
        Assert.AreEqual("No matching snippets", page.EmptyContent?.Title);
    }

    [TestMethod]
    public void GetItems_OpenedOnASearch_StartsFiltered()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com"));

        Assert.AreEqual("!myemail", new SnippetsPage(environment.Actions, "email").GetItems().Single().Title);
    }

    [TestMethod]
    public void GetItems_SnippetsFileChanged_ShowsTheChange()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        var page = new SnippetsPage(environment.Actions);
        Assert.AreEqual(1, page.GetItems().Length);

        environment.Folder.WriteSnippets([("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com")]);
        File.SetLastWriteTimeUtc(environment.Folder.DefaultSnippetsFile, DateTime.UtcNow.AddMinutes(1));

        Assert.AreEqual(2, page.GetItems().Length);
    }

    [TestMethod]
    [DataRow((int)SnippetLibraryStatus.NoSnippetsYet, "No snippets yet", true)]
    [DataRow((int)SnippetLibraryStatus.FileNotFound, "Snippets file not found", true)]
    [DataRow((int)SnippetLibraryStatus.Unreadable, "Couldn't read your snippets file", false)]
    [DataRow((int)SnippetLibraryStatus.NotSnippetsFile, "Couldn't read your snippets file", true)]
    public void EmptyState_SnippetsFileProblem_SaysWhatIsWrong(int status, string title, bool offersSettings)
    {
        var library = new SnippetLibrarySnapshot((SnippetLibraryStatus)status, @"C:\Snippets\mine.json", [], errorLine: 3);
        var empty = EmptyState.For(new SnippetSourceState(TextExpanderAvailability.On, TextExpanderSettings.Default, library));

        Assert.AreEqual(title, empty.Title);
        Assert.AreEqual(offersSettings, empty.OffersSettings);
        if ((SnippetLibraryStatus)status is not SnippetLibraryStatus.NoSnippetsYet)
        {
            StringAssert.Contains(empty.Subtitle, @"C:\Snippets\mine.json");
        }
    }

    [TestMethod]
    public void EmptyState_MistakeInTheFile_GivesTheLine()
    {
        var library = new SnippetLibrarySnapshot(SnippetLibraryStatus.NotSnippetsFile, @"C:\Snippets\mine.json", [], errorLine: 12);

        StringAssert.StartsWith(
            EmptyState.For(new SnippetSourceState(TextExpanderAvailability.On, TextExpanderSettings.Default, library)).Subtitle,
            "Line 12 of");
    }

    [TestMethod]
    public void DetailsBody_TextWithBackticks_UsesALongerFence()
        => Assert.AreEqual("````text\nUse ``` here\n````", SnippetListItem.DetailsBody("Use ``` here"));

    [TestMethod]
    public void DetailsBody_LongRunOfBackticks_GetsItsFenceAtOnce()
    {
        // Every snippet's details are made when the page is, so one with a long run of backticks held up the whole page.
        var run = new string('`', 100_000);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var body = SnippetListItem.DetailsBody("a" + run + "b");
        Assert.IsTrue(clock.ElapsedMilliseconds < 2_000, $"Took {clock.ElapsedMilliseconds} ms.");

        var fence = new string('`', 100_001);
        Assert.IsTrue(body.StartsWith(fence + "text\n", System.StringComparison.Ordinal));
        Assert.IsTrue(body.EndsWith("\n" + fence, System.StringComparison.Ordinal));
        Assert.AreEqual("```text\nNo backticks\n```", SnippetListItem.DetailsBody("No backticks"));
    }

    [TestMethod]
    public void Provider_BuiltIn_ListsTheSnippetsPageAndOffersMatchesInTheMainSearch()
    {
        using var environment = new TestEnvironment();
        var provider = new TextExpanderCommandsProvider(environment.Actions);

        Assert.AreEqual("com.microsoft.cmdpal.builtin.textexpander", provider.Id);
        Assert.AreEqual("Text Expander", provider.DisplayName);
        var item = provider.TopLevelCommands().Single();
        Assert.AreEqual("Text Expander snippets", item.Title);
        Assert.AreEqual(SnippetsPage.PageId, ((SnippetsPage)item.Command).Id);
        Assert.AreEqual(SnippetFallbackItem.FallbackId, ((FallbackCommandItem)provider.FallbackCommands().Single()).Id);
    }
}
