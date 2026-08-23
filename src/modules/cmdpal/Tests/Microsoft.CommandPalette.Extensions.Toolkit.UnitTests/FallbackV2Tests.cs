// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class FallbackV2Tests
{
    [TestMethod]
    public void PassiveFallback_UsesPassiveDefaults()
    {
        var fallback = new PassiveFallbackCommandItem("Search", "sample.search")
        {
            Title = "Search",
            TitleTemplate = "Search for {query}",
        };

        fallback.UpdateQuery("test");

        Assert.AreEqual(FallbackCommandMode.Passive, fallback.Mode);
        Assert.AreEqual("Search", fallback.Title);
        Assert.AreEqual("Search", fallback.Command?.Name);
        Assert.IsNull(fallback.QueryHandler);
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
}
