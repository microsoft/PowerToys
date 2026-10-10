// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ListViewModelTests
{
    private sealed class AppendingContextMenuFactory : IContextMenuFactory
    {
        public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
            IContextItem[] items,
            CommandItemViewModel commandItem,
            ItemSurface? surface) =>
            DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(
                [.. items, new CommandContextItem(new NoOpCommand { Id = "generated", Name = "Generated action" })],
                commandItem,
                surface);

        public void AddMoreCommandsToTopLevel(TopLevelViewModel topLevelItem, ICommandProviderContext providerContext, List<IContextItem?> contextItems)
        {
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_ComposesFactoryActionsWithoutChangingSdkEntries()
    {
        var page = CreateViewModel(new ListPage());
        var model = new CountingListItem(new NoOpCommand { Id = "primary", Name = "Item" })
        {
            Details = new Details { Title = "Initial details" },
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "extension", Name = "Extension action" })],
        };
        var item = new ListItemViewModel(model, new(page), new AppendingContextMenuFactory(), ItemSurface.CommandPalette);
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            CollectionAssert.AreEqual(
                new[] { "primary", "extension", "generated", ShowDetailsCommand.ShowDetailsCommandId },
                item.AllCommands.OfType<CommandContextItemViewModel>().Select(command => command.Command.Id).ToArray());
            Assert.AreEqual("extension", item.SecondaryCommand?.Command.Id);
            Assert.IsTrue(item.HasOverflowCommands);

            var previousSnapshot = item.AllCommands;
            var previousDetailsCommand = GetShowDetailsCommand(item);
            var version = item.ContextItemsVersion;
            var readCount = model.MoreCommandsReadCount;
            List<IContextItem?> sdkItems = [];
            item.CopySdkContextItemsTo(sdkItems);
            string[] expectedSdkIds = ["extension", "generated"];
            CollectionAssert.AreEqual(expectedSdkIds, sdkItems.OfType<ICommandContextItem>().Select(command => command.Command.Id).ToArray());

            model.Details = new Details { Title = "Updated details" };

            Assert.AreNotSame(previousSnapshot, item.AllCommands);
            Assert.AreEqual(previousSnapshot.Count, item.AllCommands.Count);
            Assert.AreSame(previousSnapshot[1], item.AllCommands[1]);
            Assert.AreSame(previousSnapshot[2], item.AllCommands[2]);
            Assert.AreEqual(version, item.ContextItemsVersion);
            Assert.AreEqual(readCount, model.MoreCommandsReadCount);
            Assert.IsNotNull(previousDetailsCommand);
            Assert.IsTrue(previousDetailsCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            List<IContextItem?> updatedSdkItems = [];
            item.CopySdkContextItemsTo(updatedSdkItems);
            CollectionAssert.AreEqual(sdkItems, updatedSdkItems);
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_ReleasesAndRecreatesHostActionAroundExtensionOverride()
    {
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" }) { Details = new Details() };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var showDetails = GetShowDetailsCommand(item);
            Assert.IsNotNull(showDetails);

            model.MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "action", Name = "Action" })];
            Assert.AreSame(showDetails, GetShowDetailsCommand(item));
            Assert.AreSame(showDetails, item.AllCommands[^1]);

            var foreignCommand = new NoOpCommand { Id = ShowDetailsCommand.ShowDetailsCommandId, Name = "Extension details" };
            model.MoreCommands = [new CommandContextItem(foreignCommand)];
            Assert.AreEqual(1, item.AllCommands.OfType<CommandContextItemViewModel>().Count(command => command.Command.Id == ShowDetailsCommand.ShowDetailsCommandId));
            Assert.AreSame(foreignCommand, GetShowDetailsCommand(item)?.Command.Model.Unsafe);
            Assert.IsFalse(item.AllCommands.Contains(showDetails));
            Assert.IsTrue(showDetails.Initialized.HasFlag(InitializedState.CleanedUp));

            model.MoreCommands = [];
            var restoredCommand = GetShowDetailsCommand(item);
            Assert.IsNotNull(restoredCommand);
            Assert.AreNotSame(showDetails, restoredCommand);
            Assert.AreSame(restoredCommand, item.SecondaryCommand);
            Assert.IsFalse(item.HasOverflowCommands);

            model.MoreCommands = [new CommandContextItem(foreignCommand)];
            Assert.IsTrue(restoredCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            item.SafeCleanup();
            Assert.IsTrue(showDetails.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_ExtensionOverrideAvoidsHostActionOnReselectionAndDetailsUpdates()
    {
        using var initialDetails = new BlockingDetails { BlockRead = false };
        var extensionCommand = new NoOpCommand { Id = ShowDetailsCommand.ShowDetailsCommandId, Name = "Extension details" };
        var model = new CountingListItem(new NoOpCommand { Name = "Item" })
        {
            Details = initialDetails,
            MoreCommands = [new CommandContextItem(extensionCommand)],
        };
        var page = CreateViewModel(new ListPage());
        var item = new InspectableDetailsListItemViewModel(model, page);
        var recipient = new object();
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var extensionEntry = GetShowDetailsCommand(item);
            Assert.IsNotNull(extensionEntry);
            Assert.AreSame(extensionCommand, extensionEntry.Command.Model.Unsafe);
            Assert.IsNull(item.RetainedShowDetailsCommand);
            var version = item.ContextItemsVersion;
            var readCount = model.MoreCommandsReadCount;

            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsTrue(item.HasDetails);
            Assert.IsNull(item.RetainedShowDetailsCommand);
            Assert.AreEqual(1, initialDetails.SubscriptionCount);

            model.Details = new Details { Title = "Latest details" };
            Assert.IsNull(item.RetainedShowDetailsCommand);
            Assert.AreSame(extensionEntry, GetShowDetailsCommand(item));
            Assert.AreEqual(0, initialDetails.SubscriptionCount);
            Assert.AreEqual(version, item.ContextItemsVersion);
            Assert.AreEqual(readCount, model.MoreCommandsReadCount);

            var currentDetails = item.Details;
            model.MoreCommands = [];
            var hostEntry = item.RetainedShowDetailsCommand;
            Assert.IsNotNull(hostEntry);
            Assert.AreSame(hostEntry, GetShowDetailsCommand(item));
            Assert.IsTrue(extensionEntry.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreSame(currentDetails, item.Details);
            DetailsViewModel? displayed = null;
            WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) => displayed = message.Details);
            ((ShowDetailsCommand)hostEntry.Command.Model.Unsafe!).Invoke();
            Assert.AreSame(currentDetails, displayed);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_UpdatesSecondaryNameAndMenuRoles()
    {
        var uiScheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
        var uiTasks = new TaskFactory(uiScheduler);
        var page = new ListViewModel(new ListPage(), uiScheduler, new TestAppExtensionHost(), CommandProviderContext.Empty, DefaultContextMenuFactory.Instance);
        var model = new ListItem(new NoOpCommand()) { Details = new Details() };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        var recipient = new object();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var showDetails = GetShowDetailsCommand(item);
            Assert.IsNotNull(showDetails);
            Assert.AreSame(showDetails, item.SecondaryCommand);
            Assert.IsTrue(item.HasSubmenu);
            Assert.IsTrue(item.CanOpenContextMenu);
            Assert.IsFalse(item.HasOverflowCommands);
            Assert.IsNull(item.PrimaryMenuItem);
            await uiTasks.StartNew(() =>
            {
                menu.PrepareForOpen(item);
                Assert.AreSame(showDetails, menu.SecondaryCommand);
                Assert.AreEqual(CommandContextItemViewModel.SecondaryShortcut, showDetails.DisplayShortcut);
            });

            DetailsViewModel? displayed = null;
            WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) => displayed = message.Details);
            var command = (ShowDetailsCommand)showDetails.Command.Model.Unsafe!;
            var oldName = item.SecondaryCommandName;
            item.ApplyPendingUpdates();
            var nameChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            item.PropertyChangedBackground += (_, args) =>
            {
                if (args.PropertyName == nameof(item.SecondaryCommandName) && item.SecondaryCommandName != oldName)
                {
                    nameChanged.TrySetResult();
                }
            };
            command.Invoke();
            showDetails.Command.ApplyPendingUpdates();
            showDetails.ApplyPendingUpdates();
            item.ApplyPendingUpdates();
            await nameChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(item.Details, displayed);
            Assert.AreNotEqual(oldName, item.SecondaryCommandName);
            Assert.AreEqual(command.Name, item.SecondaryCommandName);

            model.Details = null;
            Assert.IsNull(item.SecondaryCommand);
            Assert.IsFalse(item.HasSubmenu);
            Assert.IsFalse(item.CanOpenContextMenu);
            Assert.IsTrue(showDetails.Initialized.HasFlag(InitializedState.CleanedUp));
        }
        finally
        {
            await uiTasks.StartNew(menu.Close);
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public void ShowDetailsCommand_ReselectionReleasesOldDetailsAndResetsToggle()
    {
        using var details = new BlockingDetails { BlockRead = false };
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" }) { Details = details };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        var recipient = new object();
        try
        {
            Assert.IsTrue(item.SafeSlowInit());
            var showDetails = GetShowDetailsCommand(item);
            Assert.IsNotNull(showDetails);
            ((ShowDetailsCommand)showDetails.Command.Model.Unsafe!).Invoke();

            Assert.IsTrue(item.SafeSlowInit());
            Assert.AreEqual(1, details.SubscriptionCount);
            Assert.IsTrue(showDetails.Initialized.HasFlag(InitializedState.CleanedUp));
            DetailsViewModel? displayed = null;
            WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) => displayed = message.Details);
            var replacement = GetShowDetailsCommand(item);
            Assert.IsNotNull(replacement);
            ((ShowDetailsCommand)replacement.Command.Model.Unsafe!).Invoke();
            Assert.AreSame(item.Details, displayed);

            item.SafeCleanup();
            Assert.AreEqual(0, details.SubscriptionCount);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }
}
