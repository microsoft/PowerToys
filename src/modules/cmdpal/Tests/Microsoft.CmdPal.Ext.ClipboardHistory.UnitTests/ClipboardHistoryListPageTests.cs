// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;
using Microsoft.CmdPal.Ext.ClipboardHistory.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.UnitTests;

[TestClass]
public sealed class ClipboardHistoryListPageTests
{
    [TestMethod]
    public async Task GetItems_RepeatedFetches_ReusesSnapshotWithOneSettingsSubscriber()
    {
        var settings = new TestSettings();
        var reads = 0;
        var item = Mock.Of<IListItem>();
        using var source = new TestClipboardHistorySource
        {
            Read = _ =>
            {
                reads++;
                return Task.FromResult<IReadOnlyList<ClipboardHistoryEntry>>([new("a", _ => Task.FromResult(item))]);
            },
        };
        await using var page = new ClipboardHistoryListPage(settings, source, new ClipboardHistoryWorker());
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.ItemsChanged += (_, _) => published.TrySetResult();

        page.GetItems();
        await published.Task.WaitAsync(TimeSpan.FromSeconds(10));
        for (var i = 0; i < 20; i++)
        {
            Assert.AreSame(item, page.GetItems()[0]);
        }

        Assert.AreEqual(1, reads);
        Assert.AreEqual(1, settings.Subscribers);
        Assert.AreEqual(1, source.HistorySubscribers);
        Assert.AreEqual(1, source.EnabledSubscribers);

        settings.RaiseChanged();
        Assert.AreEqual(1, reads);
        Assert.AreSame(item, page.GetItems()[0]);

        await page.DisposeAsync();
        Assert.AreEqual(0, settings.Subscribers);
        Assert.AreEqual(0, source.HistorySubscribers);
        Assert.AreEqual(0, source.EnabledSubscribers);
        Assert.IsTrue(source.IsDisposed);
        Assert.IsEmpty(page.GetItems());
    }

    [TestMethod]
    public async Task DisposeAsync_UnopenedPage_DetachesSettingsAndHistoryEvents()
    {
        var settings = new TestSettings();
        using var source = new TestClipboardHistorySource();
        var worker = new ClipboardHistoryWorker();
        await using var page = new ClipboardHistoryListPage(settings, source, worker);
        var notifications = 0;
        page.ItemsChanged += (_, _) => notifications++;

        await page.DisposeAsync();
        settings.RaiseChanged();
        source.RaiseHistoryChanged();
        source.RaiseHistoryEnabledChanged();

        Assert.AreEqual(0, notifications);
        Assert.AreEqual(0, settings.Subscribers);
        Assert.AreEqual(0, source.HistorySubscribers);
        Assert.AreEqual(0, source.EnabledSubscribers);
        Assert.IsTrue(source.IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => worker.RunAsync(() => Task.CompletedTask));
    }

    private sealed class TestSettings : IClipboardHistorySettings
    {
        private EventHandler _changed;

        public event EventHandler Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public int Subscribers => _changed?.GetInvocationList().Length ?? 0;

        public bool KeepAfterPaste => false;

        public bool DeleteFromHistoryRequiresConfirmation => true;

        public PrimaryAction PrimaryAction => PrimaryAction.Default;

        public void RaiseChanged() => _changed?.Invoke(this, EventArgs.Empty);
    }
}
