// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Indexer;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Indexer.UnitTests;

[TestClass]
public sealed class IndexerThumbnailTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    private static readonly int[] ExpectedPageOffsets = [0, 20];

    [TestMethod]
    public async Task Search_PublishesTextBeforeDemandedThumbnailCompletes()
    {
        using var engine = new TestSearchEngine(25);
        var started = NewSignal();
        var release = new TaskCompletionSource<IconInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var page = new IndexerPage(
            () => engine,
            (_, _) =>
            {
                Interlocked.Increment(ref requests);
                started.TrySetResult();
                return release.Task;
            });
        var itemChanges = 0;
        page.ItemsChanged += (_, _) => Interlocked.Increment(ref itemChanges);

        try
        {
            page.UpdateSearchText(string.Empty, "query");
            await page.SearchTask.WaitAsync(TestTimeout);
            var items = page.GetItems();

            Assert.AreEqual(20, items.Length);
            Assert.AreEqual("File 0", items[0].Title);
            Assert.AreEqual(engine.Items[0].FilePath, items[0].Subtitle);
            Assert.AreEqual(0, requests);
            Assert.IsFalse(page.IsLoading);
            Assert.IsTrue(page.HasMoreItems);
            Assert.AreSame(Icons.DocumentIcon, items[0].Icon);
            Assert.AreSame(Icons.DocumentIcon, items[0].Icon);
            await started.Task.WaitAsync(TestTimeout);
            Assert.AreEqual(1, requests);
            Assert.IsFalse(release.Task.IsCompleted);
            page.LoadMore();
            Assert.AreEqual(25, page.GetItems().Length);
            Assert.AreEqual(1, requests);
            var changesBeforeThumbnail = itemChanges;

            var changes = new List<string>();
            items[0].PropChanged += (_, args) => changes.Add(args.PropertyName);
            var icon = new IconInfo("loaded");
            release.SetResult(icon);
            await page.ThumbnailTask.WaitAsync(TestTimeout);

            Assert.AreSame(items[0], page.GetItems()[0]);
            Assert.AreSame(icon, items[0].Icon);
            CollectionAssert.AreEqual(new[] { nameof(IListItem.Icon) }, changes);
            Assert.AreEqual(changesBeforeThumbnail, itemChanges);
        }
        finally
        {
            release.TrySetResult(null);
            await page.ThumbnailTask.WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task LoadMore_PreservesOrderNoticeAndExistingItemsWithoutRequestingIcons()
    {
        using var engine = new TestSearchEngine(25)
        {
            Notice = new SearchNoticeInfo("Indexing", "Please wait"),
        };
        var requests = 0;
        using var page = new IndexerPage(
            () => engine,
            (_, _) =>
            {
                Interlocked.Increment(ref requests);
                return Task.FromResult<IconInfo?>(null);
            });
        page.UpdateSearchText(string.Empty, "query");
        await page.SearchTask.WaitAsync(TestTimeout);
        var firstPage = page.GetItems();

        page.LoadMore();
        var items = page.GetItems();
        page.LoadMore();

        Assert.AreEqual(26, items.Length);
        Assert.AreEqual("Indexing", items[0].Title);
        Assert.AreSame(firstPage[0], items[0]);
        Assert.AreSame(firstPage[1], items[1]);
        CollectionAssert.AreEqual(engine.Items.Select(item => item.Title).ToArray(), items.Skip(1).Select(item => item.Title).ToArray());
        CollectionAssert.AreEqual(ExpectedPageOffsets, engine.Offsets);
        Assert.AreEqual(0, requests);
        Assert.IsFalse(page.HasMoreItems);
        Assert.IsFalse(page.IsLoading);
    }

    [TestMethod]
    public async Task NewQuery_DoesNotApplyOldThumbnailOrRequestUndemandedIcons()
    {
        using var firstEngine = new TestSearchEngine(2);
        using var secondEngine = new TestSearchEngine(1);
        var engines = new Queue<IIndexerSearchEngine>([firstEngine, secondEngine]);
        var started = NewSignal();
        var release = new TaskCompletionSource<IconInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var page = new IndexerPage(
            () => engines.Dequeue(),
            (_, _) =>
            {
                if (Interlocked.Increment(ref requests) > 1)
                {
                    return Task.FromResult<IconInfo?>(new IconInfo("current"));
                }

                started.TrySetResult();
                return release.Task;
            });

        try
        {
            page.UpdateSearchText(string.Empty, "first");
            await page.SearchTask.WaitAsync(TestTimeout);
            var oldItem = page.GetItems()[0];
            _ = oldItem.Icon;
            await started.Task.WaitAsync(TestTimeout);

            page.UpdateSearchText("first", "second");
            await page.SearchTask.WaitAsync(TestTimeout);
            var newItem = page.GetItems()[0];
            release.SetResult(new IconInfo("obsolete"));
            await page.ThumbnailTask.WaitAsync(TestTimeout);

            Assert.AreSame(Icons.DocumentIcon, oldItem.Icon);
            Assert.AreSame(secondEngine.Items[0], newItem);
            Assert.AreNotSame(oldItem, newItem);
            Assert.IsTrue(firstEngine.Disposed);
            _ = firstEngine.Items[1].Icon;
            Assert.AreEqual(1, requests);
            _ = newItem.Icon;
            await page.ThumbnailTask.WaitAsync(TestTimeout);
            Assert.AreEqual("current", newItem.Icon?.Light?.Icon);
        }
        finally
        {
            release.TrySetResult(null);
            await page.ThumbnailTask.WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task NewQuery_DiscardsInFlightRowsBeforePublishingTheNextGeneration()
    {
        var started = NewSignal();
        using var release = new ManualResetEventSlim();
        using var firstEngine = new TestSearchEngine(20)
        {
            BeforeFetch = _ =>
            {
                started.TrySetResult();
                if (!release.Wait(TestTimeout))
                {
                    throw new TimeoutException("The test did not release the row fetch.");
                }
            },
        };
        using var secondEngine = new TestSearchEngine(1);
        var engines = new Queue<IIndexerSearchEngine>([firstEngine, secondEngine]);
        using var page = new IndexerPage(() => engines.Dequeue());
        try
        {
            page.UpdateSearchText(string.Empty, "first");
            var oldSearch = page.SearchTask;
            await started.Task.WaitAsync(TestTimeout);
            page.UpdateSearchText("first", "second");

            Assert.IsEmpty(page.GetItems());
            Assert.IsFalse(firstEngine.Disposed);
            release.Set();
            await oldSearch.WaitAsync(TestTimeout);
            await page.SearchTask.WaitAsync(TestTimeout);

            Assert.AreEqual(1, page.GetItems().Length);
            Assert.AreSame(secondEngine.Items[0], page.GetItems()[0]);
            Assert.IsTrue(firstEngine.Disposed);
            Assert.IsFalse(page.IsLoading);
        }
        finally
        {
            release.Set();
            await page.SearchTask.WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task LoadMore_FailurePreservesExistingRowsAndSurfacesSearchNotice()
    {
        using var engine = new TestSearchEngine(25)
        {
            BeforeFetch = offset =>
            {
                if (offset > 0)
                {
                    throw new IOException("Row fetch failed");
                }
            },
        };
        using var page = new IndexerPage(() => engine);
        page.UpdateSearchText(string.Empty, "query");
        await page.SearchTask.WaitAsync(TestTimeout);

        page.LoadMore();

        Assert.AreEqual(21, page.GetItems().Length);
        Assert.AreEqual(Resources.Indexer_SearchFailedMessage, page.GetItems()[0].Title);
        Assert.AreSame(engine.Items[0], page.GetItems()[1]);
        Assert.IsFalse(page.HasMoreItems);
        Assert.IsFalse(page.IsLoading);
    }

    [TestMethod]
    public async Task EmptyQueryAndDispose_CancelDemandAndReleaseTheSearchEngine()
    {
        using var engine = new TestSearchEngine(25);
        var requests = 0;
        using var page = new IndexerPage(
            () => engine,
            (_, _) =>
            {
                Interlocked.Increment(ref requests);
                return Task.FromResult<IconInfo?>(null);
            });
        page.UpdateSearchText(string.Empty, "query");
        await page.SearchTask.WaitAsync(TestTimeout);
        var oldItem = page.GetItems()[0];

        page.UpdateSearchText("query", string.Empty);
        await page.SearchTask.WaitAsync(TestTimeout);
        _ = oldItem.Icon;
        page.Dispose();
        page.LoadMore();
        page.UpdateSearchText(string.Empty, "ignored");

        Assert.IsEmpty(page.GetItems());
        Assert.IsTrue(engine.Disposed);
        Assert.IsFalse(page.HasMoreItems);
        Assert.IsFalse(page.IsLoading);
        Assert.AreEqual(0, requests);
        Assert.IsTrue(page.ThumbnailTask.IsCompletedSuccessfully);
    }

    [TestMethod]
    public async Task Request_BoundsConcurrencyAcrossCanceledGenerationsAndDropsPendingWork()
    {
        var gate = new Lock();
        using var oldQuery = new CancellationTokenSource();
        using var newQuery = new CancellationTokenSource();
        var started = NewSignal();
        var release = NewSignal();
        var paths = new ConcurrentQueue<string>();
        var active = 0;
        var peak = 0;
        using var loader = new IndexerThumbnailLoader(
            gate,
            async (path, _) =>
            {
                var count = Interlocked.Increment(ref active);
                UpdateMaximum(ref peak, count);
                paths.Enqueue(path);
                if (count == IndexerThumbnailLoader.MaxConcurrency)
                {
                    started.TrySetResult();
                }

                await release.Task;
                Interlocked.Decrement(ref active);
                return new IconInfo("loaded");
            });
        var oldItems = Enumerable.Range(0, 20).Select(CreateItem).ToArray();
        var current = CreateItem(99);

        try
        {
            foreach (var item in oldItems)
            {
                loader.Request(item, oldQuery.Token);
            }

            await started.Task.WaitAsync(TestTimeout);
            lock (gate)
            {
                oldQuery.Cancel();
                loader.ClearPending();
                loader.Request(current, newQuery.Token);
            }

            Assert.AreEqual(IndexerThumbnailLoader.MaxConcurrency, paths.Count);
            release.SetResult();
            await loader.Completion.WaitAsync(TestTimeout);

            Assert.AreEqual(IndexerThumbnailLoader.MaxConcurrency, peak);
            Assert.AreEqual(IndexerThumbnailLoader.MaxConcurrency + 1, paths.Count);
            Assert.AreEqual(current.FilePath, paths.Last());
            Assert.AreEqual("loaded", current.Icon?.Light?.Icon);
            foreach (var item in oldItems)
            {
                Assert.AreSame(Icons.DocumentIcon, item.Icon);
            }
        }
        finally
        {
            release.TrySetResult();
            await loader.Completion.WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task Request_ReportsFailureAndKeepsPlaceholderWhileOtherItemsLoad()
    {
        var failure = new IOException("Thumbnail unavailable");
        var errors = new ConcurrentQueue<Exception>();
        using var loader = new IndexerThumbnailLoader(
            new Lock(),
            (path, _) => path.EndsWith('0')
                ? Task.FromException<IconInfo?>(failure)
                : Task.FromResult<IconInfo?>(new IconInfo("loaded")),
            errors.Enqueue);
        var failed = CreateItem(0);
        var success = CreateItem(1);
        loader.Request(failed, CancellationToken.None);
        loader.Request(success, CancellationToken.None);
        await loader.Completion.WaitAsync(TestTimeout);

        Assert.AreSame(Icons.DocumentIcon, failed.Icon);
        Assert.AreEqual("loaded", success.Icon?.Light?.Icon);
        Assert.AreEqual(1, errors.Count);
        Assert.AreSame(failure, errors.Single());
    }

    [TestMethod]
    public async Task Dispose_DrainsRunningWorkWithoutApplyingItOrStartingPendingWork()
    {
        var started = NewSignal();
        var release = NewSignal();
        var calls = 0;
        using var loader = new IndexerThumbnailLoader(
            new Lock(),
            async (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == IndexerThumbnailLoader.MaxConcurrency)
                {
                    started.SetResult();
                }

                await release.Task;
                return new IconInfo("obsolete");
            });
        var items = Enumerable.Range(0, 20).Select(CreateItem).ToArray();
        try
        {
            foreach (var item in items)
            {
                loader.Request(item, CancellationToken.None);
            }

            await started.Task.WaitAsync(TestTimeout);
            loader.Dispose();
            loader.Request(CreateItem(99), CancellationToken.None);
            release.SetResult();
            await loader.Completion.WaitAsync(TestTimeout);

            Assert.AreEqual(IndexerThumbnailLoader.MaxConcurrency, calls);
            foreach (var item in items)
            {
                Assert.AreSame(Icons.DocumentIcon, item.Icon);
            }
        }
        finally
        {
            release.TrySetResult();
            await loader.Completion.WaitAsync(TestTimeout);
        }
    }

    [TestMethod]
    public async Task LoadThumbnail_CancellationDisposesReturnedStream()
    {
        using var source = new MemoryStream([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        var release = new TaskCompletionSource<IRandomAccessStream?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var load = IndexerThumbnailLoader.LoadThumbnailAsync(() => release.Task, cancellation.Token);

        cancellation.Cancel();
        release.SetResult(source.AsRandomAccessStream());
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => load.WaitAsync(TestTimeout));

        Assert.IsFalse(source.CanRead);
    }

    [TestMethod]
    public async Task LoadThumbnail_CanceledQueryDoesNotAcquireStream()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => IndexerThumbnailLoader.LoadThumbnailAsync(
                () => throw new AssertFailedException("Canceled work acquired a stream."),
                cancellation.Token));
    }

    [TestMethod]
    public async Task LoadThumbnail_MissingThumbnailPreservesTheDefaultIcon()
    {
        using var loader = new IndexerThumbnailLoader(new Lock(), (_, _) => Task.FromResult<IconInfo?>(null));
        var item = CreateItem(0);
        loader.Request(item, CancellationToken.None);
        await loader.Completion.WaitAsync(TestTimeout);

        Assert.AreSame(Icons.DocumentIcon, item.Icon);
    }

    [TestMethod]
    public async Task CreateIcon_HostCanReadAfterSourceIsDisposedAndReopenAfterReadIsClosed()
    {
        IconInfo icon;
        using (var source = new InMemoryRandomAccessStream())
        {
            using var writer = new DataWriter(source);
            writer.WriteBytes([1, 2, 3]);
            await writer.StoreAsync();
            icon = IndexerThumbnailLoader.CreateIcon(source);
        }

        for (var index = 0; index < 2; index++)
        {
            using var read = await icon.Light.Data!.OpenReadAsync();
            using var reader = new DataReader(read);
            Assert.AreEqual(3u, await reader.LoadAsync(3));
            var bytes = new byte[3];
            reader.ReadBytes(bytes);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, bytes);
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static IndexerListItem CreateItem(int index) => new(new IndexerItem
    {
        FileName = $"File {index}",
        FullPath = $@"C:\CmdPalThumbnailTests\missing-{index}",
    })
    {
        Icon = Icons.DocumentIcon,
    };

    private static void UpdateMaximum(ref int location, int value)
    {
        var observed = Volatile.Read(ref location);
        while (value > observed)
        {
            var previous = Interlocked.CompareExchange(ref location, value, observed);
            if (previous == observed)
            {
                return;
            }

            observed = previous;
        }
    }

    private sealed class TestSearchEngine(int count) : IIndexerSearchEngine
    {
        internal IndexerListItem[] Items { get; } = Enumerable.Range(0, count).Select(CreateItem).ToArray();

        internal List<int> Offsets { get; } = [];

        internal SearchNoticeInfo? Notice { get; init; }

        internal Action<int>? BeforeFetch { get; init; }

        internal bool Disposed { get; private set; }

        public SearchNoticeInfo? Query(string query, uint queryCookie) => Notice;

        public IList<IListItem> FetchItems(int offset, int limit, uint queryCookie, out bool hasMore, out SearchNoticeInfo? notice, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(Disposed, this);
            BeforeFetch?.Invoke(offset);
            Offsets.Add(offset);
            hasMore = offset + limit < Items.Length;
            notice = Notice;
            return Items.Skip(offset).Take(limit).Cast<IListItem>().ToArray();
        }

        public void Dispose() => Disposed = true;
    }
}
