// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class ListViewModelTests
{
    private sealed partial class LateDetailsListItem : ListItem
    {
        private readonly Details _details = new() { Title = "Late details" };
        private int _moreCommandsReadCount;

        public bool ExposeDetails { get; set; }

        public int MoreCommandsReadCount => Volatile.Read(ref _moreCommandsReadCount);

        public override IDetails? Details
        {
            get => ExposeDetails ? _details : null;
            set => base.Details = value;
        }

        public override IContextItem[] MoreCommands
        {
            get
            {
                Interlocked.Increment(ref _moreCommandsReadCount);
                return base.MoreCommands;
            }

            set => base.MoreCommands = value;
        }

        public LateDetailsListItem()
            : base(new NoOpCommand { Name = "Item" })
        {
        }
    }

    private sealed partial class BlockingDetails : IDetails, INotifyPropChanged, IDisposable
    {
        private int _subscriptionCount;

        public bool BlockRead { get; init; } = true;

        public int SubscriptionCount => Volatile.Read(ref _subscriptionCount);

        public ManualResetEventSlim DetailsReadEntered { get; } = new();

        public ManualResetEventSlim ContinueDetailsRead { get; } = new();

        public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
        {
            add => Interlocked.Increment(ref _subscriptionCount);
            remove => Interlocked.Decrement(ref _subscriptionCount);
        }

        public IIconInfo HeroImage => new IconInfo(string.Empty);

        public string Title
        {
            get
            {
                if (BlockRead)
                {
                    DetailsReadEntered.Set();
                    if (!ContinueDetailsRead.Wait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException("The test did not release the details read.");
                    }
                }

                return "Updated details";
            }
        }

        public string Body => string.Empty;

        public IDetailsElement[] Metadata => [];

        public void Dispose()
        {
            DetailsReadEntered.Dispose();
            ContinueDetailsRead.Dispose();
        }
    }

    [TestMethod]
    [DataRow(nameof(ItemSurface.CommandPalette), true)]
    [DataRow(nameof(ItemSurface.QuickAccessShelf), false)]
    public void ShowDetailsCommand_FollowsSurfaceOnReselection(string surfaceName, bool expected)
    {
        var surface = surfaceName switch
        {
            nameof(ItemSurface.CommandPalette) => ItemSurface.CommandPalette,
            nameof(ItemSurface.QuickAccessShelf) => ItemSurface.QuickAccessShelf,
            _ => throw new ArgumentOutOfRangeException(nameof(surfaceName)),
        };
        var page = CreateViewModel(new ListPage());
        var model = new LateDetailsListItem();
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, surface);
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsFalse(item.HasDetails);
            var moreCommandsReadCount = model.MoreCommandsReadCount;

            model.ExposeDetails = true;
            Assert.IsTrue(item.SafeSlowInit());
            Assert.AreEqual(expected, item.HasDetails);
            Assert.AreEqual(expected, GetShowDetailsCommand(item) is not null);
            Assert.AreEqual(moreCommandsReadCount, model.MoreCommandsReadCount);

            Assert.IsTrue(item.SafeSlowInit());
            Assert.AreEqual(
                expected ? 1 : 0,
                item.AllCommands.OfType<CommandContextItemViewModel>().Count(command => command.Command.Id == ShowDetailsCommand.ShowDetailsCommandId));
            Assert.AreEqual(moreCommandsReadCount, model.MoreCommandsReadCount);

            model.ExposeDetails = false;
            Assert.IsTrue(item.SafeSlowInit());
            Assert.IsFalse(item.HasDetails);
            Assert.IsNull(GetShowDetailsCommand(item));
            Assert.AreEqual(moreCommandsReadCount, model.MoreCommandsReadCount);
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
    public async Task ShowDetailsCommand_RefreshPreservesConcurrentMoreCommands(bool extensionProvidesShowDetails)
    {
        using var blockedDetails = new BlockingDetails();
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = new Details { Title = "Initial details" },
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "old", Name = "Old action" })],
        };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        Task? detailsChange = null;
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            Assert.IsTrue(item.SafeSlowInit());
            var oldAction = item.AllCommands.OfType<CommandContextItemViewModel>().Single(command => command.Command.Id == "old");

            detailsChange = Task.Run(() => model.Details = blockedDetails);
            Assert.IsTrue(blockedDetails.DetailsReadEntered.Wait(TimeSpan.FromSeconds(5)));

            var newId = extensionProvidesShowDetails ? ShowDetailsCommand.ShowDetailsCommandId : "new";
            model.MoreCommands = [new CommandContextItem(new NoOpCommand { Id = newId, Name = "New action" })];
            var newAction = item.AllCommands.OfType<CommandContextItemViewModel>().Single(command => command.Command.Id == newId);
            Assert.IsTrue(oldAction.Initialized.HasFlag(InitializedState.CleanedUp));

            blockedDetails.ContinueDetailsRead.Set();
            await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.IsTrue(item.AllCommands.Contains(newAction));
            Assert.IsFalse(item.AllCommands.Contains(oldAction));
            Assert.IsFalse(newAction.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual(1, blockedDetails.SubscriptionCount);
        }
        finally
        {
            blockedDetails.ContinueDetailsRead.Set();
            if (detailsChange is not null)
            {
                await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));
            }

            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_ConcurrentRefreshKeepsReusedActionsAlive()
    {
        using var blockedDetails = new BlockingDetails();
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = new Details { Title = "Initial details" },
            MoreCommands = [new CommandContextItem(new NoOpCommand { Id = "shared", Name = "Shared action" })],
        };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        Task? detailsChange = null;
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            Assert.IsTrue(item.SafeSlowInit());
            var sharedAction = item.AllCommands.OfType<CommandContextItemViewModel>().Single(command => command.Command.Id == "shared");

            detailsChange = Task.Run(() => model.Details = blockedDetails);
            Assert.IsTrue(blockedDetails.DetailsReadEntered.Wait(TimeSpan.FromSeconds(5)));
            model.Details = new Details { Title = "Second update" };

            blockedDetails.ContinueDetailsRead.Set();
            await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.IsTrue(item.AllCommands.Contains(sharedAction));
            Assert.IsFalse(sharedAction.Initialized.HasFlag(InitializedState.CleanedUp));
            var detailsCommand = GetShowDetailsCommand(item);
            Assert.IsNotNull(detailsCommand);
            Assert.IsFalse(detailsCommand.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual("Second update", item.Details?.Title);
            Assert.AreEqual(0, blockedDetails.SubscriptionCount);
        }
        finally
        {
            blockedDetails.ContinueDetailsRead.Set();
            if (detailsChange is not null)
            {
                await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));
            }

            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }

    [TestMethod]
    public async Task ShowDetailsCommand_RefreshDoesNotPublishAfterCleanup()
    {
        using var blockedDetails = new BlockingDetails();
        var page = CreateViewModel(new ListPage());
        var model = new ListItem(new NoOpCommand { Name = "Item" })
        {
            Details = new Details { Title = "Initial details" },
        };
        var item = new ListItemViewModel(model, new(page), DefaultContextMenuFactory.Instance, ItemSurface.CommandPalette);
        Task? detailsChange = null;
        try
        {
            Assert.IsTrue(item.SafeFastInit());
            Assert.IsTrue(item.SafeInitializeProperties());
            Assert.IsTrue(item.SafeSlowInit());

            detailsChange = Task.Run(() => model.Details = blockedDetails);
            Assert.IsTrue(blockedDetails.DetailsReadEntered.Wait(TimeSpan.FromSeconds(5)));
            item.SafeCleanup();

            blockedDetails.ContinueDetailsRead.Set();
            await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreEqual(0, item.AllCommands.Count);
            Assert.IsNull(item.Details);
            Assert.AreEqual(0, blockedDetails.SubscriptionCount);
        }
        finally
        {
            blockedDetails.ContinueDetailsRead.Set();
            if (detailsChange is not null)
            {
                await detailsChange.WaitAsync(TimeSpan.FromSeconds(5));
            }

            item.SafeCleanup();
            page.SafeCleanup();
            page.Dispose();
        }
    }
}
