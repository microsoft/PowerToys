// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TextExpander.UnitTests;

[TestClass]
public sealed class SnippetFallbackItemTests
{
    [TestMethod]
    public void UpdateQuery_TriggerMatches_OffersTheSnippetAndAllMatches()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"), ("!myemail", "you@example.com"));
        var fallback = new SnippetFallbackItem(environment.Actions);

        fallback.UpdateQuery("mysig");

        Assert.AreEqual("!mysig", fallback.Title);
        Assert.AreEqual("Best regards, Your name", fallback.Subtitle);
        Assert.IsInstanceOfType<InsertSnippetCommand>(fallback.Command);
        var showAll = fallback.MoreCommands.OfType<CommandContextItem>().Single(item => item.Command is SnippetsPage);
        Assert.AreEqual("Show all matching snippets", showAll.Title);
        Assert.AreEqual("mysig", ((SnippetsPage)showAll.Command!).SearchText);
    }

    [TestMethod]
    public void UpdateQuery_NoTriggerMatches_ShowsNothing()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!mysig", "Best regards,\nYour name"));
        var fallback = new SnippetFallbackItem(environment.Actions);

        fallback.UpdateQuery("mysig");
        Assert.AreEqual("!mysig", fallback.Title);

        // The text matches, but a search for other things shouldn't turn up snippets that mention them.
        fallback.UpdateQuery("regards");
        Assert.AreEqual(string.Empty, fallback.Title);
        Assert.IsInstanceOfType<NoOpCommand>(fallback.Command);

        fallback.UpdateQuery(string.Empty);
        Assert.AreEqual(string.Empty, fallback.Title);
    }

    [TestMethod]
    public void UpdateQuery_TextExpanderOff_ShowsNothing()
    {
        using var environment = new TestEnvironment();
        environment.Folder.WriteSnippets([("!mysig", "Best regards,\nYour name")]);
        var fallback = new SnippetFallbackItem(environment.Actions);

        fallback.UpdateQuery("mysig");

        Assert.AreEqual(string.Empty, fallback.Title);
    }

    [TestMethod]
    public void UpdateQuery_SnippetWithQuestions_OpensItsQuestions()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!greet", "Hi {{prompt:Name}}"));
        var fallback = new SnippetFallbackItem(environment.Actions);

        fallback.UpdateQuery("greet");

        Assert.IsInstanceOfType<SnippetAnswersPage>(fallback.Command);
    }
}
