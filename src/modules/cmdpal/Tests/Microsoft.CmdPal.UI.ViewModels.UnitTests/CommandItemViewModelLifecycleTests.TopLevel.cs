// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

public partial class CommandItemViewModelLifecycleTests
{
    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void TopLevelRows_ReuseLoadedCommandWithoutRemoteMetadataReads(bool fallback, bool recent)
    {
        var context = new TestPageContext();
        var command = new TestCommand { Name = "Loaded command" };
        var item = new TestCommandItem { CommandValue = command };
        var source = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        source.InitializeProperties();
        source.ApplyPendingUpdates();
        using var services = new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();
        var topLevel = CreateTopLevelSource(source, services, fallback);
        IListItem presentation = recent ? new RecentCommandListItem(topLevel, topLevel.Id) : topLevel;
        command.ReadId = () => Assert.Fail("A recreated host row must not fetch the extension's Id again.");
        command.ReadName = () => Assert.Fail("A recreated host row must use the cached command name.");
        command.ReadIcon = () => Assert.Fail("The loaded command must not be initialized again.");

        try
        {
            // Filtering away a row and clearing the query creates new row view models.
            for (var i = 0; i < 3; i++)
            {
                var row = new ListItemViewModel(presentation, new(context), DefaultContextMenuFactory.Instance);
                try
                {
                    row.FastInitializeProperties();
                    row.InitializeProperties();
                    Assert.AreSame(source.Command, row.Command);
                    Assert.AreEqual("test.command", row.Command.Id);
                    Assert.AreEqual("Loaded command", row.Name);
                    Assert.IsTrue(row.Command.IsSet);
                    Assert.AreEqual(1, command.CountSubscribers<CommandViewModel>(), "Only the owning command view model subscribes to the extension.");
                    Assert.AreSame(source.Command, row.PrimaryMenuItem!.Command);
                }
                finally
                {
                    row.SafeCleanup();
                }

                Assert.AreEqual(1, command.CountSubscribers<CommandViewModel>(), "Removing a row must not clean up the owner's command.");
                Assert.AreEqual(0, CountCommandHandlers(source.Command, row));
            }
        }
        finally
        {
            topLevel.Cleanup();
            Assert.AreEqual(0, command.CountSubscribers<CommandViewModel>());
            GC.KeepAlive(context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TopLevelRows_FollowCommandReplacementAndCachedNameUpdates(bool recent)
    {
        var ui = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var context = new TestPageContext(ui.Scheduler);
        var oldCommand = new TestCommand { Name = "Old command" };
        var newCommand = new TestCommand { Name = "New command" };
        var item = new TestCommandItem { CommandValue = oldCommand };
        var source = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        source.InitializeProperties();
        source.ApplyPendingUpdates();
        await ui.StartNew(() => { });
        using var services = new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();
        var topLevel = CreateTopLevelSource(source, services, fallback: false);
        IListItem presentation = recent ? new RecentCommandListItem(topLevel, topLevel.Id) : topLevel;
        var row = new ListItemViewModel(presentation, new(context), DefaultContextMenuFactory.Instance);
        row.InitializeProperties();
        var oldViewModel = source.Command;

        try
        {
            item.CommandValue = newCommand;
            item.RaisePropertyChanged(nameof(ICommandItem.Command));
            newCommand.ReadId = () => Assert.Fail("Replacement metadata belongs to the already-loaded owner.");
            newCommand.ReadName = () => Assert.Fail("The row must borrow the replacement instead of reloading it.");
            source.ApplyPendingUpdates();

            Assert.AreSame(source.Command, row.Command);
            Assert.AreNotSame(oldViewModel, row.Command);
            Assert.AreEqual(0, oldCommand.CountSubscribers<CommandViewModel>());
            Assert.AreEqual(0, CountCommandHandlers(oldViewModel, row));
            Assert.AreEqual(1, newCommand.CountSubscribers<CommandViewModel>());
            Assert.AreSame(source.Command, row.PrimaryMenuItem!.Command);

            // Duplicate adapter notifications must not accumulate handlers on the shared command.
            source.MarkPropertyDirty(nameof(source.Command));
            source.ApplyPendingUpdates();
            Assert.AreEqual(1, CountCommandHandlers(source.Command, row));

            var nameReads = 0;
            newCommand.ReadName = () => nameReads++;
            newCommand.Name = "Updated command";
            newCommand.RaisePropertyChanged(nameof(ICommand.Name));
            source.Command.ApplyPendingUpdates();
            await ui.StartNew(() => { });

            Assert.AreEqual("Updated command", row.Name);
            Assert.AreEqual("Updated command", row.PrimaryMenuItem!.Title);
            Assert.AreEqual(1, nameReads, "A name change is fetched once by the owner, not again by every row.");

            row.SafeCleanup();
            Assert.AreEqual(1, newCommand.CountSubscribers<CommandViewModel>());
            Assert.AreEqual(0, CountCommandHandlers(source.Command, row));
        }
        finally
        {
            row.SafeCleanup();
            topLevel.Cleanup();
            Assert.AreEqual(0, newCommand.CountSubscribers<CommandViewModel>());
            GC.KeepAlive(context);
        }
    }

    private static TopLevelViewModel CreateTopLevelSource(CommandItemViewModel source, IServiceProvider services, bool fallback) =>
        fallback
            ? new TopLevelFallbackViewModel(source, CommandPaletteHost.Instance, CommandProviderContext.Empty, new(), services, null, DefaultContextMenuFactory.Instance)
            : new TopLevelViewModel(source, TopLevelType.Normal, CommandPaletteHost.Instance, CommandProviderContext.Empty, new(), services, DefaultContextMenuFactory.Instance);

    [TestMethod]
    public void TopLevelRows_DoNotInitializeBorrowedCommand()
    {
        var context = new TestPageContext();
        var command = new TestCommand { Name = "Loaded command" };
        var item = new TestCommandItem { CommandValue = command };
        var source = new CommandItemViewModel(new(item), new(context), DefaultContextMenuFactory.Instance);
        source.FastInitializeProperties();
        using var services = new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();
        var topLevel = CreateTopLevelSource(source, services, fallback: false);
        var row = new ListItemViewModel(topLevel, new(context), DefaultContextMenuFactory.Instance);
        command.ReadIcon = () => Assert.Fail("A row must not initialize a command owned by another view model.");

        try
        {
            row.InitializeProperties();

            Assert.AreSame(source.Command, row.Command);
            Assert.AreEqual(0, command.CountSubscribers<CommandViewModel>());
        }
        finally
        {
            row.SafeCleanup();
            topLevel.Cleanup();
            GC.KeepAlive(context);
        }
    }

    private static int CountCommandHandlers(CommandViewModel command, object target)
    {
        var field = typeof(ObservableObject).GetField(nameof(ObservableObject.PropertyChanged), BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var handlers = (Delegate?)field.GetValue(command);
        return handlers?.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, target)) ?? 0;
    }
}
