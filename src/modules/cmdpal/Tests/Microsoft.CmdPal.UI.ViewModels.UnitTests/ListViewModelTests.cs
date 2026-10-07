// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
[DoNotParallelize]
public partial class ListViewModelTests
{
    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class RecursiveItemsChangedPage : ListPage
    {
        private int _getItemsCallCount;
        private int _recursiveItemsChangedRaised;
        private TaskCompletionSource<bool> _deferredFetchObserved = NewDeferredFetchObserved();

        public int GetItemsCallCount => Volatile.Read(ref _getItemsCallCount);

        public Task DeferredFetchObserved => _deferredFetchObserved.Task;

        public bool RaiseItemsChangedDuringGetItems { get; set; }

        public override IListItem[] GetItems()
        {
            var callCount = Interlocked.Increment(ref _getItemsCallCount);
            if (callCount >= 2)
            {
                _deferredFetchObserved.TrySetResult(true);
            }

            if (RaiseItemsChangedDuringGetItems && Interlocked.Exchange(ref _recursiveItemsChangedRaised, 1) == 0)
            {
                RaiseItemsChanged(0);
            }

            return [new ListItem(new NoOpCommand() { Name = $"Item {callCount}" })];
        }

        public void PrepareRecursiveFetch()
        {
            Volatile.Write(ref _getItemsCallCount, 0);
            Volatile.Write(ref _recursiveItemsChangedRaised, 0);
            RaiseItemsChangedDuringGetItems = true;
            _deferredFetchObserved = NewDeferredFetchObserved();
        }

        public void TriggerItemsChanged(int totalItems = 0) => RaiseItemsChanged(totalItems);

        private static TaskCompletionSource<bool> NewDeferredFetchObserved() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed partial class IncrementalLoadingPage : ListPage
    {
        private IListItem[] _items = [new ListItem(new NoOpCommand() { Name = "Item 1" })];

        public override IListItem[] GetItems() => _items;

        public override void LoadMore()
        {
            _items = [.. _items, new ListItem(new NoOpCommand() { Name = "Item 2" })];
            HasMoreItems = false;
            RaiseItemsChanged(_items.Length);
        }

        public void TriggerItemsChanged(int totalItems) => RaiseItemsChanged(totalItems);
    }

    private sealed partial class LaunchFilters : Filters
    {
        public LaunchFilters()
        {
            CurrentFilterId = "all";
        }

        public override IFilterItem[] GetFilters() =>
        [
            new Filter() { Id = "all", Name = "All" },
            new Separator(),
            new Filter() { Id = "running", Name = "Running" },
        ];
    }

    private sealed partial class SearchableStaticPage : ListPage
    {
        public int GetItemsCallCount { get; private set; }

        public string? FilterAtFirstGetItems { get; private set; }

        public SearchableStaticPage()
        {
            Filters = new LaunchFilters();
        }

        public override IListItem[] GetItems()
        {
            GetItemsCallCount++;
            FilterAtFirstGetItems ??= Filters?.CurrentFilterId;
            return
            [
                new ListItem(new NoOpCommand() { Name = "SSH server" }),
                new ListItem(new NoOpCommand() { Name = "Calculator" }),
            ];
        }
    }

    private sealed partial class SearchableDynamicPage : DynamicListPage
    {
        public int GetItemsCallCount { get; private set; }

        public string? SearchAtFirstGetItems { get; private set; }

        public string? FilterAtFirstGetItems { get; private set; }

        public string? FilterWhenSearchChanged { get; private set; }

        public IListItem[] Items { get; set; } = [new ListItem(new NoOpCommand() { Name = "Result" })];

        public SearchableDynamicPage()
        {
            Filters = new LaunchFilters();
        }

        public override void UpdateSearchText(string oldSearch, string newSearch)
        {
            FilterWhenSearchChanged = Filters?.CurrentFilterId;
            RaiseItemsChanged(Items.Length);
        }

        public override IListItem[] GetItems()
        {
            GetItemsCallCount++;
            SearchAtFirstGetItems ??= SearchText;
            FilterAtFirstGetItems ??= Filters?.CurrentFilterId;
            return Items;
        }

        public void TriggerItemsChanged(int totalItems) => RaiseItemsChanged(totalItems);
    }

    private static ListViewModel CreateViewModel(IListPage page) =>
        new(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);

    [TestMethod]
    public async Task StaticListPageLaunchOptions_ApplyFilterBeforeFetchAndHostQuery()
    {
        var page = new SearchableStaticPage
        {
            Id = "static.list.page",
            Name = "Static List Page",
            Title = "Static List Page",
        };
        var viewModel = CreateViewModel(page);
        viewModel.SetLaunchOptions(new(Query: "ssh", FilterId: "running"));

        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);

            Assert.AreEqual(1, page.GetItemsCallCount);
            Assert.AreEqual("running", page.FilterAtFirstGetItems);
            Assert.AreEqual("ssh", viewModel.SearchTextBox);
            Assert.AreEqual("ssh", viewModel.InitialSearchText);
            Assert.AreEqual(1, viewModel.FilteredItems.Count);
            Assert.AreEqual("SSH server", viewModel.FilteredItems[0].Title);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    public async Task DynamicListPageLaunchOptions_ReachProviderBeforeFirstFetch()
    {
        var page = new SearchableDynamicPage
        {
            Id = "dynamic.list.page",
            Name = "Dynamic List Page",
            Title = "Dynamic List Page",
        };
        var viewModel = CreateViewModel(page);
        viewModel.SetLaunchOptions(new(Query: "ssh", FilterId: "running"));

        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);

            Assert.AreEqual(1, page.GetItemsCallCount);
            Assert.AreEqual("running", page.FilterWhenSearchChanged);
            Assert.AreEqual("running", page.FilterAtFirstGetItems);
            Assert.AreEqual("ssh", page.SearchAtFirstGetItems);
            Assert.AreEqual("ssh", viewModel.SearchTextBox);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [DataTestMethod]
    [DataRow(false, -1, false, true)]
    [DataRow(false, 1, false, true)]
    [DataRow(false, 2, false, false)]
    [DataRow(false, 1, true, false)]
    [DataRow(false, 2, true, false)]
    [DataRow(false, -1, true, false)]
    [DataRow(true, 2, false, true)]
    [DataRow(true, 1, true, false)]
    public async Task ReorderedStableResults_PreserveSelectionIntent(bool isRootPage, int selectedIndex, bool incremental, bool forceFirst)
    {
        var section = new Separator("Apps");
        var winaero = new ListItem(new NoOpCommand() { Name = "Winaero" });
        var word = new ListItem(new NoOpCommand() { Name = "Word" });
        var page = new SearchableDynamicPage { Items = [section, winaero, word] };
        var viewModel = CreateViewModel(page);
        viewModel.IsRootPage = isRootPage;

        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);
            var originalWinaero = viewModel.FilteredItems[1];
            var originalWord = viewModel.FilteredItems[2];
            viewModel.UpdateSelectedItemCommand.Execute(selectedIndex < 0 ? null : viewModel.FilteredItems[selectedIndex]);
            page.Items = [section, word, winaero];

            var update = await ObserveNextItemsUpdateAsync(viewModel, () =>
            {
                if (incremental)
                {
                    page.TriggerItemsChanged(ListViewModel.IncrementalRefresh);
                }
                else
                {
                    viewModel.SearchTextBox = "winword";
                }
            });

            Assert.AreSame(originalWord, viewModel.FilteredItems[1]);
            Assert.AreSame(originalWinaero, viewModel.FilteredItems[2]);
            Assert.AreEqual(forceFirst, update.ForceFirstItem);

            if (incremental)
            {
                // Moving a selected row can change its position without changing selection.
                page.Items = [section, winaero, word];
                var nextUpdate = await ObserveNextItemsUpdateAsync(viewModel, () => viewModel.SearchTextBox = "winword");
                Assert.AreEqual(isRootPage || selectedIndex < 0 || selectedIndex == 2, nextUpdate.ForceFirstItem);
            }
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false, 0, true)]
    [DataRow(false, 1, false)]
    [DataRow(true, 0, false)]
    [DataRow(true, 1, false)]
    [DataRow(true, 2, true)]
    public async Task SubpageRefreshWithSectionCommands_ResetsOnlyFromDefaultSelection(bool hasOrdinaryItem, int selectedIndex, bool forceFirst)
    {
        var recent = new Separator("Recent", new NoOpCommand { Name = "Open recent" });
        var favorites = new Separator("Favorites", new NoOpCommand { Name = "Open favorites" });
        var page = new SearchableDynamicPage
        {
            Items = hasOrdinaryItem
                ? [recent, favorites, new ListItem(new NoOpCommand { Name = "Open item" })]
                : [recent, favorites],
        };
        var viewModel = CreateViewModel(page);
        viewModel.IsRootPage = false;

        try
        {
            await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);
            viewModel.AcknowledgeSelection(viewModel.FilteredItems[selectedIndex]);

            var update = await ObserveNextItemsUpdateAsync(viewModel, () => page.TriggerItemsChanged(0));

            Assert.AreEqual(forceFirst, update.ForceFirstItem);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    public async Task UnknownFilter_ShowsPageErrorAndPreventsInitialFetch()
    {
        var page = new SearchableStaticPage
        {
            Id = "static.list.page",
            Name = "Static List Page",
            Title = "Static List Page",
        };
        var viewModel = CreateViewModel(page);
        viewModel.SetLaunchOptions(new(FilterId: "RUNNING"));
        var errorObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PageViewModel.ErrorMessage) &&
                !string.IsNullOrEmpty(viewModel.ErrorMessage))
            {
                errorObserved.TrySetResult();
            }
        };

        try
        {
            viewModel.InitializeProperties();

            var completed = await Task.WhenAny(errorObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(errorObserved.Task, completed);
            Assert.IsFalse(string.IsNullOrWhiteSpace(viewModel.ErrorMessage));
            Assert.AreEqual(0, page.GetItemsCallCount);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InvokeItem_VisiblePrimaryCommand_SendsCommandWithItemContext(bool sectionHeader)
    {
        var command = new NoOpCommand { Name = "Open" };
        IListItem item = sectionHeader ? new Separator("Recent", command) : new ListItem(command);

        var messages = InvokeItemAndCaptureMessages(item);

        Assert.AreEqual(1, messages.Count);
        Assert.AreSame(command, messages[0].Command.Unsafe);
        Assert.AreSame(item, messages[0].CommandContext);
    }

    [TestMethod]
    public void InvokeItem_UnnamedPrimaryCommand_SendsCommandWithItemContext()
    {
        var command = new NoOpCommand { Name = string.Empty };
        var item = new ListItem(command) { Title = "Open an unnamed command" };

        var messages = InvokeItemAndCaptureMessages(item);

        Assert.AreEqual(1, messages.Count);
        Assert.AreSame(command, messages[0].Command.Unsafe);
        Assert.AreSame(item, messages[0].CommandContext);
    }

    [TestMethod]
    public void InvokeItem_UnnamedSectionCommand_DoesNotSendCommand()
    {
        var item = new Separator("Recent", new NoOpCommand { Name = string.Empty });

        var messages = InvokeItemAndCaptureMessages(item);

        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("Recent")]
    public void InvokeItem_StructuralRowWithoutCommand_DoesNotSendCommand(string section)
    {
        var messages = InvokeItemAndCaptureMessages(new Separator(section));

        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public async Task RecursiveItemsChangedDuringGetItems_IsDeferredUntilGetItemsReturns()
    {
        var page = new RecursiveItemsChangedPage
        {
            Id = "list.page",
            Name = "List Page",
            Title = "List Page",
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();
        page.PrepareRecursiveFetch();

        page.TriggerItemsChanged();

        var completed = await Task.WhenAny(page.DeferredFetchObserved, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(page.DeferredFetchObserved, completed);
        Assert.AreEqual(2, page.GetItemsCallCount);

        viewModel.SafeCleanup();
        viewModel.Dispose();
    }

    [TestMethod]
    public async Task LoadMoreItemsChanged_PreservesSelectionImplicitly()
    {
        var page = new IncrementalLoadingPage
        {
            Id = "list.page",
            Name = "List Page",
            Title = "List Page",
            HasMoreItems = true,
        };

        var viewModel = CreateViewModel(page);
        try
        {
            var initialUpdate = await ObserveNextItemsUpdateAsync(viewModel, viewModel.InitializeProperties);
            Assert.IsFalse(initialUpdate.ForceFirstItem);
            Assert.IsTrue(initialUpdate.EnsureSelectionVisible);

            var regularUpdate = await ObserveNextItemsUpdateAsync(viewModel, () => page.TriggerItemsChanged(1));
            Assert.IsTrue(regularUpdate.ForceFirstItem);
            Assert.IsTrue(regularUpdate.EnsureSelectionVisible);

            var explicitIncrementalUpdate = await ObserveNextItemsUpdateAsync(
                viewModel,
                () => page.TriggerItemsChanged(ListViewModel.IncrementalRefresh));
            Assert.IsFalse(explicitIncrementalUpdate.ForceFirstItem);
            Assert.IsTrue(explicitIncrementalUpdate.EnsureSelectionVisible);

            var loadMoreUpdate = await ObserveNextItemsUpdateAsync(viewModel, viewModel.LoadMoreIfNeeded);
            Assert.IsFalse(loadMoreUpdate.ForceFirstItem);
            Assert.IsFalse(loadMoreUpdate.EnsureSelectionVisible);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    private static List<PerformCommandMessage> InvokeItemAndCaptureMessages(IListItem item)
    {
        var listViewModel = CreateViewModel(new RecursiveItemsChangedPage());
        var itemViewModel = new ListItemViewModel(item, listViewModel.PageContext, DefaultContextMenuFactory.Instance);
        var messages = new List<PerformCommandMessage>();
        WeakReferenceMessenger.Default.Register<List<PerformCommandMessage>, PerformCommandMessage>(messages, static (recipient, message) => recipient.Add(message));

        try
        {
            itemViewModel.InitializeProperties();
            listViewModel.InvokeItemCommand.Execute(itemViewModel);
            foreach (var message in messages)
            {
                Assert.AreSame(listViewModel, message.Context?.Page);
            }

            return messages;
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(messages);
            itemViewModel.SafeCleanup();
            listViewModel.SafeCleanup();
            listViewModel.Dispose();
        }
    }

    private static async Task<ItemsUpdatedEventArgs> ObserveNextItemsUpdateAsync(ListViewModel viewModel, Action action)
    {
        var updateObserved = new TaskCompletionSource<ItemsUpdatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnItemsUpdated(ListViewModel sender, ItemsUpdatedEventArgs args) => updateObserved.TrySetResult(args);

        viewModel.ItemsUpdated += OnItemsUpdated;
        try
        {
            action();

            var completed = await Task.WhenAny(updateObserved.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(updateObserved.Task, completed);
            return await updateObserved.Task;
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
        }
    }
}
