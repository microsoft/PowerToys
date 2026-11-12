// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.PowerScripts.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerScripts.Client;

namespace Microsoft.CmdPal.Ext.PowerScripts.UnitTests;

[TestClass]
public sealed class PowerScriptsFallbackTests
{
    [TestMethod]
    [Timeout(5000)]
    public async Task QueriesMatchNamesAndDescriptionsWithoutChangingTheListPageSearch()
    {
        var service = new FakePowerScriptsService
        {
            Catalog = new(
            [
                new PowerScriptInfo { Id = "beta", Name = "Beta", Description = "Clean the downloads folder" },
                new PowerScriptInfo { Id = "alpha", Name = "Alpha", Description = "Unrelated action" },
            ],
            true),
        };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });
        var page = (PowerScriptsListPage)provider.TopLevelCommands().Single().Command;
        page.SearchText = "alpha";
        var handler = GetHandler(provider);

        var byDescription = await handler.QueryAsync(new FallbackQueryArgs("DOWNLOADS", "first", 5));
        var byName = await handler.QueryAsync(new FallbackQueryArgs("ALPHA", "second", 5));
        var noMatch = await handler.QueryAsync(new FallbackQueryArgs("missing", "third", 5));

        Assert.AreEqual("Beta", byDescription.Items.Single().Title);
        Assert.AreEqual("Alpha", byName.Items.Single().Title);
        Assert.AreEqual("alpha", page.SearchText);
        Assert.AreSame(byName.Items.Single().Command, page.GetItems().Single().Command);
        Assert.IsEmpty(noMatch.Items);
        Assert.IsFalse(noMatch.HasMoreItems);
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [DataRow("", 5u)]
    [DataRow("   ", 5u)]
    [DataRow("script", 0u)]
    public async Task EmptyQueriesAndZeroBudgetsDoNotStartDiscovery(string query, uint count)
    {
        var service = new FakePowerScriptsService();
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });

        var result = await GetHandler(provider).QueryAsync(new FallbackQueryArgs(query, "query-id", count));

        Assert.IsEmpty(result.Items);
        Assert.AreEqual(query, result.Query);
        Assert.AreEqual("query-id", result.QueryId);
        Assert.IsFalse(result.HasMoreItems);
        Assert.AreEqual(0, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task PagingReturnsIndependentCumulativeSnapshotsAndAcceptsLargeBudgets()
    {
        var service = new FakePowerScriptsService { Catalog = MatchingScripts(6) };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });
        var first = await GetHandler(provider).QueryAsync(new FallbackQueryArgs("match", "query-id", 2));

        var second = await first.LoadMoreItemsAsync(2);
        var last = await second.LoadMoreItemsAsync(uint.MaxValue);

        Assert.HasCount(2, first.Items);
        Assert.HasCount(4, second.Items);
        Assert.HasCount(6, last.Items);
        Assert.IsTrue(first.HasMoreItems);
        Assert.IsFalse(last.HasMoreItems);
        CollectionAssert.AreEqual(first.Items.Select(item => item.Command.Id).ToArray(), last.Items.Take(2).Select(item => item.Command.Id).ToArray());
        Assert.AreSame(first.Items[0].Command, last.Items[0].Command);
        Assert.AreEqual("match", last.Query);
        Assert.AreEqual("query-id", last.QueryId);
        var all = await GetHandler(provider).QueryAsync(new FallbackQueryArgs("match", "all", uint.MaxValue));
        Assert.HasCount(6, all.Items);
        Assert.IsFalse(all.HasMoreItems);
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ZeroLoadMoreBudgetPreservesTheContinuation()
    {
        var service = new FakePowerScriptsService { Catalog = MatchingScripts(3) };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });
        var first = await GetHandler(provider).QueryAsync(new FallbackQueryArgs("match", "query-id", 1));

        var unchanged = await first.LoadMoreItemsAsync(0);
        var last = await unchanged.LoadMoreItemsAsync(uint.MaxValue);

        Assert.HasCount(1, unchanged.Items);
        Assert.IsTrue(unchanged.HasMoreItems);
        Assert.HasCount(3, last.Items);
        Assert.IsFalse(last.HasMoreItems);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task DisabledCatalogDoesNotExposeScripts()
    {
        var service = new FakePowerScriptsService { Catalog = new([TestScripts.Create()], false, "integration-disabled") };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });

        var result = await GetHandler(provider).QueryAsync(new FallbackQueryArgs("script", "query-id", uint.MaxValue));

        Assert.IsEmpty(result.Items);
        Assert.IsFalse(result.HasMoreItems);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task CancelledQueryDoesNotCancelSharedDiscoveryOrAnotherQuery()
    {
        var discovery = new TaskCompletionSource<PowerScriptsCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken discoveryToken = default;
        var service = new FakePowerScriptsService
        {
            DiscoverHandler = token =>
            {
                discoveryToken = token;
                started.TrySetResult();
                return discovery.Task.WaitAsync(token);
            },
        };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });
        using var cancellation = new CancellationTokenSource();
        var handler = GetHandler(provider);
        var first = handler.QueryAsync(new FallbackQueryArgs("script", "old", 5)).AsTask(cancellation.Token);
        await started.Task;
        var second = handler.QueryAsync(new FallbackQueryArgs("script", "current", 5)).AsTask();

        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await first);
        Assert.IsFalse(discoveryToken.IsCancellationRequested);
        discovery.SetResult(new([TestScripts.Create()], true));
        var result = await second;

        Assert.AreEqual("current", result.QueryId);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual(1, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5000)]
    public async Task ParameterizedResultsOpenTheExistingFormWithoutRunning(bool required)
    {
        var service = new FakePowerScriptsService
        {
            Catalog = new([TestScripts.Create(TestScripts.Parameter("value", required: required))], true),
        };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });

        var result = await GetHandler(provider).QueryAsync(new FallbackQueryArgs("script", "query-id", 5));
        var page = (IContentPage)result.Items.Single().Command;
        page.GetContent();
        page.GetContent();

        Assert.HasCount(1, result.Items);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ClosingAResultAndCancellingSearchDoNotStopAnExplicitRun()
    {
        var completed = new TaskCompletionSource<PowerScriptTransformResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new FakePowerScriptsService
        {
            Catalog = MatchingScripts(2),
            RunHandler = token => completed.Task.WaitAsync(token),
        };
        IPage? destination = null;
        using var provider = new PowerScriptsCommandsProvider(service, target => destination = target);
        using var cancellation = new CancellationTokenSource();
        var result = await GetHandler(provider).QueryAsync(new FallbackQueryArgs("match", "query-id", 1)).AsTask(cancellation.Token);
        var command = (RunPowerScriptCommand)result.Items.Single().Command;

        ((IDisposable)result).Dispose();
        command.Invoke();
        await service.RunStarted;
        cancellation.Cancel();

        Assert.IsNotNull(destination);
        Assert.IsFalse(service.RunCancellationToken.IsCancellationRequested);
        Assert.AreEqual(1, service.RunCalls);
        var page = (PowerScriptPage)destination;
        page.GetContent();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void ItemsChanged(object sender, IItemsChangedEventArgs args)
        {
            if (!page.IsLoading)
            {
                finished.TrySetResult();
            }
        }

        page.ItemsChanged += ItemsChanged;
        try
        {
            completed.SetResult(new PowerScriptTransformResult { Text = "finished" });
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(4));
        }
        finally
        {
            page.ItemsChanged -= ItemsChanged;
        }
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task PagingKeepsItsCatalogAndQueryAcrossOtherSearchesAndRefreshes()
    {
        var catalog = MatchingScripts(6);
        var service = new FakePowerScriptsService { Catalog = catalog };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });
        var handler = GetHandler(provider);
        var first = await handler.QueryAsync(new FallbackQueryArgs("match", "original", 2));
        var other = await handler.QueryAsync(new FallbackQueryArgs("Match 5", "other", 5));
        Assert.AreEqual("Match 5", other.Items.Single().Title);

        service.Catalog = new([.. catalog.Scripts, new PowerScriptInfo { Id = "new", Name = "Match new" }], true);
        await RefreshAsync(provider, first.Items[0]);
        var last = await first.LoadMoreItemsAsync(uint.MaxValue);

        Assert.HasCount(2, first.Items);
        Assert.HasCount(6, last.Items);
        Assert.AreEqual("match", last.Query);
        Assert.AreEqual("original", last.QueryId);
        Assert.IsFalse(last.Items.Any(item => item.Title == "Match new"));
        Assert.IsFalse(last.HasMoreItems);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RefreshNotifiesTheProviderAndChangesResultsForTheSameQuery()
    {
        var service = new FakePowerScriptsService { Catalog = MatchingScripts(1) };
        using var provider = new PowerScriptsCommandsProvider(service, static _ => { });
        var notificationCount = 0;
        provider.ItemsChanged += (_, _) => Interlocked.Increment(ref notificationCount);
        var handler = GetHandler(provider);
        var first = await handler.QueryAsync(new FallbackQueryArgs("match", "first", 5));
        Assert.AreEqual(0, notificationCount);

        service.Catalog = new([new PowerScriptInfo { Id = "new", Name = "Match new" }], true);
        await RefreshAsync(provider, first.Items.Single());
        var second = await handler.QueryAsync(new FallbackQueryArgs("match", "second", 5));

        Assert.AreEqual("Match new", second.Items.Single().Title);
        Assert.AreEqual(1, notificationCount);
        Assert.AreEqual(2, service.DiscoverCalls);
        Assert.AreEqual(0, service.RunCalls);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task RefreshMakesAnOldParameterlessResultOpenTheUpdatedForm()
    {
        var service = new FakePowerScriptsService { Catalog = new([TestScripts.Create()], true) };
        IPage? destination = null;
        using var provider = new PowerScriptsCommandsProvider(service, target => destination = target);
        var handler = GetHandler(provider);
        var first = await handler.QueryAsync(new FallbackQueryArgs("script", "first", 5));
        var oldAction = (RunPowerScriptCommand)first.Items.Single().Command;
        service.Catalog = new([TestScripts.Create(TestScripts.Parameter("value", required: true))], true);

        await RefreshAsync(provider, first.Items.Single());
        var second = await handler.QueryAsync(new FallbackQueryArgs("script", "second", 5));
        oldAction.Invoke();

        Assert.IsInstanceOfType<IContentPage>(second.Items.Single().Command);
        Assert.AreSame(second.Items.Single().Command, destination);
        Assert.AreEqual(0, service.RunCalls);
    }

    private static IFallbackQueryHandler GetHandler(PowerScriptsCommandsProvider provider)
        => ((IFallbackCommandItem3)provider.FallbackCommands().Single()).QueryHandler;

    private static PowerScriptsCatalog MatchingScripts(int count)
    {
        var scripts = Enumerable.Range(0, count).Select(index => new PowerScriptInfo
        {
            Id = $"script-{index}",
            Name = $"Match {index}",
        }).ToArray();
        return new(scripts, true);
    }

    private static async Task RefreshAsync(PowerScriptsCommandsProvider provider, IListItem item)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void ItemsChanged(object sender, IItemsChangedEventArgs args) => completed.TrySetResult();

        provider.ItemsChanged += ItemsChanged;
        try
        {
            var refresh = (IInvokableCommand)item.MoreCommands.OfType<ICommandContextItem>().Single(context => context.Title == Resources.Refresh).Command;
            refresh.Invoke(item);
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(4));
        }
        finally
        {
            provider.ItemsChanged -= ItemsChanged;
        }
    }
}
