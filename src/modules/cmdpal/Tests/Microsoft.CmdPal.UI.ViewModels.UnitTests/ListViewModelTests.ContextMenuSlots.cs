// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ListViewModelTests
{
    private sealed class SlotContextMenuFactory : IContextMenuFactory
    {
        private int _buildCount;

        public int? SlotIndex { get; set; }

        public bool RepeatSlot { get; init; }

        public int BuildCount => Volatile.Read(ref _buildCount);

        public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
            IContextItem[] items,
            CommandItemViewModel commandItem,
            ItemSurface? surface)
        {
            Interlocked.Increment(ref _buildCount);
            var results = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(items, commandItem, surface);
            results.Remove(ContextMenuSlot.ShowDetails);
            if (surface?.SupportsDetailsPane == true && SlotIndex is { } index)
            {
                results.Insert(index, ContextMenuSlot.ShowDetails);
                if (RepeatSlot)
                {
                    results.Add(ContextMenuSlot.ShowDetails);
                }
            }

            return results;
        }

        public void AddMoreCommandsToTopLevel(TopLevelViewModel topLevelItem, ICommandProviderContext providerContext, List<IContextItem?> contextItems)
        {
        }
    }

    [TestMethod]
    public void ItemSurface_BelongsToEachDisplayedInstance()
    {
        var model = new ListItem(new NoOpCommand { Name = "Item" }) { Details = new Details() };
        var page = CreateViewModel(new ListPage());
        var palette = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        var shelf = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.QuickAccessShelf);
        var command = new CommandItemViewModel(new(model), new(page), DefaultContextMenuFactory.Instance);
        try
        {
            Assert.AreSame(ItemSurface.CommandPalette, ((CommandItemViewModel)palette).Surface);
            Assert.AreSame(ItemSurface.QuickAccessShelf, ((CommandItemViewModel)shelf).Surface);
            Assert.IsNull(command.Surface);
            Assert.IsTrue(palette.SafeSlowInit());
            Assert.IsTrue(shelf.SafeSlowInit());
            Assert.IsNotNull(GetShowDetailsCommand(palette));
            Assert.IsNull(GetShowDetailsCommand(shelf));
            Assert.IsTrue(palette.HasDetails);
            Assert.IsFalse(shelf.HasDetails);
        }
        finally
        {
            command.SafeCleanup();
            palette.SafeCleanup();
            shelf.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShowDetailsCommand_UnchangedSnapshotRefreshReusesEntries(bool hasDetails)
    {
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = hasDetails ? new Details() : null,
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Action" })],
        };
        var page = CreateViewModel(new ListPage());
        var item = new InspectableDetailsListItemViewModel(model, page);
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var entries = item.AllCommands;
            for (var index = 0; index < 10; index++)
            {
                Assert.IsFalse(item.RefreshMenuSnapshot());
                Assert.AreSame(entries, item.AllCommands);
            }
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
    public void ShowDetailsCommand_DuplicateSlotsRespectAnExtensionOverrideAfterTheSlot(bool extensionProvidesShowDetails)
    {
        var rawCommand = new NoOpCommand
        {
            Id = extensionProvidesShowDetails ? ShowDetailsCommand.ShowDetailsCommandId : "extension",
            Name = "Extension action",
        };
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = new Details(),
            MoreCommands = [new CommandContextItem(rawCommand)],
        };
        var factory = new SlotContextMenuFactory { SlotIndex = 0, RepeatSlot = true };
        var page = CreateViewModel(new ListPage());
        var item = new ListItemViewModel(model, new(page), factory, ItemSurface.CommandPalette);
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var showDetails = item.AllCommands.OfType<CommandContextItemViewModel>()
                .Single(entry => entry.Command.Id == ShowDetailsCommand.ShowDetailsCommandId);
            Assert.AreEqual(extensionProvidesShowDetails, ReferenceEquals(rawCommand, showDetails.Command.Model.Unsafe));
            Assert.IsFalse(item.AllCommands.Any(entry => entry is ContextMenuSlot));
            List<IContextItem?> sdkItems = [];
            item.CopySdkContextItemsTo(sdkItems);
            Assert.AreEqual(1, sdkItems.Count);
            Assert.AreSame(rawCommand, ((ICommandContextItem)sdkItems[0]!).Command);
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_FactoryDeclaresSlotWithoutReadingDetails()
    {
        var model = new ThrowingDetailsListItem();
        var page = CreateViewModel(new ListPage());
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        try
        {
            var factory = DefaultContextMenuFactory.Instance;
            CollectionAssert.AreEqual(
                new IContextItemViewModel[] { ContextMenuSlot.ShowDetails },
                factory.UnsafeBuildAndInitMoreCommands([], item, ItemSurface.CommandPalette));
            Assert.AreEqual(0, factory.UnsafeBuildAndInitMoreCommands([], item, ItemSurface.QuickAccessShelf).Count);
            Assert.AreEqual(0, factory.UnsafeBuildAndInitMoreCommands([], item, surface: null).Count);
            Assert.AreEqual(0, model.DetailsReadCount);
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public async Task ShowDetailsCommand_PreservesFactorySlotPositionAcrossDetailsAndModeChanges(int slotIndex)
    {
        var factory = new SlotContextMenuFactory { SlotIndex = slotIndex };
        var model = new CountingListItem(new NoOpCommand { Id = "primary", Name = "Item" })
        {
            Details = new Details { Title = "Initial details" },
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Id = "first", Name = "First action" }),
                new CommandContextItem(new NoOpCommand { Id = "second", Name = "Second action" }),
            ],
        };
        var pageModel = new DetailsTestPage(model);
        var page = new ListViewModel(pageModel, TaskScheduler.Default, new TestAppExtensionHost(), CommandProviderContext.Empty, factory);
        var recipient = new object();
        try
        {
            await ObserveNextItemsUpdateAsync(page, page.InitializeProperties);
            var item = page.FilteredItems.Single();
            Assert.IsTrue(item.SafeSlowInit());
            var expectedIds = new List<string> { "primary", "first", "second" };
            expectedIds.Insert(slotIndex + 1, ShowDetailsCommand.ShowDetailsCommandId);
            CollectionAssert.AreEqual(expectedIds, item.AllCommands.OfType<CommandContextItemViewModel>().Select(command => command.Command.Id).ToArray());
            Assert.IsFalse(item.AllCommands.Any(entry => entry is ContextMenuSlot));
            Assert.AreEqual(expectedIds[1], item.SecondaryCommand?.Command.Id);

            var previousEntries = item.AllCommands;
            var previousCommand = GetShowDetailsCommand(item);
            Assert.IsNotNull(previousCommand);
            var version = item.ContextItemsVersion;
            var readCount = model.MoreCommandsReadCount;
            var buildCount = factory.BuildCount;
            List<IContextItem?> sdkItems = [];
            item.CopySdkContextItemsTo(sdkItems);
            string[] expectedSdkIds = ["first", "second"];
            CollectionAssert.AreEqual(expectedSdkIds, sdkItems.OfType<ICommandContextItem>().Select(command => command.Command.Id).ToArray());
            Assert.AreEqual(2, sdkItems.Count);

            model.Details = new Details { Title = "Updated details" };

            Assert.AreNotSame(previousEntries, item.AllCommands);
            Assert.AreEqual(previousEntries.Count, item.AllCommands.Count);
            Assert.IsTrue(previousCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            foreach (var entry in previousEntries.Where(entry => !ReferenceEquals(entry, previousCommand)))
            {
                Assert.IsTrue(item.AllCommands.Any(current => ReferenceEquals(current, entry)));
            }

            var currentCommand = GetShowDetailsCommand(item);
            Assert.IsNotNull(currentCommand);
            Assert.AreSame(currentCommand, item.AllCommands[slotIndex + 1]);
            DetailsViewModel? displayed = null;
            WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) => displayed = message.Details);
            ((ShowDetailsCommand)currentCommand.Command.Model.Unsafe!).Invoke();
            Assert.AreSame(item.Details, displayed);

            var details = item.Details;
            pageModel.ShowDetails = true;
            Assert.IsNull(GetShowDetailsCommand(item));
            Assert.IsTrue(currentCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual("first", item.SecondaryCommand?.Command.Id);
            pageModel.ShowDetails = false;
            CollectionAssert.AreEqual(expectedIds, item.AllCommands.OfType<CommandContextItemViewModel>().Select(command => command.Command.Id).ToArray());
            Assert.AreEqual(expectedIds[1], item.SecondaryCommand?.Command.Id);
            Assert.AreSame(details, item.Details);
            Assert.AreEqual(version, item.ContextItemsVersion);
            Assert.AreEqual(readCount, model.MoreCommandsReadCount);
            Assert.AreEqual(buildCount, factory.BuildCount);
            List<IContextItem?> updatedSdkItems = [];
            item.CopySdkContextItemsTo(updatedSdkItems);
            CollectionAssert.AreEqual(sdkItems, updatedSdkItems);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_FactoryCanOmitRemoveAndRestoreSlot()
    {
        using var details = new BlockingDetails { BlockRead = false };
        var factory = new SlotContextMenuFactory();
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" }) { Details = details };
        var item = new ListItemViewModel(model, new(page), factory, ItemSurface.CommandPalette);
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var currentDetails = item.Details;
            Assert.IsNotNull(currentDetails);
            Assert.IsNull(GetShowDetailsCommand(item));

            factory.SlotIndex = 0;
            model.MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "First action" })];
            var showDetails = GetShowDetailsCommand(item);
            Assert.IsNotNull(showDetails);
            Assert.AreSame(showDetails, item.SecondaryCommand);

            factory.SlotIndex = null;
            model.MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Second action" })];
            Assert.IsNull(GetShowDetailsCommand(item));
            Assert.IsTrue(showDetails.Initialized.HasFlag(InitializedState.CleanedUp));

            factory.SlotIndex = 0;
            model.MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Third action" })];
            var restoredCommand = GetShowDetailsCommand(item);
            Assert.IsNotNull(restoredCommand);
            Assert.AreNotSame(showDetails, restoredCommand);
            Assert.AreSame(restoredCommand, item.SecondaryCommand);
            Assert.AreSame(currentDetails, item.Details);
            Assert.AreEqual(1, details.SubscriptionCount);
            Assert.AreEqual(4, factory.BuildCount);

            item.SafeCleanup();
            Assert.IsTrue(restoredCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual(0, details.SubscriptionCount);
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
    public async Task ShowDetailsCommand_UsesCurrentFactorySlotWhenPendingDetailsComplete(bool includeSlot)
    {
        using var details = new BlockingDetails();
        var factory = new SlotContextMenuFactory { SlotIndex = includeSlot ? null : 0 };
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" }) { Details = new Details() };
        var item = new ListItemViewModel(model, new(page), factory, ItemSurface.CommandPalette);
        Task? detailsChange = null;
        var recipient = new object();
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            detailsChange = Task.Run(() => model.Details = details);
            Assert.IsTrue(details.DetailsReadEntered.Wait(TimeSpan.FromSeconds(5)));

            factory.SlotIndex = includeSlot ? 0 : null;
            model.MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "new", Name = "New action" })];
            var rawCommand = item.AllCommands.OfType<CommandContextItemViewModel>().Single(command => command.Command.Id == "new");

            details.ContinueDetailsRead.Set();
            await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(includeSlot, GetShowDetailsCommand(item) is not null);
            Assert.IsTrue(item.AllCommands.Contains(rawCommand));
            Assert.IsFalse(rawCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual(1, details.SubscriptionCount);
            Assert.AreEqual(2, factory.BuildCount);
            if (GetShowDetailsCommand(item) is { } command)
            {
                DetailsViewModel? displayed = null;
                WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) => displayed = message.Details);
                ((ShowDetailsCommand)command.Command.Model.Unsafe!).Invoke();
                Assert.AreSame(item.Details, displayed);
            }
        }
        finally
        {
            details.ContinueDetailsRead.Set();
            if (detailsChange is not null)
            {
                await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));
            }

            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }
}
