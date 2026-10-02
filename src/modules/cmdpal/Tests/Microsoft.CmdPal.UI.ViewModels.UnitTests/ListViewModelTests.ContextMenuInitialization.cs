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
    private sealed class BlockingContextMenuBuildFactory : IContextMenuFactory, IDisposable
    {
        private int _buildCount;

        internal int BlockBuildNumber { get; init; }

        internal int BuildCount => Volatile.Read(ref _buildCount);

        internal ManualResetEventSlim BuildFinished { get; } = new();

        internal ManualResetEventSlim ContinuePublish { get; } = new();

        internal List<IContextItemViewModel> BlockedResults { get; private set; } = [];

        public List<IContextItemViewModel> UnsafeBuildAndInitMoreCommands(
            IContextItem[] items,
            CommandItemViewModel commandItem,
            ItemSurface? surface)
        {
            var results = DefaultContextMenuFactory.Instance.UnsafeBuildAndInitMoreCommands(items, commandItem, surface);
            if (Interlocked.Increment(ref _buildCount) == BlockBuildNumber)
            {
                BlockedResults = results;
                BuildFinished.Set();
                if (!ContinuePublish.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException("The test did not release the menu build.");
                }
            }

            return results;
        }

        public void AddMoreCommandsToTopLevel(
            TopLevelViewModel topLevelItem,
            ICommandProviderContext providerContext,
            List<IContextItem?> contextItems) =>
            DefaultContextMenuFactory.Instance.AddMoreCommandsToTopLevel(topLevelItem, providerContext, contextItems);

        public void Dispose()
        {
            BuildFinished.Dispose();
            ContinuePublish.Dispose();
        }
    }

    private sealed partial class DetailsDuringInitializationListItem : ListItem
    {
        private bool _updateOnTitleRead;

        public override string Title
        {
            get
            {
                if (_updateOnTitleRead)
                {
                    _updateOnTitleRead = false;
                    Details = new Details { Title = "Updated before subscription" };
                }

                return "Item";
            }

            set => base.Title = value;
        }

        public DetailsDuringInitializationListItem()
            : base(new NoOpCommand { Name = "Item" })
        {
            Details = new Details { Title = "Initial details" };
            _updateOnTitleRead = true;
        }
    }

    [TestMethod]
    [DataRow(false, false, false)]
    [DataRow(false, true, false)]
    [DataRow(true, false, false)]
    [DataRow(true, true, false)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public async Task ShowDetailsCommand_BuildPreservesConcurrentDetails(bool moreCommandsChange, bool startsWithDetails, bool clearDetails)
    {
        using var factory = new BlockingContextMenuBuildFactory { BlockBuildNumber = moreCommandsChange ? 2 : 1 };
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = startsWithDetails ? new Details { Title = "Initial details" } : null,
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "old", Name = "Old action" })],
        };
        var item = new ListItemViewModel(model, new(page), factory, ItemSurface.CommandPalette);
        Task? build = null;
        var recipient = new object();
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            if (moreCommandsChange)
            {
                Assert.IsTrue(item.SafeSlowInit());
                build = Task.Run(() => model.MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "new", Name = "New action" })]);
            }
            else
            {
                build = Task.Run(() => Assert.IsTrue(item.SafeSlowInit()));
            }

            Assert.IsTrue(factory.BuildFinished.Wait(TimeSpan.FromSeconds(5)));
            model.Details = clearDetails ? null : new Details { Title = "Updated details" };
            Assert.AreEqual(clearDetails ? null : "Updated details", item.Details?.Title);
            Assert.AreEqual(moreCommandsChange && !clearDetails, GetShowDetailsCommand(item) is not null);

            factory.ContinuePublish.Set();
            await build.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(moreCommandsChange ? 2 : 1, factory.BuildCount);
            Assert.IsTrue(factory.BlockedResults.Where(entry => entry is not ContextMenuSlot).All(item.AllCommands.Contains));
            var actionId = moreCommandsChange ? "new" : "old";
            var action = item.AllCommands.OfType<CommandContextItemViewModel>().Single(command => command.Command.Id == actionId);
            Assert.IsFalse(action.Initialized.HasFlag(InitializedState.CleanedUp));

            var contextCommand = GetShowDetailsCommand(item);
            if (clearDetails)
            {
                Assert.IsNull(contextCommand, "A completed menu build restored Show Details after details were removed.");
                return;
            }

            Assert.IsNotNull(contextCommand, "A completed menu build dropped Show Details after a details update.");
            var showDetails = contextCommand.Command.Model.Unsafe as ShowDetailsCommand;
            Assert.IsNotNull(showDetails);
            DetailsViewModel? displayed = null;
            WeakReferenceMessenger.Default.Register<ShowDetailsMessage>(recipient, (_, message) => displayed = message.Details);
            showDetails.Invoke();
            Assert.AreSame(item.Details, displayed, "The menu build published Show Details for an obsolete details view model.");
        }
        finally
        {
            factory.ContinuePublish.Set();
            if (build is not null)
            {
                await build.WaitAsync(TimeSpan.FromSeconds(5));
            }

            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_BuildDoesNotPublishAfterCleanup()
    {
        using var factory = new BlockingContextMenuBuildFactory { BlockBuildNumber = 1 };
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = new Details { Title = "Initial details" },
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "action", Name = "Action" })],
        };
        var item = new ListItemViewModel(model, new(page), factory, ItemSurface.CommandPalette);
        Task? build = null;
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            build = Task.Run(() => Assert.IsTrue(item.SafeSlowInit()));
            Assert.IsTrue(factory.BuildFinished.Wait(TimeSpan.FromSeconds(5)));

            item.SafeCleanup();
            factory.ContinuePublish.Set();
            await build.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(0, item.AllCommands.Count);
            Assert.IsTrue(factory.BlockedResults.OfType<CommandContextItemViewModel>()
                .All(command => command.Initialized.HasFlag(InitializedState.CleanedUp)));
        }
        finally
        {
            factory.ContinuePublish.Set();
            if (build is not null)
            {
                await build.WaitAsync(TimeSpan.FromSeconds(5));
            }

            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ShowDetailsCommand_SlowInitReadsDetailsAfterSubscription(bool initializeFirst)
    {
        var page = CreateViewModel(new ListPage());
        var model = new DetailsDuringInitializationListItem();
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        try
        {
            if (initializeFirst)
            {
                Assert.IsTrue(item.SafeFastInit());
                Assert.IsTrue(item.SafeInitializeProperties());
            }

            Assert.IsTrue(item.SafeSlowInit());
            Assert.AreEqual("Updated before subscription", item.Details?.Title);
        }
        finally
        {
            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }
}
