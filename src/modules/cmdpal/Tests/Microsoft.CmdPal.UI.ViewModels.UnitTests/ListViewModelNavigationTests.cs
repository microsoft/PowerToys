// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public sealed partial class ListViewModelNavigationTests
{
    private const string InitialGlyph = "\uE8D4";
    private const string SearchGlyph = "\uE8A5";

    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Navigation test host";
    }

    private sealed partial class SearchPage : DynamicListPage
    {
        private IListItem[] _items = [CreateItem("Initial", InitialGlyph)];
        private int _getItemsCount;

        internal int GetItemsCount => Volatile.Read(ref _getItemsCount);

        internal Action<int>? OnGetItems { get; set; }

        public override IListItem[] GetItems()
        {
            var count = Interlocked.Increment(ref _getItemsCount);
            var items = Volatile.Read(ref _items);
            OnGetItems?.Invoke(count);
            return items;
        }

        public override void UpdateSearchText(string oldSearch, string newSearch) =>
            ReplaceItems([CreateItem(newSearch, SearchGlyph)]);

        internal void ReplaceItems(IListItem[] items, bool notify = true)
        {
            Volatile.Write(ref _items, items);
            if (notify)
            {
                RaiseItemsChanged(items.Length);
            }
        }

        internal void Refresh() => RaiseItemsChanged();
    }

    private sealed partial class StaticPage(IListItem[] items) : ListPage
    {
        private int _getItemsCount;

        internal int GetItemsCount => Volatile.Read(ref _getItemsCount);

        public override IListItem[] GetItems()
        {
            Interlocked.Increment(ref _getItemsCount);
            return items;
        }
    }

    private sealed partial class TrackingItem(string title, ManualResetEventSlim? started = null, ManualResetEventSlim? release = null)
        : ListItem(new NoOpCommand { Name = title })
    {
        private int _initializationCount;

        internal int InitializationCount => Volatile.Read(ref _initializationCount);

        public override ITag[] Tags
        {
            get
            {
                Interlocked.Increment(ref _initializationCount);
                started?.Set();
                if (release is not null && !release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("The blocked item was not released.");
                }

                return [];
            }

            set
            {
            }
        }
    }

    private sealed class QueuedTaskScheduler : TaskScheduler
    {
        private readonly ConcurrentQueue<Task> _tasks = new();

        protected override IEnumerable<Task> GetScheduledTasks() => _tasks.ToArray();

        protected override void QueueTask(Task task) => _tasks.Enqueue(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        internal void Drain()
        {
            while (_tasks.TryDequeue(out var task))
            {
                TryExecuteTask(task);
                task.GetAwaiter().GetResult();
            }
        }

        internal void DrainUntil(Func<bool> condition)
        {
            var elapsed = Stopwatch.StartNew();
            while (!condition())
            {
                Drain();
                Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(3), "UI publication did not finish.");
                Thread.Sleep(5);
            }
        }
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    [Timeout(15000)]
    public async Task ReturningToRetainedPagePublishesSearchResultsAndIcons(bool isRootPage)
    {
        var page = new SearchPage();
        var viewModel = CreateViewModel(page);
        viewModel.IsRootPage = isRootPage;
        using var shell = CreateShell();
        shell.CurrentPage = viewModel;

        try
        {
            await ObserveItemsAsync(viewModel, "Initial", viewModel.InitializeProperties);
            Assert.AreEqual(InitialGlyph, viewModel.FilteredItems[0].Icon.Light.Icon);

            foreach (var query in new[] { "Word", "Excel", "PowerPoint" })
            {
                await ObserveItemsAsync(viewModel, query, () =>
                {
                    // Exercise the same shell setter as Frame forward/back navigation:
                    // the navigation parameter retains and restores this exact VM.
                    shell.CurrentPage = shell.NullPage;
                    shell.CurrentPage = viewModel;
                    viewModel.SearchTextBox = query;
                });

                Assert.AreSame(viewModel, shell.CurrentPage);
                Assert.AreEqual(SearchGlyph, viewModel.FilteredItems[0].Icon.Light.Icon);
            }
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ReturningToStaticPageRestartsPendingRealizedIconsWithoutItemsChanged()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var items = Enumerable.Range(0, 48).Select(index => (IListItem)CreateItem($"Item {index}")).ToArray();
        items[20] = new TrackingItem("Item 20", started, release) { Icon = new IconInfo(SearchGlyph) };
        var last = new TrackingItem("Item 47") { Icon = new IconInfo(SearchGlyph) };
        items[47] = last;
        var page = new StaticPage(items);
        var viewModel = CreateViewModel(page);
        using var shell = CreateShell();
        shell.CurrentPage = viewModel;
        ListItemRealizationRegistration registration = default;
        Task? oldWorker = null;

        try
        {
            await ObserveItemsAsync(viewModel, vm => vm.FilteredItems.Count == 48, viewModel.InitializeProperties);
            await WaitForPublishedAsync(viewModel);
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(2)));
            oldWorker = GetPrivateField<ListItemInitializationCoordinator>(viewModel, "_itemInitializationCoordinator").Completion;
            var pending = viewModel.FilteredItems.Single(item => ReferenceEquals(item.Model.Unsafe, last));
            Assert.AreEqual(0, last.InitializationCount);
            Assert.IsFalse(pending.Icon.HasIcon(light: true));

            shell.CurrentPage = shell.NullPage;
            release.Set();
            await oldWorker.WaitAsync(TimeSpan.FromSeconds(2));

            // The page has no ItemsChanged event to restart work for us. A new
            // visual tree can publish realization before the shell restores its VM.
            registration = pending.BeginRealization();
            Assert.IsTrue(registration.IsValid);
            shell.CurrentPage = viewModel;
            Assert.IsTrue(await pending.WaitForInitializationAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(3)));

            Assert.AreEqual(1, last.InitializationCount);
            Assert.AreEqual(SearchGlyph, pending.Icon.Light.Icon);
            Assert.IsTrue(viewModel.FilteredItems.Contains(pending));
            Assert.AreEqual(1, page.GetItemsCount, "Restarting retained rows must not refetch an unchanged page.");
        }
        finally
        {
            release.Set();
            registration.Release();
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            if (oldWorker is not null)
            {
                await oldWorker.WaitAsync(TimeSpan.FromSeconds(2));
            }

            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task FetchFromPreviousVisitCannotOverwriteResultsAfterReturn()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page);
        using var shell = CreateShell();
        shell.CurrentPage = viewModel;
        Task? oldFetch = null;

        try
        {
            await ObserveItemsAsync(viewModel, "Initial", viewModel.InitializeProperties);
            page.OnGetItems = count =>
            {
                if (count == 2)
                {
                    started.Set();
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
                }
            };

            oldFetch = Task.Run(() => page.ReplaceItems([CreateItem("Obsolete")]));
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(2)));
            shell.CurrentPage = shell.NullPage;
            page.ReplaceItems([CreateItem("Current")]);
            Assert.AreEqual(2, page.GetItemsCount, "A hidden page must not restart fetching on ItemsChanged.");

            await ObserveItemsAsync(viewModel, "Current", () => shell.CurrentPage = viewModel);
            release.Set();
            await oldFetch.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(SearchGlyph, viewModel.FilteredItems.Single().Icon.Light.Icon);
            Assert.AreEqual(3, page.GetItemsCount);
        }
        finally
        {
            release.Set();
            if (oldFetch is not null)
            {
                await oldFetch.WaitAsync(TimeSpan.FromSeconds(2));
            }

            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public void QueuedPublicationFromPreviousVisitStaysInvalidAfterResume()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        using var shell = CreateShell();
        shell.CurrentPage = viewModel;
        var publications = 0;
        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args) => publications++;

        try
        {
            viewModel.InitializeProperties();
            scheduler.DrainUntil(() => viewModel.FilteredItems.Count == 1);
            scheduler.Drain();
            viewModel.ItemsUpdated += OnItemsUpdated;
            page.ReplaceItems([CreateItem("Obsolete")]);
            shell.CurrentPage = shell.NullPage;
            page.ReplaceItems([CreateItem("Current")]);

            // Hold off the resumed background fetch before it can increment the
            // generation itself. Run queued UI work reentrantly on this test thread
            // to prove suspension invalidated the old callback, not just the next fetch.
            using (GetPrivateField<Lock>(viewModel, "_fetchStateLock").EnterScope())
            {
                shell.CurrentPage = viewModel;
                scheduler.Drain();
                Assert.AreEqual(0, publications, "Resumption must not make a previous visit's callback current again.");
            }

            scheduler.DrainUntil(() => publications == 1);
            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
    public void TerminalCleanupCannotBeReversedByBackNavigation(bool useSafeCleanup)
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        using var shell = CreateShell();
        shell.CurrentPage = viewModel;

        try
        {
            viewModel.InitializeProperties();
            scheduler.DrainUntil(() => viewModel.FilteredItems.Count == 1);
            shell.CurrentPage = shell.NullPage;
            if (useSafeCleanup)
            {
                viewModel.SafeCleanup();
            }
            else
            {
                viewModel.Dispose();
            }

            shell.CurrentPage = viewModel;
            page.ReplaceItems([CreateItem("Must not load")]);
            scheduler.Drain();
            Assert.AreEqual(1, page.GetItemsCount);
            Assert.AreEqual(useSafeCleanup ? 0 : 1, viewModel.FilteredItems.Count);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task NavigationHandoffDoesNotWaitForWorkerOwnedLocks()
    {
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page);
        using var shell = CreateShell();
        shell.CurrentPage = viewModel;
        Task? lockHolder = null;
        Task? navigation = null;

        try
        {
            await ObserveItemsAsync(viewModel, "Initial", viewModel.InitializeProperties);
            lockHolder = Task.Run(() =>
            {
                using (GetPrivateField<Lock>(viewModel, "_initializationCoordinatorLock").EnterScope())
                using (GetPrivateField<Lock>(viewModel, "_fetchStateLock").EnterScope())
                using (GetPrivateField<Lock>(viewModel, "_listLock").EnterScope())
                {
                    held.Set();
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
                }
            });

            Assert.IsTrue(held.Wait(TimeSpan.FromSeconds(2)));
            navigation = Task.Run(() =>
            {
                shell.CurrentPage = shell.NullPage;
                shell.CurrentPage = viewModel;
            });
            await navigation.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsFalse(lockHolder.IsCompleted, "Navigation must finish while the worker still owns the locks.");
        }
        finally
        {
            release.Set();
            if (lockHolder is not null)
            {
                await lockHolder.WaitAsync(TimeSpan.FromSeconds(2));
            }

            if (navigation is not null)
            {
                await navigation.WaitAsync(TimeSpan.FromSeconds(2));
            }

            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ReturningToUnchangedPageDoesNotFetchOrRepublish()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        var publications = 0;
        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args) => publications++;

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            var retained = viewModel.FilteredItems.Single();
            viewModel.ItemsUpdated += OnItemsUpdated;

            for (var visit = 0; visit < 3; visit++)
            {
                viewModel.SuspendForNavigation();
                await viewModel.ResumeAfterNavigation();
                scheduler.Drain();
            }

            Assert.AreEqual(1, page.GetItemsCount);
            Assert.AreEqual(0, publications);
            Assert.AreSame(retained, viewModel.FilteredItems.Single());
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task DirectInitialFetchDeferredBySuspensionIsRecovered()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);

        try
        {
            viewModel.SuspendForNavigation();
            viewModel.InitializeProperties(); // Calls FetchItems directly, not RequestFetch.
            Assert.AreEqual(0, page.GetItemsCount);
            Assert.AreEqual(ListPageFetchPhase.Fetching, GetWorkState(viewModel).Phase);

            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(1, page.GetItemsCount);
            Assert.AreEqual("Initial", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(ListPageFetchPhase.Published, GetWorkState(viewModel).Phase);
        }
        finally
        {
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(15000)]
    public async Task SupersededFetchDoesNotCreateRecoveryWhenItUnwinds(bool finishWhileSuspended)
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        Task? oldFetch = null;

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            page.OnGetItems = count =>
            {
                if (count == 2)
                {
                    started.Set();
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
                }
            };

            oldFetch = Task.Run(() => page.ReplaceItems([CreateItem("Obsolete")]));
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(2)));
            page.ReplaceItems([CreateItem("Current")]);
            scheduler.Drain();
            Assert.AreEqual(ListPageFetchPhase.Published, GetWorkState(viewModel).Phase);

            if (finishWhileSuspended)
            {
                viewModel.SuspendForNavigation();
            }

            release.Set();
            await oldFetch.WaitAsync(TimeSpan.FromSeconds(2));
            if (!finishWhileSuspended)
            {
                viewModel.SuspendForNavigation();
            }

            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(3, page.GetItemsCount, "A superseded fetch must not resurrect a satisfied request.");
            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
        }
        finally
        {
            release.Set();
            if (oldFetch is not null)
            {
                await oldFetch.WaitAsync(TimeSpan.FromSeconds(2));
            }

            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task InterruptedFetchIsRecoveredBeforeTheOldGetItemsReturns()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        Task? oldFetch = null;

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            page.OnGetItems = count =>
            {
                if (count == 2)
                {
                    started.Set();
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
                }
            };

            oldFetch = Task.Run(() => page.ReplaceItems([CreateItem("Current")]));
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(2)));
            viewModel.SuspendForNavigation();

            // No ItemsChanged while suspended, and the cancelled call has not
            // unwound: recovery must already know that the snapshot is unfinished.
            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.IsFalse(oldFetch.IsCompleted);
            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(3, page.GetItemsCount);

            release.Set();
            await oldFetch.WaitAsync(TimeSpan.FromSeconds(2));
            viewModel.SuspendForNavigation();
            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(3, page.GetItemsCount, "The late unwind must not create another recovery fetch.");
        }
        finally
        {
            release.Set();
            if (oldFetch is not null)
            {
                await oldFetch.WaitAsync(TimeSpan.FromSeconds(2));
            }

            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CommittedSnapshotIsRepublishedWithoutGetItemsAndPreservesSelectionIntent()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        viewModel.IsRootPage = true;
        var publications = new List<ItemsUpdatedEventArgs>();
        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args) => publications.Add(args);

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            viewModel.ItemsUpdated += OnItemsUpdated;
            page.ReplaceItems([CreateItem("Current")]);
            Assert.AreEqual(ListPageFetchPhase.Committed, GetWorkState(viewModel).Phase);
            Assert.AreEqual("Initial", viewModel.FilteredItems.Single().Title);

            viewModel.SuspendForNavigation();
            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();

            Assert.AreEqual(2, page.GetItemsCount);
            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(1, publications.Count);
            Assert.IsTrue(publications[0].ForceFirstItem);
            Assert.IsTrue(publications[0].EnsureSelectionVisible);
            Assert.AreEqual(ListPageFetchPhase.Published, GetWorkState(viewModel).Phase);

            viewModel.SuspendForNavigation();
            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(1, publications.Count, "A recovered publication must not be repeated on the next Back.");
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task SuspendedFetchRequestsSubsumeACommittedSnapshot()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            page.ReplaceItems([CreateItem("Committed but superseded")]);
            viewModel.SuspendForNavigation();
            page.ReplaceItems([CreateItem("First suspended change")]);
            page.ReplaceItems([CreateItem("Latest suspended change")]);
            Assert.AreEqual(2, page.GetItemsCount);

            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(3, page.GetItemsCount, "Suspended requests should reconcile in one fetch.");
            Assert.AreEqual("Latest suspended change", viewModel.FilteredItems.Single().Title);
        }
        finally
        {
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task CancelledProviderFetchRemainsRecoverable()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            page.OnGetItems = count =>
            {
                if (count == 2)
                {
                    throw new OperationCanceledException();
                }
            };
            page.ReplaceItems([CreateItem("Current")]);
            Assert.AreEqual(ListPageFetchPhase.Fetching, GetWorkState(viewModel).Phase);

            viewModel.SuspendForNavigation();
            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(3, page.GetItemsCount);
        }
        finally
        {
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [DataTestMethod]
    [DataRow("Fetching")]
    [DataRow("Committed")]
    [DataRow("Published")]
    [Timeout(15000)]
    public async Task RepeatedNavigationBeforeRecoveryRunsPreservesTheRequiredPhase(string phaseName)
    {
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var phase = Enum.Parse<ListPageFetchPhase>(phaseName);
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        Task? lockHolder = null;
        Task? firstResume = null;
        Task? secondResume = null;

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            if (phase == ListPageFetchPhase.Committed)
            {
                page.ReplaceItems([CreateItem("Current")]);
            }

            viewModel.SuspendForNavigation();
            if (phase == ListPageFetchPhase.Fetching)
            {
                page.ReplaceItems([CreateItem("Current")]);
            }

            lockHolder = Task.Run(() =>
            {
                using (GetPrivateField<Lock>(viewModel, "_initializationCoordinatorLock").EnterScope())
                using (GetPrivateField<Lock>(viewModel, "_fetchStateLock").EnterScope())
                {
                    held.Set();
                    Assert.IsTrue(release.Wait(TimeSpan.FromSeconds(5)));
                }
            });
            Assert.IsTrue(held.Wait(TimeSpan.FromSeconds(2)));

            firstResume = viewModel.ResumeAfterNavigation();
            viewModel.SuspendForNavigation();
            secondResume = viewModel.ResumeAfterNavigation();
            Assert.AreEqual(phase, GetWorkState(viewModel).Phase, "Queueing recovery must not consume it.");
            release.Set();
            await Task.WhenAll(lockHolder, firstResume, secondResume).WaitAsync(TimeSpan.FromSeconds(3));
            scheduler.Drain();

            Assert.AreEqual(phase == ListPageFetchPhase.Published ? 1 : 2, page.GetItemsCount);
            Assert.AreEqual(phase == ListPageFetchPhase.Published ? "Initial" : "Current", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(ListPageFetchPhase.Published, GetWorkState(viewModel).Phase);
        }
        finally
        {
            release.Set();
            foreach (var task in new[] { lockHolder, firstResume, secondResume })
            {
                if (task is not null)
                {
                    await task.WaitAsync(TimeSpan.FromSeconds(3));
                }
            }

            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ItemsChangedRacingResumeIsNotLost()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            for (var visit = 1; visit <= 30; visit++)
            {
                using var begin = new ManualResetEventSlim();
                var title = $"Visit {visit}";
                viewModel.SuspendForNavigation();
                page.ReplaceItems([CreateItem(title)], notify: false);
                var request = Task.Run(() =>
                {
                    Assert.IsTrue(begin.Wait(TimeSpan.FromSeconds(2)));
                    page.Refresh();
                });
                var resume = Task.Run(async () =>
                {
                    Assert.IsTrue(begin.Wait(TimeSpan.FromSeconds(2)));
                    await viewModel.ResumeAfterNavigation();
                });
                begin.Set();
                await Task.WhenAll(request, resume).WaitAsync(TimeSpan.FromSeconds(3));
                scheduler.Drain();
                Assert.AreEqual(title, viewModel.FilteredItems.Single().Title);
                Assert.AreEqual(visit + 1, page.GetItemsCount);
            }
        }
        finally
        {
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public void ReentrantPublicationDoesNotAdvanceThePhaseBeforeItsMutationRuns()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);
        var reentered = false;
        var publications = 0;
        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args) => publications++;
        void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (reentered)
            {
                return;
            }

            reentered = true;
            page.ReplaceItems([CreateItem("Inner")]);
            scheduler.Drain(); // Models WinUI pumping a queued callback during mutation.
            Assert.AreEqual(ListPageFetchPhase.Committed, GetWorkState(viewModel).Phase);
            Assert.AreEqual(0, publications, "A deferred mutation must not report successful publication.");
        }

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            viewModel.ItemsUpdated += OnItemsUpdated;
            viewModel.FilteredItems.CollectionChanged += OnCollectionChanged;
            page.ReplaceItems([CreateItem("Outer")]);
            scheduler.Drain();

            Assert.IsTrue(reentered);
            Assert.AreEqual("Inner", viewModel.FilteredItems.Single().Title);
            Assert.AreEqual(1, publications);
            Assert.AreEqual(ListPageFetchPhase.Published, GetWorkState(viewModel).Phase);
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
            viewModel.FilteredItems.CollectionChanged -= OnCollectionChanged;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task DuplicateSuspendedRequestsReuseTheirPendingRecord()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage();
        var viewModel = CreateViewModel(page, scheduler);

        try
        {
            viewModel.InitializeProperties();
            scheduler.Drain();
            viewModel.SuspendForNavigation();
            page.ReplaceItems([CreateItem("Current")]);
            var pending = GetWorkState(viewModel);
            for (var request = 0; request < 30; request++)
            {
                page.Refresh();
                Assert.AreSame(pending, GetWorkState(viewModel));
            }

            await viewModel.ResumeAfterNavigation();
            scheduler.Drain();
            Assert.AreEqual(2, page.GetItemsCount);
            Assert.AreEqual("Current", viewModel.FilteredItems.Single().Title);
        }
        finally
        {
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    private static ListPageWorkState GetWorkState(ListViewModel viewModel) =>
        GetPrivateField<ListPageWorkState>(viewModel, "_workState");

    private static async Task WaitForPublishedAsync(ListViewModel viewModel)
    {
        var elapsed = Stopwatch.StartNew();
        while (GetWorkState(viewModel).Phase != ListPageFetchPhase.Published)
        {
            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(3), "The fetch did not finish publishing.");
            await Task.Delay(1);
        }
    }

    private static ListItem CreateItem(string title, string glyph = SearchGlyph) =>
        new(new NoOpCommand { Name = title }) { Icon = new IconInfo(glyph) };

    private static ListViewModel CreateViewModel(IListPage page, TaskScheduler? scheduler = null) =>
        new(page, scheduler ?? TaskScheduler.Default, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);

    private static ShellViewModel CreateShell()
    {
        var hostService = new Mock<IAppHostService>();
        hostService.Setup(service => service.GetDefaultHost()).Returns(new TestHost());
        return new(TaskScheduler.Default, Mock.Of<IRootPageService>(), Mock.Of<IPageViewModelFactoryService>(), hostService.Object);
    }

    private static T GetPrivateField<T>(ListViewModel viewModel, string name) =>
        (T)(typeof(ListViewModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(viewModel)
            ?? throw new AssertFailedException($"Missing field {name}."));

    private static Task ObserveItemsAsync(ListViewModel viewModel, string expectedTitle, Action action) =>
        ObserveItemsAsync(viewModel, vm => vm.FilteredItems.Count == 1 && vm.FilteredItems[0].Title == expectedTitle, action);

    private static async Task ObserveItemsAsync(ListViewModel viewModel, Func<ListViewModel, bool> predicate, Action action)
    {
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args)
        {
            if (predicate(sender))
            {
                published.TrySetResult();
            }
        }

        viewModel.ItemsUpdated += OnItemsUpdated;
        try
        {
            action();
            await published.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
        }
    }
}
