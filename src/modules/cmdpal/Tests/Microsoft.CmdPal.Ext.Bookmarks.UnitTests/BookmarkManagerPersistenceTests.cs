// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

public partial class BookmarkManagerTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DisposeAsync_PendingEdits_DrainsLatestSnapshotAfterFirstSave(bool firstSaveFails)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var attempts = 0;
        var dataSource = new MockBookmarkDataSource
        {
            BeforeSave = () =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    started.SetResult();
                    release.Wait();
                    if (firstSaveFails)
                    {
                        throw new InvalidOperationException("First write failed.");
                    }
                }
            },
        };
        using var manager = new BookmarksManager(dataSource);
        var timeout = TimeSpan.FromSeconds(5);
        try
        {
            var bookmark = await Task.Run(() => manager.Add("Before", "C:\\Before")).WaitAsync(timeout);
            await started.Task.WaitAsync(timeout);
            manager.Update(bookmark.Id, "After", "C:\\After");
            var removed = manager.Add("Remove", "C:\\Remove");
            Assert.IsTrue(manager.Remove(removed.Id));
            var disposal = manager.DisposeAsync().AsTask();
            Assert.IsFalse(disposal.IsCompleted);
            release.Set();
            await disposal.WaitAsync(timeout);
            await manager.DisposeAsync().AsTask().WaitAsync(timeout);

            using var reloaded = new BookmarksManager(dataSource);
            var saved = reloaded.Bookmarks.Single();
            Assert.AreEqual(bookmark.Id, saved.Id);
            Assert.AreEqual("After", saved.Name);
            Assert.AreEqual("C:\\After", saved.Bookmark);
            Assert.AreEqual(2, attempts);
            Assert.AreEqual(firstSaveFails ? 1 : 2, dataSource.SaveCount);
        }
        finally
        {
            release.Set();
            await manager.DisposeAsync().AsTask().WaitAsync(timeout);
        }
    }
}
