// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
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

    private static ListViewModel CreateViewModel(IListPage page) =>
        new(page, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);

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
