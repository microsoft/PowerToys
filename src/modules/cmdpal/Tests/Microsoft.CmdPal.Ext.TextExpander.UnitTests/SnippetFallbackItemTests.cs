// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.TextExpander.Commands;
using Microsoft.CmdPal.Ext.TextExpander.Pages;
using Microsoft.CmdPal.Ext.TextExpander.Snippets;
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

    [TestMethod]
    public async Task UpdateQuery_OlderLookupFinishesLast_KeepsTheNewerSnippetAndCommands()
    {
        using var environment = new TestEnvironment();
        environment.UseSnippets(("!a", "Alpha"), ("!ab", "Bravo"));
        var state = environment.Source.GetState();
        using var snippets = new DelayedSnippets(state.Library.Snippets);
        var snapshot = new SnippetLibrarySnapshot(SnippetLibraryStatus.Ready, state.Library.FilePath, snippets);

        // The source clock is fixed; delay the old scan after GetState has released its lock.
        typeof(SnippetSource).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(environment.Source, new SnippetSourceState(state.Availability, state.Settings, snapshot));
        var provider = new TextExpanderCommandsProvider(environment.Actions);
        var fallback = provider.FallbackCommands().Single();
        var item = (ICommandItem)fallback;
        var handler = fallback.FallbackHandler!;
        var older = Task.Run(() => handler.UpdateQuery("!a"));

        try
        {
            Assert.IsTrue(snippets.Started.Wait(TimeSpan.FromSeconds(5)));
            handler.UpdateQuery("!ab");
            Assert.AreEqual("!ab", item.Title);
            snippets.Resume.Set();
            await older.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual("!ab", item.Title);
            var showAll = item.MoreCommands.OfType<CommandContextItem>().Single(command => command.Command is SnippetsPage);
            Assert.AreEqual("!ab", ((SnippetsPage)showAll.Command!).SearchText);
            _ = ((IInvokableCommand)item.Command!).Invoke(null);
            Assert.AreEqual("Bravo", environment.Clipboard.Writes.Single().Text);
        }
        finally
        {
            snippets.Resume.Set();
            await older.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class DelayedSnippets(IReadOnlyList<Snippet> snippets) : IReadOnlyList<Snippet>, IDisposable
    {
        private int _enumerations;

        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim Resume { get; } = new();

        public int Count => snippets.Count;

        public Snippet this[int index] => snippets[index];

        public IEnumerator<Snippet> GetEnumerator()
        {
            if (Interlocked.Increment(ref _enumerations) == 1)
            {
                Started.Set();
                if (!Resume.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("The newer lookup did not finish in time.");
                }
            }

            return snippets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            Started.Dispose();
            Resume.Dispose();
        }
    }
}
