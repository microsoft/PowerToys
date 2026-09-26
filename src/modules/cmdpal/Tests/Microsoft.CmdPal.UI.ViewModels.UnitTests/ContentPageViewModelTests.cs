// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class ContentPageViewModelTests
{
    // UI messages must finish within each test, not overwrite another test's shell context.
    private sealed class ImmediateTaskScheduler : TaskScheduler
    {
        protected override IEnumerable<Task> GetScheduledTasks() => [];

        protected override void QueueTask(Task task) => TryExecuteTask(task);

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => TryExecuteTask(task);
    }

    private sealed partial class TestAppExtensionHost : AppExtensionHost
    {
        public override string? GetExtensionDisplayName() => "Test Host";
    }

    private sealed partial class TestContentPage : ContentPage
    {
        public override IContent[] GetContent() => [];
    }

    private static CommandContextItem Command(string name) => new(new NoOpCommand { Name = name });

    private static ContentPageViewModel CreateViewModel(TestContentPage page) =>
        new(page, new ImmediateTaskScheduler(), new TestAppExtensionHost(), CommandProviderContext.Empty);

    [TestMethod]
    public void AllCommands_ReturnCachedSnapshot()
    {
        // Content pages should expose stable snapshots, not the live Commands
        // list, so repeated reads don't allocate and callers can't observe
        // in-place list mutations.
        var page = new TestContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Commands =
            [
                Command("Primary"),
                Command("Secondary"),
            ],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var allCommands = viewModel.AllCommands;

        Assert.AreSame(allCommands, viewModel.AllCommands);
        Assert.AreEqual(2, allCommands.Count);
        Assert.AreEqual("Primary", viewModel.PrimaryCommand?.Name);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand?.Name);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void ActionRoles_IgnoreSeparatorPosition(int separatorIndex)
    {
        List<IContextItem> commands = [Command("Primary"), Command("Secondary")];
        commands.Insert(separatorIndex, new Separator("Group"));
        var page = new TestContentPage
        {
            Id = "content.page",
            Commands = [.. commands],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var actions = viewModel.AllCommands.OfType<CommandContextItemViewModel>().ToArray();
        Assert.IsTrue(actions.All(command => command.DisplayShortcut is null));
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);
        Assert.AreSame(viewModel.PrimaryCommand, actions[0]);
        Assert.AreSame(viewModel.SecondaryCommand, actions[1]);
        Assert.AreEqual(CommandContextItemViewModel.PrimaryShortcut, actions[0].DisplayShortcut);
        Assert.AreEqual(new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0), actions[1].DisplayShortcut);
        Assert.IsFalse(viewModel.HasOverflowCommands);
        Assert.IsTrue(viewModel.CanOpenContextMenu);

        page.Commands = [.. page.Commands, Command("Additional")];

        Assert.IsTrue(viewModel.HasOverflowCommands);
        menu.Close();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReservedRequestedShortcut_DoesNotOverrideDefaultAction(bool primaryShortcut)
    {
        var shortcut = primaryShortcut ? CommandContextItemViewModel.PrimaryShortcut : CommandContextItemViewModel.SecondaryShortcut;
        var requested = Command("Requested");
        requested.RequestedShortcut = shortcut;
        var viewModel = CreateViewModel(new TestContentPage
        {
            Commands = [Command("Primary"), Command("Secondary"), requested],
        });
        viewModel.InitializeProperties();

        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);
        var action = primaryShortcut ? viewModel.PrimaryCommand : viewModel.SecondaryCommand;
        Assert.AreEqual(shortcut, ((CommandContextItemViewModel)action!).DisplayShortcut);
        Assert.IsNull(((IContextMenuContext)viewModel).FindKeybinding(shortcut));
        Assert.IsNull(menu.FindKeybinding(shortcut));
        var requestedCommand = (CommandContextItemViewModel)viewModel.AllCommands[2];
        Assert.IsFalse(requestedCommand.HasRequestedShortcut);
        Assert.IsNull(requestedCommand.DisplayShortcut);
        menu.Close();
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DefaultShortcuts_IgnoreReservedRequestsEvenForActionsWithSubmenus(bool hasSubmenu)
    {
        var primary = Command("Primary");
        primary.RequestedShortcut = CommandContextItemViewModel.PrimaryShortcut;
        primary.MoreCommands = hasSubmenu ? [Command("Child")] : [];
        var duplicate = Command("Duplicate");
        duplicate.RequestedShortcut = CommandContextItemViewModel.PrimaryShortcut;
        var viewModel = CreateViewModel(new TestContentPage { Commands = [primary, duplicate] });
        viewModel.InitializeProperties();
        viewModel.PrimaryCommand!.SlowInitializeProperties();

        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        Assert.IsNull(((CommandContextItemViewModel)viewModel.PrimaryCommand!).DisplayShortcut);
        menu.PrepareForOpen(viewModel);
        Assert.IsNull(menu.FindKeybinding(CommandContextItemViewModel.PrimaryShortcut));
        Assert.AreEqual(CommandContextItemViewModel.PrimaryShortcut, ((CommandContextItemViewModel)viewModel.PrimaryCommand!).DisplayShortcut);
        Assert.AreEqual(CommandContextItemViewModel.SecondaryShortcut, ((CommandContextItemViewModel)viewModel.SecondaryCommand!).DisplayShortcut);
        menu.Close();
    }

    [TestMethod]
    public void CommandsUpdate_RefreshesSnapshotsConsistently()
    {
        // Updating the model commands should swap in a new coherent snapshot.
        // The old snapshots stay intact, and the new cached values agree on
        // counts and primary/secondary actions.
        var page = new TestContentPage
        {
            Id = "content.page",
            Name = "Content Page",
            Title = "Content Page",
            Commands =
            [
                Command("Primary"),
                Command("Secondary"),
            ],
        };

        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        var oldAllCommands = viewModel.AllCommands;

        page.Commands =
        [
            Command("Updated Primary"),
            new Separator("Group"),
            Command("Updated Secondary"),
        ];

        Assert.AreEqual(2, oldAllCommands.Count);

        Assert.AreEqual(3, viewModel.AllCommands.Count);
        Assert.IsTrue(viewModel.HasCommands);
        Assert.IsFalse(viewModel.HasOverflowCommands);
        Assert.AreEqual("Updated Primary", viewModel.PrimaryCommand?.Name);
        Assert.AreEqual("Updated Secondary", viewModel.SecondaryCommand?.Name);
        Assert.AreEqual("Updated Secondary", viewModel.SecondaryCommandName);
    }

    [TestMethod]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Menu)]
    public void RequestedPrimaryShortcut_UsesTheSameRuleWithTheMenuOpenOrClosed(VirtualKey primaryKeyValue, VirtualKeyModifiers modifiers)
    {
        var primaryKey = new KeyChord(modifiers, (int)primaryKeyValue, 0);
        var secondaryKey = new KeyChord(0, (int)VirtualKey.F7, 0);
        var primary = Command("Primary");
        primary.RequestedShortcut = primaryKey;
        var secondary = Command("Secondary");
        secondary.RequestedShortcut = secondaryKey;
        var duplicate = Command("Duplicate primary shortcut");
        duplicate.RequestedShortcut = primaryKey;
        var page = new TestContentPage
        {
            Id = "content.page",
            Commands = [new Separator("Group"), primary, secondary, duplicate],
        };
        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        Assert.AreSame(viewModel.PrimaryCommand, ((IContextMenuContext)viewModel).FindKeybinding(primaryKey));
        Assert.AreSame(viewModel.SecondaryCommand, ((IContextMenuContext)viewModel).FindKeybinding(secondaryKey));
        Assert.AreEqual(primaryKey, ((CommandContextItemViewModel)viewModel.PrimaryCommand!).DisplayShortcut);
        Assert.AreEqual(secondaryKey, ((CommandContextItemViewModel)viewModel.SecondaryCommand!).DisplayShortcut);

        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);
        PerformCommandMessage? invocation = null;
        menu.CommandInvoking += (_, message) => invocation = message;
        Assert.AreEqual(ContextKeybindingResult.Hide, menu.InvokeCommand(menu.FindKeybinding(primaryKey)));
        Assert.IsNotNull(invocation);
        Assert.AreSame(primary, invocation.Context);

        page.Commands = [new Separator("Empty")];

        Assert.IsNull(viewModel.PrimaryCommand);
        Assert.IsNull(viewModel.SecondaryCommand);
        Assert.IsFalse(viewModel.HasOverflowCommands);
        Assert.IsFalse(viewModel.CanOpenContextMenu);
        Assert.IsNull(((IContextMenuContext)viewModel).FindKeybinding(primaryKey));
        menu.Close();
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void HiddenCommands_DoNotOccupyActionSlots(int visibleCommands)
    {
        List<IContextItem> entries = [Command(string.Empty), new Separator("Group"), Command(string.Empty), Command(string.Empty)];
        for (var i = 0; i < visibleCommands; i++)
        {
            entries.Add(Command($"Visible {i}"));
        }

        var page = new TestContentPage { Id = "hidden.commands", Commands = [.. entries] };
        var viewModel = CreateViewModel(page);
        viewModel.InitializeProperties();

        Assert.AreEqual(visibleCommands > 0 ? "Visible 0" : null, viewModel.PrimaryCommand?.Name);
        Assert.AreEqual(visibleCommands > 1 ? "Visible 1" : null, viewModel.SecondaryCommand?.Name);
        Assert.AreEqual(visibleCommands > 2, viewModel.HasOverflowCommands);
        Assert.AreEqual(visibleCommands > 0, viewModel.CanOpenContextMenu);
        Assert.AreEqual(entries.Count, viewModel.AllCommands.Count);
        viewModel.SafeCleanup();
    }

    [TestMethod]
    public async Task CommandVisibilityChanges_RefreshActionRoles()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var first = Command("First");
        var second = Command("Second");
        var third = Command("Third");
        var page = new TestContentPage { Id = "changing.visibility", Commands = [first, second, third] };
        var viewModel = new ContentPageViewModel(page, uiTasks.Scheduler!, new TestAppExtensionHost(), CommandProviderContext.Empty);
        viewModel.InitializeProperties();
        var commands = viewModel.AllCommands.OfType<CommandContextItemViewModel>().ToArray();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        await uiTasks.StartNew(() => menu.PrepareForOpen(viewModel));
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(viewModel.AllCommands))
            {
                return;
            }

            if (!viewModel.CanOpenContextMenu && !viewModel.HasOverflowCommands &&
                commands.All(command => command.DisplayShortcut is null))
            {
                hidden.TrySetResult();
            }

            if (viewModel.PrimaryCommand?.Name == "Restored" && !viewModel.HasOverflowCommands &&
                commands[0].DisplayShortcut == CommandContextItemViewModel.PrimaryShortcut &&
                commands[1].DisplayShortcut is null && commands[2].DisplayShortcut is null)
            {
                restored.TrySetResult();
            }
        };

        try
        {
            Assert.IsTrue(viewModel.HasOverflowCommands);
            ((NoOpCommand)first.Command!).Name = string.Empty;
            ((NoOpCommand)second.Command!).Name = string.Empty;
            ((NoOpCommand)third.Command!).Name = string.Empty;
            await hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsNull(viewModel.PrimaryCommand);
            Assert.IsNull(viewModel.SecondaryCommand);
            Assert.IsTrue(commands.All(command => command.DisplayShortcut is null));

            ((NoOpCommand)first.Command!).Name = "Restored";
            await restored.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(viewModel.HasCommands);
            Assert.IsTrue(viewModel.CanOpenContextMenu);
            Assert.AreEqual(3, viewModel.AllCommands.Count);
            Assert.AreEqual(new KeyChord(0, (int)VirtualKey.Enter, 0), commands[0].DisplayShortcut);
            Assert.IsNull(commands[1].DisplayShortcut);
        }
        finally
        {
            await uiTasks.StartNew(menu.Close);
            viewModel.SafeCleanup();
        }
    }

    [TestMethod]
    public async Task CommandNameChanges_DoNotRepublishUnchangedMenu()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var commands = Enumerable.Range(0, 32).Select(index => new NoOpCommand { Name = $"Child {index}" }).ToArray();
        var page = new TestContentPage
        {
            Commands = commands.Select(command => new CommandContextItem(command)).ToArray(),
        };
        var viewModel = new ContentPageViewModel(page, uiTasks.Scheduler!, new TestAppExtensionHost(), CommandProviderContext.Empty);
        viewModel.InitializeProperties();
        var children = viewModel.AllCommands.OfType<CommandContextItemViewModel>().ToArray();

        try
        {
            foreach (var child in children)
            {
                child.ApplyPendingUpdates();
            }

            viewModel.ApplyPendingUpdates();
            await uiTasks.StartNew(() => { });
            var allCommands = viewModel.AllCommands;
            List<string> menuNotifications = [];
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(viewModel.AllCommands) or nameof(viewModel.PrimaryCommand) or nameof(viewModel.SecondaryCommand))
                {
                    menuNotifications.Add(args.PropertyName);
                }
            };

            for (var i = 0; i < commands.Length; i++)
            {
                commands[i].Name = $"Renamed {i}";
                children[i].Command.ApplyPendingUpdates();
            }

            await uiTasks.StartNew(() => { });
            foreach (var child in children)
            {
                child.ApplyPendingUpdates();
            }

            viewModel.ApplyPendingUpdates();
            await uiTasks.StartNew(() => { });

            Assert.AreEqual(0, menuNotifications.Count, "Name changes must not republish unchanged menu entries or action roles.");
            Assert.AreSame(allCommands, viewModel.AllCommands);
            Assert.AreEqual("Renamed 1", viewModel.SecondaryCommandName);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
    }
}
