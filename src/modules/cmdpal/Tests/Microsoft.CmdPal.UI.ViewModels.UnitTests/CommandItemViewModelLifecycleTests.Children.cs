// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class CommandItemViewModelLifecycleTests
{
    private sealed partial class TestCommand : TestObservable, ICommand
    {
        public Action? ReadIcon { get; set; }

        public string Id => "test.command";

        public string Name { get; set; } = string.Empty;

        public IIconInfo? Icon
        {
            get
            {
                ReadIcon?.Invoke();
                return null;
            }
        }
    }

    private sealed partial class TestListItem : TestCommandItem, IListItem
    {
        public Action? ReadDerivedProperty { get; set; }

        public ITag[] Tags
        {
            get
            {
                ReadDerivedProperty?.Invoke();
                return [];
            }
        }

        public IDetails? Details
        {
            get
            {
                ReadDerivedProperty?.Invoke();
                return null;
            }
        }

        public string Section => string.Empty;

        public string TextToSuggest => string.Empty;
    }

    private sealed partial class TestContextItem : TestCommandItem, ICommandContextItem
    {
        public Action? ReadDerivedProperty { get; set; }

        public bool IsCritical
        {
            get
            {
                ReadDerivedProperty?.Invoke();
                return false;
            }
        }

        public KeyChord RequestedShortcut => default;
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void DerivedInitializationAfterCleanup_DoesNotReadExtension(bool listItem)
    {
        var context = new TestPageContext();
        var viewModel = CreateDerivedItem(listItem, context, out _, () => Assert.Fail("A cleaned item read derived properties."));

        viewModel.SafeCleanup();
        viewModel.InitializeProperties();
        viewModel.SlowInitializeProperties();

        GC.KeepAlive(context);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task CleanupDuringBaseInitialization_DoesNotReadDerivedProperties(bool listItem)
    {
        var context = new TestPageContext();
        var viewModel = CreateDerivedItem(listItem, context, out var item, () => Assert.Fail("Initialization continued after base cleanup."));

        await RunWithCleanup(viewModel, viewModel.InitializeProperties, block => item.ReadIcon = block);

        AssertNoSubscriptions(item, viewModel);
        GC.KeepAlive(context);
    }

    [TestMethod]
    [DataRow("Icon")]
    [DataRow("BeforeSubscribe")]
    [DataRow("AfterSubscribe")]
    public async Task CleanupDuringOwnedCommandInitialization_DoesNotLeaveSubscriptions(string blockAt)
    {
        var context = new TestPageContext();
        var command = new TestCommand();
        var item = new TestCommandItem { CommandValue = command };
        var viewModel = new CommandItemViewModel(new(item), new(context), null);

        await RunWithCleanup(viewModel, viewModel.InitializeProperties, block =>
        {
            switch (blockAt)
            {
                case "Icon":
                    command.ReadIcon = block;
                    break;
                case "BeforeSubscribe":
                    command.BeforeSubscribe = block;
                    break;
                case "AfterSubscribe":
                    command.AfterSubscribe = block;
                    break;
            }
        });

        Assert.AreEqual(0, command.SubscriberCount);
        Assert.IsFalse(command.RemovedDuringSubscribe);
        AssertNoSubscriptions(item, viewModel);
        GC.KeepAlive(context);
    }

    [TestMethod]
    public void OwnedCommand_SubscriptionIsOnceOnlyAndCannotRestartAfterCleanup()
    {
        var context = new TestPageContext();
        var command = new TestCommand();
        var item = new TestCommandItem { CommandValue = command };
        var viewModel = new CommandItemViewModel(new(item), new(context), null);

        try
        {
            viewModel.InitializeProperties();
            viewModel.Command.InitializeProperties();
            Assert.AreEqual(1, command.SubscriberCount);

            var lateNotification = command.CapturePropertyChanged(nameof(ICommand.Name));
            viewModel.SafeCleanup();
            command.Name = "After cleanup";
            lateNotification();
            Assert.AreEqual(string.Empty, viewModel.Command.Name);

            command.ReadIcon = () => Assert.Fail("A cleaned command restarted initialization.");
            viewModel.Command.InitializeProperties();
            Assert.AreEqual(0, command.SubscriberCount);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public async Task CleanupDuringCommandGetter_DoesNotReplaceCleanedCommand()
    {
        var context = new TestPageContext();
        var command = new TestCommand();
        var item = new TestCommandItem { CommandValue = command };
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        var originalCommand = viewModel.Command;

        await RunWithCleanup(viewModel, viewModel.FastInitializeProperties, block => item.ReadCommand = block);

        Assert.AreSame(originalCommand, viewModel.Command);
        Assert.AreEqual(0, command.SubscriberCount);
        GC.KeepAlive(context);
    }

    [TestMethod]
    public async Task CleanupDuringCommandReplacement_DoesNotAttachNewCommand()
    {
        var context = new TestPageContext();
        var previous = new TestCommand();
        var next = new TestCommand();
        var item = new TestCommandItem { CommandValue = previous };
        var viewModel = new CommandItemViewModel(new(item), new(context), null);
        viewModel.InitializeProperties();
        item.CommandValue = next;

        await RunWithCleanup(viewModel, () => item.RaisePropertyChanged(nameof(ICommandItem.Command)), block => item.ReadCommand = block);

        Assert.AreEqual(0, previous.SubscriberCount);
        Assert.AreEqual(0, next.SubscriberCount);
        Assert.AreEqual(0, viewModel.AllCommands.Count);
        GC.KeepAlive(context);
    }

    [TestMethod]
    public void CleaningSyntheticPrimary_PreservesBorrowedCommand()
    {
        var context = new TestPageContext();
        var command = new TestCommand { Name = "Primary" };
        var item = new TestCommandItem { CommandValue = command };
        var viewModel = new CommandItemViewModel(new(item), new(context), null);

        try
        {
            viewModel.SlowInitializeProperties();
            var primary = (CommandContextItemViewModel)viewModel.AllCommands[0];
            Assert.AreSame(viewModel.Command, primary.Command);
            primary.SafeCleanup();
            Assert.AreEqual(1, command.CountSubscribers<CommandViewModel>());

            viewModel.SafeCleanup();
            Assert.AreEqual(0, command.CountSubscribers<CommandViewModel>());
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    public async Task CleanupDuringSyntheticPrimaryCreation_DoesNotRepopulateCommands()
    {
        var context = new TestPageContext();
        var command = new TestCommand { Name = "Primary" };
        var item = new TestCommandItem { CommandValue = command };
        var viewModel = new CommandItemViewModel(new(item), new(context), null);

        await RunWithCleanup(viewModel, viewModel.FastInitializeProperties, block => command.BeforeSubscribe = block);

        Assert.AreEqual(0, viewModel.AllCommands.Count);
        Assert.AreEqual(0, command.CountSubscribers<CommandViewModel>());
        GC.KeepAlive(context);
    }

    private static CommandItemViewModel CreateDerivedItem(bool listItem, TestPageContext context, out TestCommandItem item, Action readDerivedProperty)
    {
        if (listItem)
        {
            var list = new TestListItem { ReadDerivedProperty = readDerivedProperty };
            item = list;
            return new ListItemViewModel(list, new(context), DefaultContextMenuFactory.Instance);
        }

        var contextItem = new TestContextItem { ReadDerivedProperty = readDerivedProperty };
        item = contextItem;
        return new CommandContextItemViewModel(contextItem, new(context));
    }

    private static async Task RunWithCleanup(ExtensionObjectViewModel viewModel, Action operation, Action<Action> configureBlock)
    {
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        configureBlock(() =>
        {
            entered.Set();
            Assert.IsTrue(resume.Wait(TestTimeout * 2), "The extension call was not released.");
        });

        var task = Task.Run(operation);
        try
        {
            Assert.IsTrue(entered.Wait(TestTimeout), "The operation did not reach the blocking call.");
            await Task.Run(viewModel.SafeCleanup).WaitAsync(TestTimeout);
        }
        finally
        {
            resume.Set();
            await task.WaitAsync(TestTimeout);
        }
    }
}
