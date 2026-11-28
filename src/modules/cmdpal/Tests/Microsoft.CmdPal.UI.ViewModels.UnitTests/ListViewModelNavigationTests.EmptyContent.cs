// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// The command bar of a page that lists nothing: its empty content's command, such as an extension's Open settings, stays
/// there however the empty list's selection and the empty content arrive.
/// </summary>
public sealed partial class ListViewModelNavigationTests
{
    [TestMethod]
    [Timeout(15000)]
    public async Task AnEmptyListKeepsItsEmptyContentsCommandWhenTheSelectionClears()
    {
        // The view clears the selection once an empty list comes in, which can be after the empty content's command went
        // into the command bar: it was gone then.
        var scheduler = new QueuedTaskScheduler();
        var page = new StaticPage([]) { EmptyContent = new CommandItem(new NoOpCommand { Name = "Open settings" }) { Title = "It's off" } };
        var viewModel = CreateViewModel(page, scheduler);
        var contexts = new List<ICommandBarContext?>();

        try
        {
            Assert.IsTrue(await viewModel.InitializeAsync());
            scheduler.Drain();
            Assert.IsTrue(viewModel.ShowEmptyContent);
            viewModel.CommandBarContextChanged += OnCommandBarContextChanged;

            viewModel.UpdateSelectedItemCommand.Execute(null);
            scheduler.Drain();
            Assert.IsTrue(contexts.Count > 0);
            Assert.AreSame(viewModel.EmptyContent, contexts[^1]);
        }
        finally
        {
            viewModel.CommandBarContextChanged -= OnCommandBarContextChanged;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }

        void OnCommandBarContextChanged(object? sender, PageCommandBarContextChangedEventArgs e) => contexts.Add(e.Context);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task AListWithItemsStillClearsTheCommandBarWhenTheSelectionClears()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new StaticPage([CreateItem("Alpha")]) { EmptyContent = new CommandItem(new NoOpCommand { Name = "Open settings" }) };
        var viewModel = CreateViewModel(page, scheduler);
        var contexts = new List<ICommandBarContext?>();

        try
        {
            Assert.IsTrue(await viewModel.InitializeAsync());
            scheduler.Drain();
            Assert.AreEqual(1, viewModel.FilteredItems.Count);
            viewModel.UpdateSelectedItemCommand.Execute(viewModel.FilteredItems[0]);
            scheduler.Drain();
            viewModel.CommandBarContextChanged += OnCommandBarContextChanged;

            viewModel.UpdateSelectedItemCommand.Execute(null);
            scheduler.Drain();
            Assert.IsTrue(contexts.Count > 0);
            Assert.IsNull(contexts[^1]);
        }
        finally
        {
            viewModel.CommandBarContextChanged -= OnCommandBarContextChanged;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }

        void OnCommandBarContextChanged(object? sender, PageCommandBarContextChangedEventArgs e) => contexts.Add(e.Context);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task EmptyContentChangedWhileTheListIsEmptyPutsItsCommandInTheCommandBar()
    {
        // The new empty content was taken without the command bar, which kept the old one's command, or none.
        var scheduler = new QueuedTaskScheduler();
        var page = new StaticPage([]) { EmptyContent = new CommandItem(new NoOpCommand { Name = "Nothing to do" }) { Title = "Before" } };
        var viewModel = CreateViewModel(page, scheduler);
        var contexts = new List<ICommandBarContext?>();

        try
        {
            Assert.IsTrue(await viewModel.InitializeAsync());
            scheduler.Drain();
            Assert.IsTrue(viewModel.ShowEmptyContent);
            viewModel.CommandBarContextChanged += OnCommandBarContextChanged;

            var after = new CommandItem(new NoOpCommand { Name = "Open settings" }) { Title = "After" };
            page.EmptyContent = after;
            scheduler.DrainUntil(() => contexts.Count > 0);
            scheduler.Drain();
            Assert.AreSame(after, viewModel.EmptyContent.Model.Unsafe);
            Assert.AreSame(viewModel.EmptyContent, contexts[^1]);

            page.EmptyContent = null;
            scheduler.Drain();
            Assert.IsNull(contexts[^1], "Removing the empty content clears its command from the bar.");
        }
        finally
        {
            viewModel.CommandBarContextChanged -= OnCommandBarContextChanged;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }

        void OnCommandBarContextChanged(object? sender, PageCommandBarContextChangedEventArgs e) => contexts.Add(e.Context);
    }

    [TestMethod]
    [Timeout(15000)]
    public async Task EmptyContentChangeKeepsThePublishedRowsCommandBarContext()
    {
        var scheduler = new QueuedTaskScheduler();
        var page = new SearchPage { EmptyContent = new CommandItem(new NoOpCommand { Name = "Before search" }) };
        page.ReplaceItems([], notify: false);
        var viewModel = CreateViewModel(page, scheduler);
        var selectedContextSeen = false;
        var emptyContextAfterSelection = false;

        try
        {
            Assert.IsTrue(await viewModel.InitializeAsync());
            scheduler.Drain();
            Assert.IsTrue(viewModel.ShowEmptyContent);
            viewModel.ItemsUpdated += SelectPublishedItem;
            viewModel.CommandBarContextChanged += ObserveCommandBarContext;

            await Task.Run(() =>
            {
                // The Indexer raises ItemsChanged and then EmptyContent on the same worker.
                page.ReplaceItems([CreateItem("First result")]);
                Assert.IsTrue(viewModel.ShowEmptyContent, "The UI has not published the fetched row yet.");
                page.EmptyContent = new CommandItem(new AnonymousCommand(null) { Name = "Search all files" });
            });
            scheduler.Drain();

            Assert.AreEqual(1, viewModel.FilteredItems.Count);
            Assert.IsTrue(selectedContextSeen, "The published row must set the command-bar context.");
            Assert.IsFalse(emptyContextAfterSelection, "The queued empty-content update must leave the published row's context active.");
        }
        finally
        {
            viewModel.ItemsUpdated -= SelectPublishedItem;
            viewModel.CommandBarContextChanged -= ObserveCommandBarContext;
            viewModel.Dispose();
            scheduler.Drain();
            viewModel.SafeCleanup();
        }

        void SelectPublishedItem(ListViewModel sender, ItemsUpdatedEventArgs args) =>
            sender.UpdateSelectedItemCommand.Execute(sender.FilteredItems[0]);

        void ObserveCommandBarContext(object? sender, PageCommandBarContextChangedEventArgs args)
        {
            if (viewModel.FilteredItems.Count == 0)
            {
                return;
            }

            selectedContextSeen |= ReferenceEquals(args.Context, viewModel.FilteredItems[0]);
            emptyContextAfterSelection |= selectedContextSeen && ReferenceEquals(args.Context, viewModel.EmptyContent);
        }
    }
}
