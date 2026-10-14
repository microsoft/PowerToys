// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ListViewModelTests
{
    private sealed partial class DetailsTestPage(params IListItem[] items) : ListPage
    {
        public override IListItem[] GetItems() => items;
    }

    private sealed partial class DetailsDynamicTestPage(IListItem item) : DynamicListPage
    {
        public override IListItem[] GetItems() => [item];

        public override void UpdateSearchText(string oldSearch, string newSearch) => RaiseItemsChanged(1);
    }

    private sealed partial class ThrowingDetails : Details
    {
        public override IDetailsElement[] Metadata
        {
            get => throw new InvalidOperationException("Constructed details metadata failure.");
            set => base.Metadata = value;
        }
    }

    private sealed partial class TransientDetailsReadItem : ListItem
    {
        public bool FailNextDetailsRead { get; set; }

        public override IDetails? Details
        {
            get
            {
                if (FailNextDetailsRead)
                {
                    FailNextDetailsRead = false;
                    throw new InvalidOperationException("Constructed transient details read failure.");
                }

                return base.Details;
            }

            set => base.Details = value;
        }

        public TransientDetailsReadItem()
            : base(new NoOpCommand { Name = "Item" })
        {
        }
    }

    private sealed partial class ThrowingDetailsListItem : ListItem
    {
        public int DetailsReadCount { get; private set; }

        public override IDetails? Details
        {
            get
            {
                DetailsReadCount++;
                throw new InvalidOperationException("Details are unavailable.");
            }

            set => base.Details = value;
        }

        public ThrowingDetailsListItem()
            : base(new NoOpCommand { Name = "Item" })
        {
        }

        public void NotifyDetailsChanged() => OnPropertyChanged(nameof(Details));
    }

    private sealed class InspectableDetailsListItemViewModel : ListItemViewModel
    {
        public CommandContextItemViewModel? RetainedShowDetailsCommand => UnsafeShowDetailsCommand;

        public void NotifyDetailsChanged() => FetchProperty(nameof(Details));

        public bool RefreshMenuSnapshot()
        {
            lock (ContextItemsLock)
            {
                return RefreshContextMenuSnapshotUnsafe();
            }
        }

        public InspectableDetailsListItemViewModel(IListItem model, ListViewModel page)
            : base(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette)
        {
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_ShelfMenuDoesNotReadUnavailableDetails()
    {
        var page = CreateViewModel(new ListPage());
        var model = new ThrowingDetailsListItem
        {
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "action", Name = "Extension action" })],
        };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.QuickAccessShelf);
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            Assert.IsTrue(item.SafeSlowInit());
            var commands = item.AllCommands;
            Assert.AreEqual(2, commands.Count);
            Assert.AreSame(item.PrimaryMenuItem, commands[0]);
            Assert.AreEqual("action", ((CommandContextItemViewModel)commands[1]).Command.Id);

            model.NotifyDetailsChanged();
            Assert.IsTrue(item.SafeSlowInit());

            Assert.AreEqual(0, model.DetailsReadCount);
            Assert.IsNull(item.Details);
            Assert.IsFalse(item.HasDetails);
            Assert.IsNull(GetShowDetailsCommand(item));
            Assert.AreSame(commands, item.AllCommands);
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task ShowDetailsCommand_DetailsFailurePreservesItemAcrossFiltering(bool dynamicPage, bool failDetailsGetter)
    {
        var model = new TransientDetailsReadItem
        {
            Details = failDetailsGetter ? new Details() : new ThrowingDetails(),
            FailNextDetailsRead = failDetailsGetter,
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "action", Name = "Extension action" })],
            TextToSuggest = "Item suggestion",
        };
        var page = CreateViewModel(dynamicPage ? new DetailsDynamicTestPage(model) : new DetailsTestPage(model));
        try
        {
            await ObserveNextItemsUpdateAsync(page, page.InitializeProperties);
            var item = page.FilteredItems.Single();
            var initialized = item.SafeSlowInit();
            await ObserveNextItemsUpdateAsync(page, () => page.SearchTextBox = "Item");

            Assert.AreSame(item, page.FilteredItems.Single());
            Assert.IsTrue(initialized);
            Assert.IsFalse(item.IsInErrorState);
            Assert.IsNull(item.Details);
            Assert.IsNull(GetShowDetailsCommand(item));
            Assert.AreEqual("Item suggestion", item.TextToSuggest);
            Assert.AreSame(model.Command, item.Command.Model.Unsafe);
            Assert.IsTrue(item.CanOpenContextMenu);
            var action = item.AllCommands.OfType<CommandContextItemViewModel>().Single(command => command.Command.Id == "action");

            model.Details = new Details { Title = "Recovered details" };
            Assert.IsTrue(item.SafeSlowInit());
            Assert.AreEqual("Recovered details", item.Details?.Title);
            Assert.IsNotNull(GetShowDetailsCommand(item));
            Assert.IsTrue(item.AllCommands.Contains(action));

            await ObserveNextItemsUpdateAsync(page, () => page.SearchTextBox = "Ite");
            Assert.AreSame(item, page.FilteredItems.Single());
            Assert.IsFalse(item.IsInErrorState);
        }
        finally
        {
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_PrimaryReplacesHiddenDetailsInSameSizeSnapshot()
    {
        var pageModel = new ListPage();
        var page = CreateViewModel(pageModel);
        var command = new NoOpCommand { Id = "primary" };
        var model = new ListItem(command) { Details = new Details() };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        try
        {
            page.InitializeProperties();
            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsNull(item.PrimaryMenuItem);
            Assert.IsNotNull(GetShowDetailsCommand(item));
            Assert.AreEqual(1, item.AllCommands.Count);

            // The next snapshot refresh must observe both slot changes together.
            pageModel.ShowDetails = true;
            var menuChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            item.PropertyChangedBackground += (_, args) =>
            {
                if (args.PropertyName == nameof(item.AllCommands) && item.PrimaryMenuItem is not null)
                {
                    menuChanged.TrySetResult();
                }
            };
            command.Name = "Run";
            item.Command.ApplyPendingUpdates();
            item.ApplyPendingUpdates();
            await menuChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(1, item.AllCommands.Count);
            Assert.AreSame(item.PrimaryMenuItem, item.AllCommands[0]);
            Assert.IsNull(GetShowDetailsCommand(item));
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ShowDetailsCommand_PageDetailsModeRefreshesCachedMenus(bool initialMode)
    {
        var models = new[]
        {
            new CountingListItem(new NoOpCommand { Name = "First" }) { Details = new Details() },
            new CountingListItem(new NoOpCommand { Name = "Second" }) { Details = new Details() },
        };
        var pageModel = new DetailsTestPage(models) { ShowDetails = initialMode };
        var page = CreateViewModel(pageModel);
        try
        {
            await ObserveNextItemsUpdateAsync(page, page.InitializeProperties);
            var items = page.FilteredItems.ToArray();
            foreach (var item in items)
            {
                Assert.IsTrue(item.SafeSlowInit());
                Assert.AreEqual(!initialMode, GetShowDetailsCommand(item) is not null);
            }

            var details = items.Select(item => item.Details).ToArray();
            var commands = items.Select(GetShowDetailsCommand).ToArray();
            var versions = items.Select(item => item.ContextItemsVersion).ToArray();
            var reads = models.Select(model => model.MoreCommandsReadCount).ToArray();
            await ObserveNextItemsUpdateAsync(page, () => page.SearchTextBox = "First");
            Assert.AreEqual(1, page.FilteredItems.Count);

            pageModel.ShowDetails = !initialMode;
            Assert.AreEqual(!initialMode, page.ShowDetails);
            for (var i = 0; i < items.Length; i++)
            {
                Assert.AreEqual(initialMode, GetShowDetailsCommand(items[i]) is not null);
                Assert.AreSame(details[i], items[i].Details);
                Assert.AreEqual(versions[i], items[i].ContextItemsVersion);
                Assert.AreEqual(reads[i], models[i].MoreCommandsReadCount);
                if (commands[i] is { } previous)
                {
                    Assert.IsTrue(previous.Initialized.HasFlag(InitializedState.CleanedUp));
                }
            }
        }
        finally
        {
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_FailedReselectionDoesNotDiscardSuccessfulUpdate()
    {
        using var updatedDetails = new BlockingDetails();
        var page = CreateViewModel(new ListPage());
        var model = new TransientDetailsReadItem { Details = new Details { Title = "Old details" } };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        Task? update = null;
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var originalDetails = item.Details;
            var originalCommand = GetShowDetailsCommand(item);
            update = Task.Run(() => model.Details = updatedDetails);
            Assert.IsTrue(updatedDetails.DetailsReadEntered.Wait(TimeSpan.FromSeconds(5)));

            model.FailNextDetailsRead = true;
            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsFalse(item.IsInErrorState);
            Assert.AreSame(originalDetails, item.Details);
            Assert.AreSame(originalCommand, GetShowDetailsCommand(item));
            updatedDetails.ContinueDetailsRead.Set();
            await update.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual("Updated details", item.Details?.Title, "A failed newer read prevented a completed valid update from being published.");
        }
        finally
        {
            updatedDetails.ContinueDetailsRead.Set();
            if (update is not null)
            {
                await update.WaitAsync(TimeSpan.FromSeconds(5));
            }

            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_AutomaticDetailsDoesNotAllocateHiddenAction()
    {
        var pageModel = new ListPage { ShowDetails = true };
        var page = CreateViewModel(pageModel);
        var item = new InspectableDetailsListItemViewModel(new ListItem(new NoOpCommand { Name = "Item" }) { Details = new Details() }, page);
        try
        {
            page.InitializeProperties();
            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsTrue(item.HasDetails);
            Assert.IsNull(GetShowDetailsCommand(item));
            Assert.IsNull(item.RetainedShowDetailsCommand, "An initialized Show Details action is retained despite being hidden by the page's details mode.");
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_NullDetailsDoesNotNotifyOnReselection()
    {
        var page = CreateViewModel(new ListPage());
        var item = new InspectableDetailsListItemViewModel(new ListItem(new NoOpCommand { Name = "Item" }), page);
        var detailsChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        item.PropertyChangedBackground += (_, args) =>
        {
            if (args.PropertyName is nameof(item.Details) or nameof(item.HasDetails))
            {
                detailsChanged.TrySetResult();
            }
        };
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsTrue(item.SafeSlowInit());
            item.NotifyDetailsChanged();
            item.ApplyPendingUpdates();
            var completed = await Task.WhenAny(detailsChanged.Task, Task.Delay(200));
            Assert.AreNotSame(detailsChanged.Task, completed);
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ShowDetailsCommand_PendingDetailsUsesLatestPageMode(bool initialMode)
    {
        using var updatedDetails = new BlockingDetails();
        var model = new ListItem(new NoOpCommand { Name = "Item" }) { Details = new Details() };
        var pageModel = new DetailsTestPage(model) { ShowDetails = initialMode };
        var page = CreateViewModel(pageModel);
        Task? update = null;
        try
        {
            await ObserveNextItemsUpdateAsync(page, page.InitializeProperties);
            var item = page.FilteredItems.Single();
            Assert.IsTrue(item.SafeSlowInit());
            update = Task.Run(() => model.Details = updatedDetails);
            Assert.IsTrue(updatedDetails.DetailsReadEntered.Wait(TimeSpan.FromSeconds(5)));

            pageModel.ShowDetails = !initialMode;
            updatedDetails.ContinueDetailsRead.Set();
            await update.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual("Updated details", item.Details?.Title);
            Assert.AreEqual(initialMode, GetShowDetailsCommand(item) is not null);
            Assert.AreEqual(1, updatedDetails.SubscriptionCount);
        }
        finally
        {
            updatedDetails.ContinueDetailsRead.Set();
            if (update is not null)
            {
                await update.WaitAsync(TimeSpan.FromSeconds(5));
            }

            page.SafeCleanup();
            page.Dispose();
        }
    }
}
