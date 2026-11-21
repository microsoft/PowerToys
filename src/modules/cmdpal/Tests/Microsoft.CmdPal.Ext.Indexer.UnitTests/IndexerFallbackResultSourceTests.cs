// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public class IndexerFallbackResultSourceTests
{
    private static readonly int[] InitialFetchCounts = [2, 1];
    private static readonly int[] CappedFetchCounts = [2, 48, 50];
    private static readonly int[] SingleFetchCount = [1];

    [TestMethod]
    public async Task QueryReturnsIndependentSnapshotsAndTransfersContinuationOwnership()
    {
        var engine = new TestSearchEngine(6);
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "query-id", 3), engine);
        var progress = new RecordingProgress();

        var first = await session.QueryAsync(CancellationToken.None, progress);

        Assert.HasCount(3, first.Items);
        Assert.HasCount(1, progress.Results);
        Assert.HasCount(2, progress.Results[0].Items);
        Assert.IsFalse(progress.Results[0].HasMoreItems);
        Assert.AreEqual("query-id", first.QueryId);
        CollectionAssert.AreEqual(InitialFetchCounts, engine.FetchCounts);

        var second = await first.LoadMoreItemsAsync(2);
        ((IDisposable)first).Dispose();
        Assert.AreEqual(0, engine.DisposeCount);
        Assert.HasCount(3, first.Items);
        Assert.HasCount(5, second.Items);

        var last = await second.LoadMoreItemsAsync(2);
        Assert.HasCount(6, last.Items);
        Assert.HasCount(2, progress.Results[0].Items);
        Assert.IsFalse(last.HasMoreItems);
        Assert.AreEqual(1, engine.DisposeCount);
        session.Dispose();
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task ClosingUnusedContinuationReleasesSearchEngine()
    {
        var engine = new TestSearchEngine(6);
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", 1), engine);
        var result = await session.QueryAsync(CancellationToken.None, new RecordingProgress());

        ((IDisposable)result).Dispose();
        ((IDisposable)result).Dispose();

        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task FailedContinuationReleasesSearchEngine()
    {
        var engine = new TestSearchEngine(6);
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", 1), engine);
        var result = await session.QueryAsync(CancellationToken.None, new RecordingProgress());
        engine.OnFetch = () => throw new InvalidOperationException("Search failed.");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await result.LoadMoreItemsAsync(1));

        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task CancellationDiscardsResultsAndReleasesSearchEngine()
    {
        using var cancellation = new CancellationTokenSource();
        var engine = new TestSearchEngine(6) { OnFetch = cancellation.Cancel };
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", 3), engine);
        var progress = new RecordingProgress();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => session.QueryAsync(cancellation.Token, progress));

        Assert.IsEmpty(progress.Results);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task ZeroBudgetDoesNotStartSearch()
    {
        var engine = new TestSearchEngine(6);
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", 0), engine);

        var result = await session.QueryAsync(CancellationToken.None, new RecordingProgress());

        Assert.IsEmpty(result.Items);
        Assert.AreEqual(0, engine.QueryCount);
        Assert.IsEmpty(engine.FetchCounts);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task LargeUnsignedBudgetsAreCappedWithoutOverflow()
    {
        var engine = new TestSearchEngine(100);
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", uint.MaxValue), engine);

        var first = await session.QueryAsync(CancellationToken.None, new RecordingProgress());
        var last = await first.LoadMoreItemsAsync(uint.MaxValue);

        Assert.HasCount(50, first.Items);
        Assert.HasCount(100, last.Items);
        CollectionAssert.AreEqual(CappedFetchCounts, engine.FetchCounts);
        Assert.IsFalse(last.HasMoreItems);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task ZeroLoadMoreBudgetPreservesContinuation()
    {
        var engine = new TestSearchEngine(6);
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", 1), engine);
        var first = await session.QueryAsync(CancellationToken.None, new RecordingProgress());

        var unchanged = await first.LoadMoreItemsAsync(0);

        Assert.HasCount(1, unchanged.Items);
        Assert.IsTrue(unchanged.HasMoreItems);
        Assert.AreEqual(0, engine.DisposeCount);
        CollectionAssert.AreEqual(SingleFetchCount, engine.FetchCounts);
        ((IDisposable)unchanged).Dispose();
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task NoticeCommandPreservesSearchQuery()
    {
        var engine = new TestSearchEngine(0)
        {
            Notice = new SearchNoticeInfo("Search unavailable", "Open search settings"),
        };
        using var session = new IndexerFallbackSearchSession(new FallbackQueryArgs("query", "id", 3), engine);

        var result = await session.QueryAsync(CancellationToken.None, new RecordingProgress());
        using var page = (IndexerPage)result.Items.Single().Command;

        Assert.AreEqual("query", page.SearchText);
        Assert.AreEqual("Search unavailable", result.Items[0].Subtitle);
        Assert.IsFalse(result.HasMoreItems);
    }

    [TestMethod]
    public async Task DirectPathHasStableCommandId()
    {
        var source = new IndexerFallbackResultSource();

        var fallback = (IFallbackCommandItem3)source;
        Assert.AreSame(source, fallback.QueryHandler);
        var first = await fallback.QueryHandler.QueryAsync(new FallbackQueryArgs(Environment.SystemDirectory, "first", 1));
        var second = await fallback.QueryHandler.QueryAsync(new FallbackQueryArgs(Environment.SystemDirectory, "second", 1));

        Assert.HasCount(1, first.Items);
        Assert.AreEqual($"com.microsoft.cmdpal.builtin.indexer.open:{Environment.SystemDirectory}", first.Items[0].Command.Id);
        Assert.AreEqual(first.Items[0].Command.Id, second.Items[0].Command.Id);
        Assert.AreNotSame(first.Items[0], second.Items[0]);
        Assert.AreEqual("first", first.QueryId);
        Assert.AreEqual("second", second.QueryId);
    }

    [TestMethod]
    public async Task SuppressionAndZeroBudgetDoNotReturnDirectPaths()
    {
        var source = new IndexerFallbackResultSource();
        var handler = ((IFallbackCommandItem3)source).QueryHandler;
        var empty = await handler.QueryAsync(new FallbackQueryArgs(Environment.SystemDirectory, "empty", 0));
        source.SuppressFallbackWhen(_ => true);

        var suppressed = await handler.QueryAsync(new FallbackQueryArgs(Environment.SystemDirectory, "suppressed", 1));

        Assert.IsEmpty(empty.Items);
        Assert.IsEmpty(suppressed.Items);
        Assert.AreEqual("suppressed", suppressed.QueryId);
        Assert.IsInstanceOfType<FallbackResultsItem>(source);
        Assert.AreSame(source, ((IFallbackCommandItem3)source).QueryHandler);
        Assert.AreEqual("com.microsoft.cmdpal.builtin.indexer.fallback", source.Id);
    }

    private sealed class RecordingProgress : IProgress<IFallbackCommandResult>
    {
        public List<IFallbackCommandResult> Results { get; } = [];

        public void Report(IFallbackCommandResult value) => Results.Add(value);
    }

    private sealed class TestSearchEngine(int totalCount) : IIndexerFallbackSearchEngine
    {
        private int _offset;

        public int QueryCount { get; private set; }

        public int DisposeCount { get; private set; }

        public List<int> FetchCounts { get; } = [];

        public Action OnFetch { get; set; }

        public SearchNoticeInfo? Notice { get; set; }

        public SearchNoticeInfo? Query(string query, uint queryCookie)
        {
            QueryCount++;
            return Notice;
        }

        public IList<IListItem> FetchNextItems(int limit, uint queryCookie, out bool hasMore, out SearchNoticeInfo? notice)
        {
            FetchCounts.Add(limit);
            OnFetch?.Invoke();
            var count = Math.Min(limit, totalCount - _offset);
            var items = Enumerable.Range(_offset, count)
                .Select(index => (IListItem)new ListItem(new NoOpCommand { Id = $"item-{index}" }))
                .ToArray();
            _offset += count;
            hasMore = _offset < totalCount;
            notice = Notice;
            return items;
        }

        public void Dispose() => DisposeCount++;
    }
}
