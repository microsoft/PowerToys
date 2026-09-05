// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed partial class StaticPageFilteringTests
{
    private static readonly string[] AlphaTitles = ["Alpha"];
    private static readonly string[] BetaTitles = ["Beta"];
    private static readonly string[] GammaTitles = ["Gamma"];
    private static readonly string[] InitialTitles = ["Alpha", "Beta"];
    private static readonly string[] ReplacementTitles = ["Alpha replacement"];

    [TestMethod]
    public void QueryChanges_CoalesceBeforeScoringAndPublishOnlyOnUiScheduler()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"), Item("Gamma"));
        var original = fixture.ViewModel.FilteredItems.ToArray();
        var notifications = 0;
        fixture.ViewModel.FilteredItems.CollectionChanged += (_, _) =>
        {
            Assert.AreSame(fixture.Ui, TaskScheduler.Current);
            notifications++;
        };
        fixture.ViewModel.ItemsUpdated += (_, _) => Assert.AreSame(fixture.Ui, TaskScheduler.Current);

        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.ViewModel.SearchTextBox = "Gamma";

        Assert.AreEqual(1, fixture.Worker.Count);
        CollectionAssert.AreEqual(original, fixture.ViewModel.FilteredItems.ToArray());
        fixture.Worker.ExecuteAll();
        CollectionAssert.AreEqual(original, fixture.ViewModel.FilteredItems.ToArray());
        Assert.AreEqual(0, notifications);
        fixture.Ui.ExecuteAll();

        CollectionAssert.AreEqual(GammaTitles, fixture.Titles);
        Assert.AreEqual(1, fixture.Updates.Count);
        Assert.IsTrue(fixture.Updates[0].ForceFirstItem);
        Assert.IsTrue(fixture.Updates[0].EnsureSelectionVisible);
    }

    [TestMethod]
    public void CompletedQuery_BecomesStaleBeforePublication()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Worker.ExecuteAll();

        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.Ui.ExecuteAll();

        CollectionAssert.AreEqual(InitialTitles, fixture.Titles);
        Assert.AreEqual(0, fixture.Updates.Count);
        fixture.Drain();
        CollectionAssert.AreEqual(BetaTitles, fixture.Titles);
        Assert.AreEqual(1, fixture.Updates.Count);
    }

    [TestMethod]
    public void ItemRefresh_InvalidatesCompletedQueryAndRetainsSelectionIntent()
    {
        using var fixture = new Fixture(Item("Alpha"));
        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Worker.ExecuteAll();

        fixture.Page.SetItems(Item("Alpha replacement"));
        fixture.Page.Refresh();
        fixture.Page.Refresh(ListViewModel.IncrementalRefresh);
        fixture.Ui.ExecuteAll();

        Assert.AreEqual(0, fixture.Updates.Count);
        fixture.Drain();
        CollectionAssert.AreEqual(ReplacementTitles, fixture.Titles);
        Assert.AreEqual(1, fixture.Updates.Count);
        Assert.IsTrue(fixture.Updates[0].ForceFirstItem);
    }

    [TestMethod]
    public void QueryDuringFetch_WaitsForNewItems()
    {
        using var fixture = new Fixture(Item("Alpha"));
        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Worker.ExecuteAll();
        fixture.Page.SetItems(Item("Beta"));
        fixture.Page.BeforeGetItems = () =>
        {
            fixture.ViewModel.SearchTextBox = "Beta";
            fixture.Drain();
            Assert.AreEqual(0, fixture.Updates.Count);
            CollectionAssert.AreEqual(AlphaTitles, fixture.Titles);
        };

        fixture.Page.Refresh();
        fixture.Drain();

        CollectionAssert.AreEqual(BetaTitles, fixture.Titles);
        Assert.AreEqual(1, fixture.Updates.Count);
        Assert.IsTrue(fixture.Updates[0].ForceFirstItem);
    }

    [TestMethod]
    public void NestedPage_ResetsSelectionForQueryButNotRefresh()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        fixture.ViewModel.IsRootPage = false;
        fixture.Page.Refresh();
        fixture.Drain();
        Assert.IsFalse(fixture.Updates.Single().ForceFirstItem);
        fixture.Updates.Clear();

        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.Drain();

        Assert.IsTrue(fixture.Updates.Single().ForceFirstItem);
    }

    [TestMethod]
    public async Task DynamicPage_UsesProviderFilteringInsteadOfStaticWorker()
    {
        var ui = new QueuedScheduler();
        var worker = new QueuedScheduler();
        var page = new TestDynamicPage();
        using var viewModel = new ListViewModel(page, ui, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance, worker);
        try
        {
            viewModel.SearchTextBox = "Before initialization";
            viewModel.InitializeProperties();
            worker.ExecuteAll();
            ui.ExecuteAll();
            viewModel.SearchTextBox = "Alpha";

            var query = await page.SearchUpdated.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ui.ExecuteAll();

            Assert.AreEqual("Alpha", query);
            Assert.AreEqual(0, worker.Count);
            CollectionAssert.AreEqual(InitialTitles, viewModel.FilteredItems.Select(i => i.Title).ToArray());
        }
        finally
        {
            viewModel.SafeCleanup();
            ui.ExecuteAll();
        }
    }

    [TestMethod]
    public void ItemTextChange_RefiltersWithoutResettingSelection()
    {
        var item = Item("Alpha");
        using var fixture = new Fixture(item);
        var viewModel = fixture.ViewModel.FilteredItems[0];
        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.Drain();
        Assert.AreEqual(0, fixture.Titles.Length);
        fixture.Updates.Clear();

        item.Subtitle = "Beta";
        viewModel.ApplyPendingUpdates();
        fixture.Drain();

        CollectionAssert.AreEqual(AlphaTitles, fixture.Titles);
        Assert.AreEqual(1, fixture.Updates.Count);
        Assert.IsFalse(fixture.Updates[0].ForceFirstItem);
        Assert.IsFalse(fixture.Updates[0].EnsureSelectionVisible);

        item.Subtitle = string.Empty;
        item.Title = "Beta";
        viewModel.ApplyPendingUpdates();
        fixture.Drain();
        CollectionAssert.AreEqual(BetaTitles, fixture.Titles);
    }

    [TestMethod]
    public void ItemTextChange_InvalidatesAlreadyScoredSnapshot()
    {
        var item = Item("Alpha");
        using var fixture = new Fixture(item);
        var viewModel = fixture.ViewModel.FilteredItems[0];
        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Worker.ExecuteAll();

        item.Title = "Beta";
        viewModel.ApplyPendingUpdates();
        fixture.Ui.ExecuteAll();

        Assert.AreEqual(0, fixture.Updates.Count);
        fixture.Drain();
        Assert.AreEqual(0, fixture.Titles.Length);
        Assert.AreEqual(1, fixture.Updates.Count);
        Assert.IsTrue(fixture.Updates[0].ForceFirstItem);
    }

    [TestMethod]
    public async Task LateHydrationFailure_RemovesItemWithoutAnotherQuery()
    {
        using var release = new ManualResetEventSlim();
        var lateItem = new FailingHydrationItem(release) { Title = "Late item" };
        var items = Enumerable.Range(0, 20).Select(i => Item($"Item {i}")).Append(lateItem).ToArray();
        using var fixture = new Fixture(items);
        try
        {
            await lateItem.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(21, fixture.Titles.Length);

            release.Set();
            await fixture.Worker.WhenQueued.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Drain();

            Assert.AreEqual(20, fixture.Titles.Length);
            Assert.IsFalse(fixture.Titles.Contains("Error"));
            Assert.AreEqual(1, fixture.Updates.Count);
            Assert.IsFalse(fixture.Updates[0].ForceFirstItem);
        }
        finally
        {
            release.Set();
        }
    }

    [TestMethod]
    public void ReentrantPublication_DefersMutationsAndRejectsStaleDeferredResult()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"), Item("Gamma"));
        var reentered = false;
        var inNotification = false;
        fixture.ViewModel.FilteredItems.CollectionChanged += (_, _) =>
        {
            Assert.IsFalse(inNotification, "Collection mutations must not overlap during native renderer callbacks.");
            if (reentered)
            {
                return;
            }

            reentered = true;
            inNotification = true;
            try
            {
                fixture.ViewModel.SearchTextBox = "Beta";
                fixture.Drain();
                fixture.ViewModel.SearchTextBox = "Gamma";
            }
            finally
            {
                inNotification = false;
            }
        };

        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Drain();
        Assert.IsTrue(reentered);
        Assert.AreEqual(0, fixture.Updates.Count);
        fixture.Drain();

        CollectionAssert.AreEqual(GammaTitles, fixture.Titles);
        Assert.AreEqual(1, fixture.Updates.Count);
        Assert.IsTrue(fixture.Updates[0].ForceFirstItem);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Dispose_RejectsQueuedAndCompletedWork(bool completeScoring)
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        fixture.ViewModel.SearchTextBox = "Alpha";
        if (completeScoring)
        {
            fixture.Worker.ExecuteAll();
        }

        fixture.ViewModel.Dispose();
        fixture.ViewModel.SearchTextBox = "Beta";
        fixture.Page.Refresh();
        fixture.Drain();

        CollectionAssert.AreEqual(InitialTitles, fixture.Titles);
        Assert.AreEqual(0, fixture.Updates.Count);
        Assert.AreEqual(0, fixture.Worker.Count);
    }

    [TestMethod]
    public void CleanupDuringPublication_ClearsListAfterMutationAndDoesNotRepublish()
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Beta"));
        var cleaned = false;
        fixture.ViewModel.FilteredItems.CollectionChanged += (_, _) =>
        {
            if (!cleaned)
            {
                cleaned = true;
                fixture.ViewModel.SafeCleanup();
                fixture.Ui.ExecuteAll();
            }
        };

        fixture.ViewModel.SearchTextBox = "Alpha";
        fixture.Drain();

        Assert.IsTrue(cleaned);
        Assert.AreEqual(0, fixture.Titles.Length);
        Assert.AreEqual(0, fixture.Updates.Count);
        fixture.Page.Refresh();
        fixture.Drain();
        Assert.AreEqual(0, fixture.Titles.Length);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("alpha")]
    [DataRow("bt")]
    [DataRow("hidden")]
    [DataRow("no matches")]
    [DataRow(" ")]
    public void SnapshotScoring_PreservesLegacyScoresAndStableOrdering(string query)
    {
        using var fixture = new Fixture(Item("Alpha"), Item("Alpha"), Item("Beta"), Item(string.Empty), Item("Hidden"));
        var viewModels = fixture.ViewModel.FilteredItems.ToArray();
        ListViewModel.StaticFilterItem[] snapshot =
        [
            new(viewModels[0], "Alpha", "Beta", false),
            new(viewModels[1], "Alpha", "Beta", false),
            new(viewModels[2], "Beta", "Alpha", false),
            new(viewModels[3], string.Empty, string.Empty, false),
            new(viewModels[4], "Hidden", "Alpha", true),
        ];
        var expected = snapshot.Where(i => !i.IsInErrorState)
            .Select(i => (i.ViewModel, Score: string.IsNullOrEmpty(query) ? 1 : new[]
            {
                FuzzyStringMatcher.ScoreFuzzy(query, i.Title),
                (FuzzyStringMatcher.ScoreFuzzy(query, i.Subtitle) - 4) / 2,
                0,
            }.Max()))
            .Where(i => i.Score > 0)
            .OrderByDescending(i => i.Score)
            .Select(i => i.ViewModel)
            .ToArray();

        var actual = ListViewModel.FilterSnapshot(snapshot, query, CancellationToken.None);

        CollectionAssert.AreEqual(expected, actual);
        if (query is "" or "alpha")
        {
            Assert.AreSame(viewModels[0], actual[0]);
            Assert.AreSame(viewModels[1], actual[1]);
        }
    }

    [TestMethod]
    public void SnapshotScoring_DoesNotRereadMutableViewModels()
    {
        var item = Item("Alpha");
        using var fixture = new Fixture(item);
        var viewModel = fixture.ViewModel.FilteredItems[0];
        var snapshot = new[] { ListViewModel.StaticFilterItem.Capture(viewModel) };

        item.Title = "Beta";
        viewModel.SafeCleanup();
        var actual = ListViewModel.FilterSnapshot(snapshot, "Alpha", CancellationToken.None);

        Assert.AreEqual("Beta", viewModel.Title);
        Assert.AreEqual(1, actual.Length);
        Assert.AreSame(viewModel, actual[0]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ListViewModel.FilterSnapshot(snapshot, "Alpha", cancellation.Token));
    }

    private static ListItem Item(string title) => new(new NoOpCommand()) { Title = title };

    private sealed class Fixture : IDisposable
    {
        internal QueuedScheduler Ui { get; } = new();

        internal QueuedScheduler Worker { get; } = new();

        internal TestPage Page { get; }

        internal ListViewModel ViewModel { get; }

        internal List<ItemsUpdatedEventArgs> Updates { get; } = [];

        internal string[] Titles => ViewModel.FilteredItems.Select(i => i.Title).ToArray();

        internal Fixture(params ListItem[] items)
        {
            Page = new(items);
            ViewModel = new(Page, Ui, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance, Worker);
            ViewModel.InitializeProperties();
            Drain();
            ViewModel.ItemsUpdated += (_, args) => Updates.Add(args);
        }

        internal void Drain()
        {
            Worker.ExecuteAll();
            Ui.ExecuteAll();
        }

        public void Dispose()
        {
            ViewModel.SafeCleanup();
            Drain();
        }
    }

    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Static filter tests";
    }

    private sealed partial class TestPage(IListItem[] items) : ListPage
    {
        private IListItem[] _items = items;

        internal Action? BeforeGetItems { get; set; }

        public override IListItem[] GetItems()
        {
            BeforeGetItems?.Invoke();
            return _items;
        }

        internal void SetItems(params IListItem[] items) => _items = items;

        internal void Refresh(int totalItems = 0) => RaiseItemsChanged(totalItems);
    }

    private sealed partial class TestDynamicPage : DynamicListPage
    {
        internal TaskCompletionSource<string> SearchUpdated { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override IListItem[] GetItems() => [Item("Alpha"), Item("Beta")];

        public override void UpdateSearchText(string oldSearch, string newSearch) => SearchUpdated.TrySetResult(newSearch);
    }

    private sealed partial class FailingHydrationItem(ManualResetEventSlim release) : ListItem(new NoOpCommand())
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ITag[] Tags
        {
            get
            {
                Entered.TrySetResult();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("Hydration was not released.");
                }

                throw new InvalidOperationException("The extension failed to hydrate this item.");
            }

            set => throw new NotSupportedException();
        }
    }

    private sealed class QueuedScheduler : TaskScheduler
    {
        private readonly Lock _gate = new();
        private readonly Queue<Task> _tasks = [];
        private TaskCompletionSource _queued = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int Count
        {
            get
            {
                lock (_gate)
                {
                    return _tasks.Count;
                }
            }
        }

        internal Task WhenQueued
        {
            get
            {
                lock (_gate)
                {
                    return _tasks.Count > 0 ? Task.CompletedTask : _queued.Task;
                }
            }
        }

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_gate)
            {
                return _tasks.ToArray();
            }
        }

        protected override void QueueTask(Task task)
        {
            lock (_gate)
            {
                _tasks.Enqueue(task);
                _queued.TrySetResult();
            }
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        internal void ExecuteAll()
        {
            while (true)
            {
                Task task;
                lock (_gate)
                {
                    if (!_tasks.TryDequeue(out task!))
                    {
                        return;
                    }

                    if (_tasks.Count == 0)
                    {
                        _queued = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }

                Assert.IsTrue(TryExecuteTask(task));
                task.GetAwaiter().GetResult();
            }
        }
    }
}
