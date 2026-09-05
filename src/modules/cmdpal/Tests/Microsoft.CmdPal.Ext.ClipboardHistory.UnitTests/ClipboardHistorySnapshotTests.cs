// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public sealed class ClipboardHistorySnapshotTests
{
    [TestMethod]
    public async Task RefreshAsync_UnchangedEntries_ReusesItemsWithoutLoadingContent()
    {
        var loads = 0;
        var entry = new ClipboardHistoryEntry("text", _ =>
        {
            loads++;
            return Task.FromResult(Mock.Of<IListItem>());
        });

        var first = await ClipboardHistorySnapshot.Empty.RefreshAsync([entry], CancellationToken.None);
        var second = await first.RefreshAsync([entry], CancellationToken.None);

        Assert.AreEqual(1, loads);
        Assert.AreSame(first.Items[0], second.Items[0]);
    }

    [TestMethod]
    public async Task RefreshAsync_AddRemoveReorder_KeepsOnlyCurrentHistoryInOrder()
    {
        var loads = 0;
        ClipboardHistoryEntry Entry(string id) => new(id, _ =>
        {
            loads++;
            return Task.FromResult(Mock.Of<IListItem>());
        });

        var first = await ClipboardHistorySnapshot.Empty.RefreshAsync([Entry("a"), Entry("b")], CancellationToken.None);
        var second = await first.RefreshAsync([Entry("b"), Entry("c"), Entry("a")], CancellationToken.None);
        Assert.AreSame(first.Items[1], second.Items[0]);
        Assert.AreSame(first.Items[0], second.Items[2]);
        Assert.AreEqual(3, loads);

        var third = await second.RefreshAsync([Entry("c")], CancellationToken.None);
        Assert.HasCount(1, third.Items);
        Assert.AreSame(second.Items[1], third.Items[0]);

        var fourth = await third.RefreshAsync([Entry("a"), Entry("c")], CancellationToken.None);
        Assert.AreEqual(4, loads);
        Assert.AreNotSame(first.Items[0], fourth.Items[0]);
        Assert.AreSame(third.Items[0], fourth.Items[1]);
    }

    [TestMethod]
    public async Task RefreshAsync_RemovedItem_DoesNotDisposeContentHeldByACommand()
    {
        var item = new Mock<IListItem>();
        var disposable = item.As<IDisposable>();
        var first = await ClipboardHistorySnapshot.Empty.RefreshAsync(
            [new ClipboardHistoryEntry("image", _ => Task.FromResult(item.Object))],
            CancellationToken.None);

        var empty = await first.RefreshAsync([], CancellationToken.None);

        Assert.IsEmpty(empty.Items);
        Assert.AreSame(item.Object, first.Items[0]);
        disposable.Verify(value => value.Dispose(), Times.Never());
    }

    [TestMethod]
    public async Task RefreshAsync_Canceled_DoesNotReadContent()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var loads = 0;
        var entry = new ClipboardHistoryEntry("a", _ =>
        {
            loads++;
            return Task.FromResult(Mock.Of<IListItem>());
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            ClipboardHistorySnapshot.Empty.RefreshAsync([entry], cancellation.Token));

        Assert.AreEqual(0, loads);
    }
}
