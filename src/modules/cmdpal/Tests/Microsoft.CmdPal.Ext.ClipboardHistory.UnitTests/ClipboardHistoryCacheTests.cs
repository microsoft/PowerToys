// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public sealed class ClipboardHistoryCacheTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RefreshAsync_RequestsDuringRead_CoalescesAndPublishesLatestOnly(bool obsoleteReadFails)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        var publications = 0;
        var current = Mock.Of<IListItem>();
        using var source = new TestClipboardHistorySource
        {
            Read = async _ =>
            {
                if (Interlocked.Increment(ref reads) == 1)
                {
                    started.SetResult();
                    await release.Task;
                    if (obsoleteReadFails)
                    {
                        throw new InvalidOperationException("Obsolete history read failed.");
                    }

                    return [Entry("stale", Mock.Of<IListItem>())];
                }

                return [Entry("current", current)];
            },
        };
        await using var cache = new ClipboardHistoryCache(source, new ClipboardHistoryWorker(), () => publications++);
        var refresh = cache.RefreshAsync();
        try
        {
            await started.Task.WaitAsync(Timeout);
            for (var i = 0; i < 20; i++)
            {
                Assert.AreSame(refresh, cache.RefreshAsync());
            }
        }
        finally
        {
            release.TrySetResult();
        }

        await refresh.WaitAsync(Timeout);

        Assert.AreEqual(2, reads);
        Assert.AreEqual(1, publications);
        Assert.HasCount(1, cache.Items);
        Assert.AreSame(current, cache.Items[0]);
    }

    [TestMethod]
    public async Task Clear_DuringRead_ImmediatelyDropsSnapshotAndRejectsStaleResult()
    {
        var first = Mock.Of<IListItem>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new TestClipboardHistorySource
        {
            Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([Entry("a", first)]),
        };
        await using var cache = new ClipboardHistoryCache(source, new ClipboardHistoryWorker(), () => { });
        await cache.RefreshAsync().WaitAsync(Timeout);
        source.Read = async _ =>
        {
            started.TrySetResult();
            await release.Task;
            return [Entry("a", first)];
        };

        var refresh = cache.RefreshAsync();
        try
        {
            await started.Task.WaitAsync(Timeout);
            cache.Clear();
            Assert.IsEmpty(cache.Items);
            source.Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([]);
            Assert.AreSame(refresh, cache.RefreshAsync());
        }
        finally
        {
            release.TrySetResult();
        }

        await refresh.WaitAsync(Timeout);
        Assert.IsEmpty(cache.Items);
    }

    [TestMethod]
    public async Task RefreshAsync_Failure_ClearsStaleItemsAndCanRetry()
    {
        using var source = new TestClipboardHistorySource
        {
            Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([Entry("a", Mock.Of<IListItem>())]),
        };
        await using var cache = new ClipboardHistoryCache(source, new ClipboardHistoryWorker(), () => { });
        await cache.RefreshAsync().WaitAsync(Timeout);
        source.Read = _ => Task.FromException<IReadOnlyList<ClipboardHistoryEntry>>(new InvalidOperationException("History unavailable."));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => cache.RefreshAsync().WaitAsync(Timeout));
        Assert.IsEmpty(cache.Items);
        Assert.IsFalse(cache.IsRefreshing);

        var replacement = Mock.Of<IListItem>();
        source.Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([Entry("b", replacement)]);
        await cache.RefreshAsync().WaitAsync(Timeout);
        Assert.AreSame(replacement, cache.Items[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_DispatchFailure_DoesNotLeaveRefreshStuck()
    {
        using var source = new TestClipboardHistorySource();
        await using var cache = new ClipboardHistoryCache(source, new RejectingWorker(), () => { });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => cache.RefreshAsync());

        Assert.IsFalse(cache.IsRefreshing);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => cache.RefreshAsync());
        Assert.IsFalse(cache.IsRefreshing);
    }

    [TestMethod]
    public async Task RefreshAsync_DisabledThenEnabled_ReloadsContentRatherThanKeepingOldEntries()
    {
        var loads = 0;
        var entry = new ClipboardHistoryEntry("a", _ =>
        {
            loads++;
            return Task.FromResult(Mock.Of<IListItem>());
        });
        using var source = new TestClipboardHistorySource
        {
            Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([entry]),
        };
        await using var cache = new ClipboardHistoryCache(source, new ClipboardHistoryWorker(), () => { });
        await cache.RefreshAsync().WaitAsync(Timeout);
        var original = cache.Items[0];

        source.Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([]);
        await cache.RefreshAsync().WaitAsync(Timeout);
        Assert.IsEmpty(cache.Items);

        source.Read = _ => Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([entry]);
        await cache.RefreshAsync().WaitAsync(Timeout);
        Assert.AreEqual(2, loads);
        Assert.AreNotSame(original, cache.Items[0]);
    }

    [TestMethod]
    public async Task DisposeAsync_DuringRead_CancelsAndPreventsPublication()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var blocked = new TaskCompletionSource<IReadOnlyList<ClipboardHistoryEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publications = 0;
        using var source = new TestClipboardHistorySource
        {
            Read = token =>
            {
                started.SetResult();
                return blocked.Task.WaitAsync(token);
            },
        };
        var worker = new ClipboardHistoryWorker();
        await using var cache = new ClipboardHistoryCache(source, worker, () => publications++);
        var refresh = cache.RefreshAsync();
        await started.Task.WaitAsync(Timeout);

        await cache.DisposeAsync().AsTask().WaitAsync(Timeout);
        await refresh.WaitAsync(Timeout);
        await cache.RefreshAsync();

        Assert.AreEqual(0, publications);
        Assert.IsEmpty(cache.Items);
        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunAsync(() => Task.CompletedTask));
    }

    private static ClipboardHistoryEntry Entry(string id, IListItem item) => new(id, _ => Task.FromResult(item));

    private sealed class RejectingWorker : IClipboardHistoryWorker
    {
        public Task RunAsync(Func<Task> action) => Task.FromException(new InvalidOperationException("Dispatcher unavailable."));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
