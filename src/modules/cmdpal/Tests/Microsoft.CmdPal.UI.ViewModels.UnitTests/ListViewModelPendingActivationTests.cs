// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
public sealed partial class ListViewModelPendingActivationTests
{
    private sealed partial class TestHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Pending activation test host";
    }

    private sealed partial class DelayedSearchPage : DynamicListPage
    {
        private IListItem[] _items;
        private int _getItemsCount;

        internal ManualResetEventSlim GetItemsStarted { get; } = new(false);

        internal ManualResetEventSlim GetItemsGate { get; } = new(true);

        internal DelayedSearchPage(params IListItem[] items) => _items = items;

        public override IListItem[] GetItems()
        {
            var count = Interlocked.Increment(ref _getItemsCount);
            if (count > 1)
            {
                GetItemsStarted.Set();
                if (!GetItemsGate.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("The blocked GetItems call was not released.");
                }
            }

            return Volatile.Read(ref _items);
        }

        public override void UpdateSearchText(string oldSearch, string newSearch) =>
            ReplaceItems([CreateItem(newSearch)]);

        internal void ReplaceItems(IListItem[] items)
        {
            Volatile.Write(ref _items, items);
            RaiseItemsChanged(items.Length);
        }
    }

    private sealed partial class StaticSearchPage(IListItem[] items) : ListPage
    {
        public override IListItem[] GetItems() => items;
    }

    private sealed class InvokeListener : IDisposable
    {
        private readonly TaskCompletionSource<string> _invoked = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Task<string> Invoked => _invoked.Task;

        internal InvokeListener()
        {
            WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this, (_, message) =>
                _invoked.TrySetResult(message.Command.Unsafe?.Name ?? string.Empty));
        }

        public void Dispose() => WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task EnterDuringInFlightSearch_InvokesFirstResultWhenPublished()
    {
        var page = new DelayedSearchPage(CreateItem("Initial"));
        var viewModel = CreateViewModel(page);
        using var listener = new InvokeListener();

        try
        {
            await ObserveItemsAsync(viewModel, "Initial", viewModel.InitializeProperties);

            page.GetItemsGate.Reset();
            page.GetItemsStarted.Reset();
            viewModel.SearchTextBox = "Notepad";

            Assert.IsTrue(page.GetItemsStarted.Wait(TimeSpan.FromSeconds(3)), "The search fetch did not start.");
            viewModel.InvokeSelectedItemOrQueue(viewModel.FilteredItems[0]);
            Assert.IsFalse(listener.Invoked.IsCompleted, "Enter should not run a stale result while the query is in flight.");

            page.GetItemsGate.Set();

            var invoked = await listener.Invoked.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreEqual("Notepad", invoked);
        }
        finally
        {
            page.GetItemsGate.Set();
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task EnterWhenResultsAreReady_InvokesSelectedItemImmediately()
    {
        var page = new DelayedSearchPage(CreateItem("Calculator"));
        var viewModel = CreateViewModel(page);
        using var listener = new InvokeListener();

        try
        {
            await ObserveItemsAsync(viewModel, "Calculator", viewModel.InitializeProperties);

            viewModel.InvokeSelectedItemOrQueue(viewModel.FilteredItems[0]);

            var invoked = await listener.Invoked.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreEqual("Calculator", invoked);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task ClearingSearch_CancelsQueuedEnter()
    {
        var page = new DelayedSearchPage(CreateItem("Initial"));
        var viewModel = CreateViewModel(page);
        using var listener = new InvokeListener();

        try
        {
            await ObserveItemsAsync(viewModel, "Initial", viewModel.InitializeProperties);

            page.GetItemsGate.Reset();
            page.GetItemsStarted.Reset();
            viewModel.SearchTextBox = "Chrome";
            Assert.IsTrue(page.GetItemsStarted.Wait(TimeSpan.FromSeconds(3)), "The search fetch did not start.");
            viewModel.InvokeSelectedItemOrQueue(null);

            viewModel.SearchTextBox = string.Empty;
            page.GetItemsGate.Set();

            await Task.Delay(200);
            Assert.IsFalse(listener.Invoked.IsCompleted, "Clearing the query should drop the queued Enter.");
        }
        finally
        {
            page.GetItemsGate.Set();
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task StaticPageFilter_InvokesFirstMatchWithoutWaiting()
    {
        var page = new StaticSearchPage([CreateItem("Alpha"), CreateItem("Beta")]);
        var viewModel = CreateViewModel(page);
        using var listener = new InvokeListener();

        try
        {
            await ObserveItemsAsync(viewModel, vm => vm.FilteredItems.Count == 2, viewModel.InitializeProperties);

            viewModel.SearchTextBox = "Beta";
            viewModel.InvokeSelectedItemOrQueue(null);

            var invoked = await listener.Invoked.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.AreEqual("Beta", invoked);
        }
        finally
        {
            viewModel.SafeCleanup();
            viewModel.Dispose();
        }
    }

    private static ListItem CreateItem(string title) =>
        new(new NoOpCommand { Name = title }) { Title = title };

    private static ListViewModel CreateViewModel(IListPage page) =>
        new(page, TaskScheduler.Default, new TestHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);

    private static async Task ObserveItemsAsync(ListViewModel viewModel, string expectedTitle, Action action) =>
        await ObserveItemsAsync(viewModel, vm => vm.FilteredItems.Count >= 1 && vm.FilteredItems[0].Title == expectedTitle, action);

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
            if (predicate(viewModel))
            {
                published.TrySetResult();
            }

            await published.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally
        {
            viewModel.ItemsUpdated -= OnItemsUpdated;
        }
    }
}
