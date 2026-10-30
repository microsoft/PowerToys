// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FallbackV2Tests
{
    private sealed partial class StubProvider : CommandProvider
    {
        public override ICommandItem[] TopLevelCommands() => [];
    }

    [TestMethod]
    public void ApiExtensionStubs_IncludeFallbackV2Interfaces()
    {
        var stubs = new StubProvider().GetApiExtensionStubs();

        Assert.IsTrue(stubs.Any(stub => stub is IFallbackCommandItem2 and not IFallbackCommandItem3));
        Assert.IsTrue(stubs.Any(stub => stub is IFallbackCommandItem3));
        Assert.IsTrue(stubs.Any(stub => stub is IFallbackHandler2));
        Assert.IsTrue(stubs.Any(stub => stub is IFallbackCommandResult));
    }

    [TestMethod]
    public void PassiveFallback_UsesPassiveDefaults()
    {
        var fallback = new FallbackPassiveItem("Search", "sample.search")
        {
            Name = "Search",
            TitleTemplate = "Search for {query}",
        };

        fallback.UpdateQuery("test");

        Assert.IsInstanceOfType<IFallbackCommandItem3>(fallback);
        Assert.AreEqual("Search", fallback.Title);
        Assert.AreEqual("Search", fallback.Name);
        Assert.AreEqual("Search", fallback.Command?.Name);
        Assert.IsNull(fallback.QueryHandler);
        Assert.IsNull(((IFallbackCommandItem3)fallback).QueryHandler);
        Assert.AreEqual("Search for {query}", fallback.TitleTemplate);
        Assert.AreSame(fallback.Command, fallback.CreateCommand(new FallbackCommandInvocationArgs("test", "query-id")));
    }

    [TestMethod]
    public void FallbackCommandItem_RemainsLegacyFallback()
    {
        var command = new LegacyFallbackCommand { Name = "Run" };
        var fallback = new FallbackCommandItem(command, "Runner", "sample.run");

        fallback.FallbackHandler!.UpdateQuery("query");

        Assert.IsInstanceOfType<IFallbackCommandItem2>(fallback);
        Assert.IsFalse(fallback is IFallbackCommandItem3);
        Assert.AreSame(command, fallback.Command);
        Assert.AreEqual("query", command.Query);
    }

    [TestMethod]
    public void PassiveFallback_UpdateQueryDoesNotForwardToCommand()
    {
        var command = new LegacyFallbackCommand();
        var fallback = new FallbackPassiveItem(command, "Search", "sample.search");

        ((IFallbackCommandItem)fallback).FallbackHandler.UpdateQuery("query");

        Assert.AreEqual(string.Empty, command.Query);
    }

    [TestMethod]
    public void PassiveFallback_NameFollowsCurrentCommand()
    {
        var command = new NoOpCommand { Name = "Run" };
        var fallback = new FallbackPassiveItem(command, "Runner", "sample.run");
        var nameChanges = 0;
        fallback.PropChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(FallbackPassiveItem.Name))
            {
                nameChanges++;
            }
        };

        command.Name = string.Empty;
        Assert.AreEqual(string.Empty, fallback.Name);

        var replacement = new NoOpCommand { Name = "Open" };
        fallback.Command = replacement;
        Assert.AreEqual("Open", fallback.Name);
        Assert.AreSame(replacement, fallback.CreateCommand(new FallbackCommandInvocationArgs("query", "query-id")));
        Assert.AreEqual(2, nameChanges);
    }

    [TestMethod]
    public void PassiveFallback_ExplicitNameIsKept()
    {
        var command = new NoOpCommand { Name = "Run" };
        var fallback = new FallbackPassiveItem(command, "Runner", "sample.run")
        {
            Name = "Run it",
        };

        Assert.AreEqual("Run it", command.Name);

        fallback.Command = new NoOpCommand { Name = "Open" };
        Assert.AreEqual("Run it", fallback.Name);
    }

    [TestMethod]
    public async Task ResultsFallback_ExposesQueryHandlerAndReturnsSnapshot()
    {
        var fallback = new TestResultsItem();
        var item = (IFallbackCommandItem3)fallback;
        var args = new FallbackQueryArgs("query", "query-id", 5);

        item.FallbackHandler.UpdateQuery("ignored");
        Assert.IsNull(fallback.LastQuery);
        Assert.AreSame(fallback, item.QueryHandler);

        var result = await item.QueryHandler.QueryAsync(args);

        Assert.AreSame(args, fallback.LastQuery);
        Assert.AreEqual(args.Query, result.Query);
        Assert.AreEqual(args.QueryId, result.QueryId);
        Assert.HasCount(1, result.Items);
        Assert.AreEqual("query", result.Items[0].Title);
    }

    [TestMethod]
    public async Task ResultsFallback_QueryAsyncPropagatesCancellation()
    {
        var fallback = new TestResultsItem { WaitForCancellation = true };
        var operation = fallback.QueryHandler.QueryAsync(new FallbackQueryArgs("query", "query-id", 5));
        var task = operation.AsTask();
        await fallback.Started.Task;

        operation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
        Assert.IsTrue(fallback.QueryCancellationToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task QueryResult_TransfersContinuationOnlyOnce()
    {
        var loadCount = 0;
        var first = new FallbackQueryResult(
            "query",
            "query-id",
            [],
            true,
            (requestedItemCount, cancellationToken, progress) =>
            {
                loadCount++;
                return Task.FromResult<IFallbackCommandResult>(
                    new FallbackQueryResult("query", "query-id", []));
            });

        var loaded = await first.LoadMoreItemsAsync(5);
        var repeated = await first.LoadMoreItemsAsync(5);

        Assert.AreEqual(1, loadCount);
        Assert.AreEqual("query-id", loaded.QueryId);
        Assert.IsFalse(repeated.HasMoreItems);
    }

    [TestMethod]
    public async Task QueryResult_FailedLoadMoreReleasesContinuation()
    {
        var closeCount = 0;
        var result = new FallbackQueryResult(
            "query",
            "query-id",
            [],
            true,
            (requestedItemCount, cancellationToken, progress) =>
                Task.FromException<IFallbackCommandResult>(new InvalidOperationException()),
            () => closeCount++);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await result.LoadMoreItemsAsync(5));

        Assert.AreEqual(1, closeCount);
    }

    [TestMethod]
    public void QueryResult_DisposeReleasesUnusedContinuation()
    {
        var closeCount = 0;
        using (var result = new FallbackQueryResult(
            "query",
            "query-id",
            [],
            true,
            (requestedItemCount, cancellationToken, progress) =>
                Task.FromResult<IFallbackCommandResult>(new FallbackQueryResult("query", "query-id", [])),
            () => closeCount++))
        {
        }

        Assert.AreEqual(1, closeCount);
    }

    [TestMethod]
    public async Task QueryResult_NullLoadMoreResultReleasesContinuation()
    {
        var closeCount = 0;
        var result = new FallbackQueryResult(
            "query",
            "query-id",
            [],
            true,
            (requestedItemCount, cancellationToken, progress) => Task.FromResult<IFallbackCommandResult>(null!),
            () => closeCount++);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await result.LoadMoreItemsAsync(5));

        Assert.AreEqual(1, closeCount);
    }

    [TestMethod]
    public async Task QueryResult_DisposeDoesNotCloseTransferredContinuation()
    {
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCanFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closeCount = 0;
        var result = new FallbackQueryResult(
            "query",
            "query-id",
            [],
            true,
            async (requestedItemCount, cancellationToken, progress) =>
            {
                callbackStarted.SetResult();
                await callbackCanFinish.Task;
                return new FallbackQueryResult("query", "query-id", []);
            },
            () => closeCount++);

        var loadTask = Task.Run(async () => await result.LoadMoreItemsAsync(5));
        await callbackStarted.Task;
        result.Dispose();

        Assert.AreEqual(0, closeCount);
        callbackCanFinish.SetResult();
        await loadTask;
        Assert.AreEqual(0, closeCount);
    }

    private sealed partial class LegacyFallbackCommand : NoOpCommand, IFallbackHandler
    {
        public string Query { get; private set; } = string.Empty;

        public void UpdateQuery(string query) => Query = query;
    }

    private sealed partial class TestResultsItem : FallbackResultsItem
    {
        public IFallbackQueryArgs? LastQuery { get; private set; }

        public bool WaitForCancellation { get; init; }

        public CancellationToken QueryCancellationToken { get; private set; }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestResultsItem()
            : base("Results", "sample.results")
        {
        }

        protected async override Task<IFallbackCommandResult> QueryAsync(
            FallbackQueryArgs args,
            CancellationToken cancellationToken,
            IProgress<IFallbackCommandResult> progress)
        {
            LastQuery = args;
            QueryCancellationToken = cancellationToken;
            Started.SetResult();
            if (WaitForCancellation)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new FallbackQueryResult(args.Query, args.QueryId, [new ListItem(new NoOpCommand()) { Title = args.Query }]);
        }
    }
}
