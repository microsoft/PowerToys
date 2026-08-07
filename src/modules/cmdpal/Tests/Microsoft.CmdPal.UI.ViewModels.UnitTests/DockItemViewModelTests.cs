// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class DockItemViewModelTests
{
    private sealed class TestPageContext(TaskScheduler scheduler) : IPageContext
    {
        public TaskScheduler Scheduler { get; } = scheduler;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = [];

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        public void ExecuteAllAvailable()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
            }
        }

        public void ExecuteUntil(Func<bool> condition)
        {
            var timeout = Stopwatch.StartNew();
            while (!condition())
            {
                ExecuteAllAvailable();
                if (timeout.Elapsed > TimeSpan.FromSeconds(2))
                {
                    Assert.Fail("Timed out waiting for scheduled Dock work.");
                }

                Thread.Sleep(5);
            }
        }
    }

    private sealed partial class TestDockPage : ListPage
    {
        private IListItem[] _items;
        private int _getItemsCallCount;
        private Action? _getItemsCallback;

        public TestDockPage(IListItem item)
        {
            _items = [item];
        }

        public int GetItemsCallCount => Volatile.Read(ref _getItemsCallCount);

        public override IListItem[] GetItems()
        {
            Interlocked.Increment(ref _getItemsCallCount);
            Interlocked.Exchange(ref _getItemsCallback, null)?.Invoke();
            return Volatile.Read(ref _items);
        }

        public int ItemsChangedSubscriberCount =>
            ((Delegate?)typeof(ListPage)
                .GetField(nameof(ItemsChanged), BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(this))?
                .GetInvocationList()
                .Length ?? 0;

        public void SetItem(IListItem item) => SetItems(item);

        public void SetItems(params IListItem[] items) => Volatile.Write(ref _items, items);

        public void OnNextGetItems(Action callback) => Volatile.Write(ref _getItemsCallback, callback);

        public void TriggerItemsChanged() => RaiseItemsChanged(_items.Length);
    }

    private sealed record BandFixture(
        DockBandViewModel Band,
        CommandItemViewModel Root,
        TestDockPage Page,
        TestPageContext Context,
        QueuedTaskScheduler Scheduler);

    [TestMethod]
    public void ItemsChanged_CleansReplacedViewModelAfterUiUpdate()
    {
        var fixture = CreateBandFixture();
        try
        {
            var original = fixture.Band.Items[0];
            fixture.Page.SetItem(CreateItem("Replacement"));

            fixture.Page.TriggerItemsChanged();
            fixture.Scheduler.ExecuteUntil(() => !ReferenceEquals(original, fixture.Band.Items[0]));

            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => original.Initialized.HasFlag(InitializedState.CleanedUp),
                    TimeSpan.FromSeconds(2)),
                "The replaced Dock item was not cleaned.");
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }

    [TestMethod]
    public void RepeatedItemsChanged_ReusesStableViewModel()
    {
        var fixture = CreateBandFixture();
        try
        {
            var original = fixture.Band.Items[0];

            for (var i = 0; i < 100; i++)
            {
                fixture.Page.TriggerItemsChanged();
                fixture.Scheduler.ExecuteAllAvailable();
            }

            Assert.AreSame(original, fixture.Band.Items[0]);
            Assert.IsFalse(original.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }

    [TestMethod]
    public void DuplicateItems_ReuseSingleViewModel()
    {
        var fixture = CreateBandFixture();
        try
        {
            var duplicate = CreateItem("Duplicate");
            fixture.Page.SetItems(duplicate, duplicate);

            fixture.Page.TriggerItemsChanged();
            fixture.Scheduler.ExecuteUntil(() => fixture.Band.Items.Count == 2);

            Assert.AreSame(fixture.Band.Items[0], fixture.Band.Items[1]);

            var sharedViewModel = fixture.Band.Items[0];
            fixture.Page.SetItem(duplicate);
            fixture.Page.TriggerItemsChanged();
            fixture.Scheduler.ExecuteUntil(() => fixture.Band.Items.Count == 1);

            Assert.AreSame(sharedViewModel, fixture.Band.Items[0]);
            Assert.IsFalse(sharedViewModel.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }

    [TestMethod]
    public void BurstyItemsChanged_CoalescesRefreshWork()
    {
        var fixture = CreateBandFixture();
        try
        {
            var initialCalls = fixture.Page.GetItemsCallCount;

            for (var i = 0; i < 1000; i++)
            {
                fixture.Page.TriggerItemsChanged();
            }

            Assert.AreEqual(initialCalls + 1, fixture.Page.GetItemsCallCount);

            fixture.Scheduler.ExecuteAllAvailable();
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => fixture.Page.GetItemsCallCount == initialCalls + 2,
                    TimeSpan.FromSeconds(2)),
                "The coalesced follow-up refresh did not run.");
            fixture.Scheduler.ExecuteAllAvailable();

            Assert.AreEqual(initialCalls + 2, fixture.Page.GetItemsCallCount);
            Assert.AreEqual(1, fixture.Band.Items.Count);
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }

    [TestMethod]
    public void CleanupWithQueuedRefresh_DoesNotRepopulateItems()
    {
        var fixture = CreateBandFixture();
        try
        {
            var original = fixture.Band.Items[0];
            fixture.Page.SetItem(CreateItem("Replacement"));
            fixture.Page.TriggerItemsChanged();

            fixture.Band.SafeCleanup();
            fixture.Scheduler.ExecuteAllAvailable();

            Assert.AreEqual(0, fixture.Band.Items.Count);
            Assert.IsTrue(
                SpinWait.SpinUntil(
                    () => original.Initialized.HasFlag(InitializedState.CleanedUp),
                    TimeSpan.FromSeconds(2)),
                "The disposed Dock item was not cleaned.");
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }

    [TestMethod]
    public void CleanupDuringInitialization_DoesNotLeaveItemsChangedSubscription()
    {
        var fixture = CreateBandFixture(cleanupDuringInitialization: true);
        try
        {
            Assert.AreEqual(0, fixture.Page.ItemsChangedSubscriberCount);
        }
        finally
        {
            CleanupFixture(fixture);
        }
    }

    private static BandFixture CreateBandFixture(bool cleanupDuringInitialization = false)
    {
        var scheduler = new QueuedTaskScheduler();
        var context = new TestPageContext(scheduler);
        var page = new TestDockPage(CreateItem("Initial"))
        {
            Id = "test.dock.page",
            Name = "Test Dock Page",
            Title = "Test Dock Page",
        };
        var root = new CommandItemViewModel(
            new(new CommandItem(page) { Title = page.Title }),
            new(context),
            DefaultContextMenuFactory.Instance);
        root.SlowInitializeProperties();

        var settingsService = new Mock<ISettingsService>();
        settingsService.SetupGet(service => service.Settings).Returns(new SettingsModel());

        var band = new DockBandViewModel(
            root,
            new(context),
            new DockBandSettings { ProviderId = "test", CommandId = page.Id },
            settingsService.Object,
            DefaultContextMenuFactory.Instance);
        if (cleanupDuringInitialization)
        {
            page.OnNextGetItems(band.SafeCleanup);
        }

        band.InitializeProperties();
        if (cleanupDuringInitialization)
        {
            scheduler.ExecuteAllAvailable();
        }
        else
        {
            scheduler.ExecuteUntil(() => band.Items.Count == 1);
        }

        return new(band, root, page, context, scheduler);
    }

    private static ListItem CreateItem(string title) =>
        new(new NoOpCommand { Name = title }) { Title = title };

    private static void CleanupFixture(BandFixture fixture)
    {
        fixture.Band.SafeCleanup();
        fixture.Root.SafeCleanup();
        fixture.Scheduler.ExecuteAllAvailable();
    }
}
