// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Graphics;

namespace Microsoft.CmdPal.UI.ManualTests;

[TestClass]
[DoNotParallelize]
[TestCategory("Manual")]
public sealed partial class ManualListItemsViewTests
{
    private static DispatcherQueue _dispatcher = null!;
    private static Window _window = null!;

    [ClassInitialize]
    public static async Task Initialize(TestContext context)
    {
        var ready = new TaskCompletionSource<DispatcherQueue>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                WinRT.ComWrappersSupport.InitializeComWrappers();
                Application.Start(args =>
                {
                    var dispatcher = DispatcherQueue.GetForCurrentThread();
                    SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcher));
                    try
                    {
                        _ = new TestApp();
                    }
                    catch (Exception ex)
                    {
                        ready.TrySetException(ex);
                        return;
                    }

                    _window = new Window { Title = "CmdPal manual selection checks" };
                    _window.AppWindow.Resize(new SizeInt32(760, 450));
                    _window.Activate();
                    ready.SetResult(dispatcher);
                });
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        _dispatcher = await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [ClassCleanup]
    public static Task Cleanup() => _dispatcher is null ? Task.CompletedTask : OnUiThread(() =>
    {
        _window.Content = null;
        _window.AppWindow.Hide();
        return Task.CompletedTask;
    });

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task FullResetThenLoadMoreKeepsSelectionAndScroll(bool isGrid, bool isRootPage) => OnUiThread(async () =>
    {
        using var fixture = await ShowAsync(isGrid, isRootPage);
        var rows = fixture.Page.Rows;
        await Task.Run(() => fixture.Page.Replace([rows[1], rows[0], .. rows.Skip(2)]));
        fixture.Scheduler.Drain();
        await WaitUntilAsync(() => fixture.Items.SelectedItem is ListItemViewModel { Title: "Item 1" }, fixture.Scheduler);
        await FlushDispatcherAsync();
        var selected = fixture.Items.SelectedItem;
        fixture.Items.UpdateLayout();
        var scroll = FindScrollViewer(fixture.Items)!;
        Assert.IsTrue(scroll.ScrollableHeight > 300);
        var scrollRequested = scroll.ChangeView(null, 300, null, disableAnimation: true);
        await WaitUntilAsync(
            () => scroll.VerticalOffset > 200,
            fixture.Scheduler,
            () => $"Scroll request accepted: {scrollRequested}; offset: {scroll.VerticalOffset}; height: {scroll.ScrollableHeight}; viewport: {scroll.ViewportHeight}.");
        await FlushDispatcherAsync();
        var offset = scroll.VerticalOffset;
        Assert.IsTrue(offset > 200, "The control must remain scrolled before LoadMore starts.");

        fixture.Page.OnLoadMore = () => fixture.Page.Replace(
            [rows[2], rows[1], rows[0], .. rows.Skip(3), CreateItem("More")], ListViewModel.IncrementalRefresh);
        fixture.Page.HasMoreItems = true;
        fixture.ViewModel.LoadMoreIfNeeded();
        await WaitUntilAsync(() => fixture.Items.Items.Count == rows.Length + 1, fixture.Scheduler);
        await FlushDispatcherAsync();

        Assert.AreSame(selected, fixture.Items.SelectedItem, "LoadMore must not reuse a consumed first-result reset.");
        Assert.AreSame(selected, GetField<ListItemViewModel>(fixture.ViewModel, "_lastSelectedItem"));
        Assert.AreEqual(offset, scroll.VerticalOffset, 1, "LoadMore must not scroll a consumed reset back to the top.");
    });

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task HomeFollowsLateFirstResultsUntilUserSelection(bool isGrid) => OnUiThread(async () =>
    {
        using var fixture = await ShowAsync(isGrid, isRootPage: true, isMainPage: true);
        var rows = fixture.Page.Rows;
        await Task.Run(() => fixture.Page.Replace([rows[1], rows[0], .. rows.Skip(2)]));
        fixture.Scheduler.Drain();
        await Task.Run(() => fixture.Page.Replace([rows[2], rows[1], rows[0], .. rows.Skip(3)], ListViewModel.IncrementalRefresh));
        fixture.Scheduler.Drain();
        await FlushDispatcherAsync();
        Assert.AreEqual("Item 2", ((ListItemViewModel)fixture.Items.SelectedItem).Title);

        var later = fixture.ViewModel.FilteredItems[8];
        fixture.Items.SelectedItem = later;
        await Task.Run(() => fixture.Page.Replace(rows, ListViewModel.IncrementalRefresh));
        fixture.Scheduler.Drain();
        await FlushDispatcherAsync();
        Assert.AreSame(later, fixture.Items.SelectedItem, "Explicit selection must end Home's first-result tracking.");
    });

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task ForcedRefreshAcknowledgesDirectSelectionWithoutReplayingEffects(bool isGrid) => OnUiThread(async () =>
    {
        using var fixture = await ShowAsync(isGrid, isRootPage: true);
        var recipient = new object();
        var commandBars = 0;
        var suggestions = 0;
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(recipient, (_, _) => commandBars++);
        WeakReferenceMessenger.Default.Register<UpdateSuggestionMessage>(recipient, (_, _) => suggestions++);
        try
        {
            var later = fixture.ViewModel.FilteredItems[1];
            fixture.Items.SelectedItem = later;
            await WaitUntilAsync(() => suggestions > 0, fixture.Scheduler);
            Assert.AreSame(later, GetField<ListItemViewModel>(fixture.ViewModel, "_lastSelectedItem"));
            Assert.AreNotSame(later, GetField<ListItemViewModel>(fixture.View, "_lastPushedToVm"), "Native SelectionChanged must leave the push cache stale for this regression.");
            var selectionWork = GetField<CancellationTokenSource>(fixture.ViewModel, "_selectedItemCts");
            var barsBefore = commandBars;
            var suggestionsBefore = suggestions;
            var rows = fixture.Page.Rows;
            await Task.Run(() => fixture.Page.Replace([rows[1], rows[0], .. rows.Skip(2)]));
            fixture.Scheduler.Drain();
            await FlushDispatcherAsync();

            Assert.AreSame(later, fixture.Items.SelectedItem);
            Assert.AreSame(selectionWork, GetField<CancellationTokenSource>(fixture.ViewModel, "_selectedItemCts"));
            Assert.AreEqual(barsBefore, commandBars);
            Assert.AreEqual(suggestionsBefore, suggestions);
            Assert.IsFalse(GetField<bool>(fixture.ViewModel, "_awaitingFirstSelection"));
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
        }
    });

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public Task TemporarilyUnboundItemsCanClearAndReselectTheSameItem(bool isGrid) => OnUiThread(async () =>
    {
        using var fixture = await ShowAsync(isGrid);
        var selected = fixture.Items.SelectedItem;
        var source = fixture.Items.ItemsSource;
        fixture.Items.ItemsSource = null;
        fixture.View.EnsureInitialSelection();
        await FlushDispatcherAsync();
        Assert.IsNull(GetField<ListItemViewModel?>(fixture.ViewModel, "_lastSelectedItem"));
        Assert.IsNull(GetField<ListItemViewModel?>(fixture.View, "_lastPushedToVm"));

        fixture.Items.ItemsSource = source;
        fixture.View.EnsureInitialSelection();
        await FlushDispatcherAsync();
        Assert.AreSame(selected, fixture.Items.SelectedItem);
        Assert.AreSame(selected, GetField<ListItemViewModel>(fixture.ViewModel, "_lastSelectedItem"));
    });

    [TestMethod]
    public Task UserSelectionCancelsAQueuedGridReset() => OnUiThread(async () =>
    {
        using var fixture = await ShowAsync(isGrid: true);
        var reentered = false;
        void OnGroupChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            if (!reentered)
            {
                reentered = true;

                // A reentrant source change leaves the real grouped projection pending.
                fixture.ViewModel.FilteredItems.Move(0, 1);
            }
        }

        var group = fixture.View.GridItems.Groups[0];
        group.Items.CollectionChanged += OnGroupChanged;
        try
        {
            var later = fixture.ViewModel.FilteredItems[8];
            var rows = fixture.Page.Rows;
            await Task.Run(() => fixture.Page.Replace([rows[1], rows[0], .. rows.Skip(2)]));
            fixture.Scheduler.Drain();
            Assert.IsTrue(reentered);
            Assert.IsTrue(fixture.View.GridItems.HasPendingChanges);
            Assert.IsTrue(GetField<bool>(fixture.View, "_forceFirstPending"));

            fixture.Items.SelectedItem = later;
            Assert.IsFalse(GetField<bool>(fixture.View, "_forceFirstPending"));
            await FlushDispatcherAsync();
            Assert.IsFalse(fixture.View.GridItems.HasPendingChanges);
            Assert.AreSame(later, fixture.Items.SelectedItem);
            Assert.AreSame(later, GetField<ListItemViewModel>(fixture.ViewModel, "_lastSelectedItem"));
        }
        finally
        {
            group.Items.CollectionChanged -= OnGroupChanged;
        }
    });

    [DataTestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public Task BackRestoresFirstSelectionDuringDeferredOrInterruptedPublication(bool isGrid, bool interruptPublication) => OnUiThread(async () =>
    {
        var page = new TestPage(isGrid);
        var scheduler = new QueuedTaskScheduler();
        var viewModel = CreateViewModel(page, scheduler);
        var hosts = new Mock<IAppHostService>();
        hosts.Setup(service => service.GetDefaultHost()).Returns(new TestHost());
        using var shell = new ShellViewModel(scheduler, Mock.Of<IRootPageService>(), Mock.Of<IPageViewModelFactoryService>(), hosts.Object);
        var frame = new Frame();
        frame.Navigated += (_, args) => shell.CurrentPage =
            (args.Parameter as AsyncNavigationRequest)?.TargetViewModel as PageViewModel ?? shell.NullPage;
        var interruptNext = false;
        void OnPublished(ListViewModel sender, ItemsUpdatedEventArgs args)
        {
            if (interruptNext)
            {
                interruptNext = false;
                frame.Navigate(typeof(Microsoft.UI.Xaml.Controls.Page));
            }
        }

        // Register before the view so navigation can interrupt a committed publication
        // before its deferred selection reaches the control.
        viewModel.ItemsUpdated += OnPublished;
        _window.Content = frame;
        try
        {
            frame.Navigate(typeof(Microsoft.CmdPal.UI.ListPage), new AsyncNavigationRequest(viewModel, CancellationToken.None));
            await Task.Run(viewModel.InitializeProperties);
            var original = (ListItemsView)((Microsoft.CmdPal.UI.ListPage)frame.Content).FindName("ListView");
            await WaitUntilAsync(() => ItemView(original).SelectedItem is not null, scheduler);
            await FlushDispatcherAsync();
            var rows = page.Rows;
            interruptNext = interruptPublication;
            await Task.Run(() => page.Replace([rows[1], rows[0], .. rows.Skip(2)]));
            if (interruptPublication)
            {
                scheduler.Drain();
                Assert.IsFalse(interruptNext);
                Assert.IsTrue(GetField<bool>(viewModel, "_awaitingFirstSelection"));
            }
            else
            {
                frame.Navigate(typeof(Microsoft.UI.Xaml.Controls.Page));
            }

            Assert.IsNull(original.ViewModel);
            Assert.IsTrue(frame.CanGoBack);
            frame.GoBack();
            var restored = (ListItemsView)((Microsoft.CmdPal.UI.ListPage)frame.Content).FindName("ListView");
            Assert.AreNotSame(original, restored);
            await WaitUntilAsync(
                () => IsPublished(viewModel) && ItemView(restored).SelectedItem is ListItemViewModel { Title: "Item 1" },
                scheduler);
            await FlushDispatcherAsync();

            Assert.AreSame(viewModel.FilteredItems[0], ItemView(restored).SelectedItem);
            Assert.AreSame(ItemView(restored).SelectedItem, GetField<ListItemViewModel>(viewModel, "_lastSelectedItem"));
            Assert.AreEqual(2, page.GetItemsCount, "Back should republish the committed snapshot without another extension fetch.");
        }
        finally
        {
            viewModel.ItemsUpdated -= OnPublished;
            _window.Content = null;
            WeakReferenceMessenger.Default.UnregisterAll(shell);
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    });

    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Manual selection checks";
    }

    private sealed partial class TestPage : DynamicListPage
    {
        private IListItem[] _items;
        private int _getItemsCount;

        internal TestPage(bool isGrid)
        {
            Rows = Enumerable.Range(0, 80).Select(index => CreateItem($"Item {index}")).ToArray();
            _items = Rows;
            GridProperties = isGrid ? new MediumGridLayout() : null;
        }

        internal ListItem[] Rows { get; }

        internal int GetItemsCount => Volatile.Read(ref _getItemsCount);

        internal Action? OnLoadMore { get; set; }

        public override IListItem[] GetItems()
        {
            Interlocked.Increment(ref _getItemsCount);
            return Volatile.Read(ref _items);
        }

        public override void UpdateSearchText(string oldSearch, string newSearch) => Replace(_items);

        public override void LoadMore()
        {
            HasMoreItems = false;
            OnLoadMore?.Invoke();
        }

        internal void Replace(IListItem[] items, int? totalItems = null)
        {
            Volatile.Write(ref _items, items);
            RaiseItemsChanged(totalItems ?? items.Length);
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
    }

    private sealed class Fixture(TestPage page, QueuedTaskScheduler scheduler, ListViewModel viewModel, ListItemsView view) : IDisposable
    {
        internal TestPage Page => page;

        internal QueuedTaskScheduler Scheduler => scheduler;

        internal ListViewModel ViewModel => viewModel;

        internal ListItemsView View => view;

        internal ListViewBase Items => ItemView(view);

        public void Dispose()
        {
            view.ViewModel = null;
            _window.Content = null;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }
    }

    private static ListItem CreateItem(string title) => new(new NoOpCommand { Name = title }) { TextToSuggest = title + " suggestion" };

    private static ListViewModel CreateViewModel(TestPage page, QueuedTaskScheduler scheduler, bool isRootPage = false, bool isMainPage = false) =>
        new(page, scheduler, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance)
        {
            IsRootPage = isRootPage,
            IsMainPage = isMainPage,
        };

    private static async Task<Fixture> ShowAsync(bool isGrid, bool isRootPage = false, bool isMainPage = false)
    {
        var page = new TestPage(isGrid);
        var scheduler = new QueuedTaskScheduler();
        var viewModel = CreateViewModel(page, scheduler, isRootPage, isMainPage);
        var view = new ListItemsView { ViewModel = viewModel };
        var fixture = new Fixture(page, scheduler, viewModel, view);
        try
        {
            _window.Content = view;
            await Task.Run(viewModel.InitializeProperties);
            await WaitUntilAsync(() => view.IsLoaded && fixture.Items.Items.Count == page.Rows.Length && fixture.Items.SelectedItem is not null, scheduler);
            await FlushDispatcherAsync();
            return fixture;
        }
        catch
        {
            fixture.Dispose();
            throw;
        }
    }

    private static ListViewBase ItemView(ListItemsView view) => (ListViewBase)view.FindName(view.ViewModel?.IsGridView == true ? "ItemsGrid" : "ItemsList");

    private static T GetField<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;

    private static bool IsPublished(ListViewModel viewModel) =>
        GetField<object>(viewModel, "_workState").GetType().GetProperty("Phase")!.GetValue(GetField<object>(viewModel, "_workState"))!.ToString() == "Published";

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer scroll)
        {
            return scroll;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            if (FindScrollViewer(VisualTreeHelper.GetChild(root, index)) is { } child)
            {
                return child;
            }
        }

        return null;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, QueuedTaskScheduler scheduler, Func<string>? failureMessage = null)
    {
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            scheduler.Drain();
            if (condition())
            {
                return;
            }

            Assert.IsTrue(elapsed.Elapsed < TimeSpan.FromSeconds(5), failureMessage?.Invoke() ?? "Native selection did not settle.");
            await Task.Delay(10);
        }
    }

    private static async Task FlushDispatcherAsync()
    {
        for (var pass = 0; pass < 2; pass++)
        {
            var flushed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.IsTrue(_dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => flushed.SetResult()));
            await flushed.Task;
        }
    }

    private static async Task OnUiThread(Func<Task> action)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(_dispatcher.TryEnqueue(async () =>
        {
            try
            {
                await action();
                finished.SetResult();
            }
            catch (Exception ex)
            {
                finished.SetException(ex);
            }
        }));
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
