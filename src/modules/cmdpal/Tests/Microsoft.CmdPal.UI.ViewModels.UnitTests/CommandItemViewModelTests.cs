// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public partial class CommandItemViewModelTests
{
    private sealed class TestPageContext(TaskScheduler? scheduler = null) : IPageContext
    {
        public TaskScheduler Scheduler => scheduler ?? TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    [TestMethod]
    public void AllCommands_ReturnCachedSnapshot()
    {
        // The public getters should return cached read-only snapshots, so
        // repeated reads don't allocate a new list when the backing data hasn't
        // changed.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        var allCommands = viewModel.AllCommands;

        Assert.AreSame(allCommands, viewModel.AllCommands);
        Assert.AreEqual(2, allCommands.Count);
    }

    [TestMethod]
    public void InitializeProperties_CachesDockCommandId()
    {
        var pageContext = new TestPageContext();
        var item = new ListItem(new NoOpCommand { Name = "Primary" });
        item.GetProperties()[WellKnownExtensionAttributes.DockCommandId] = "provider.item.dock";

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.InitializeProperties();

        Assert.AreEqual("provider.item.dock", viewModel.DockCommandId);
    }

    [TestMethod]
    public void DockBandItems_UsesWrappedListItems()
    {
        var pageContext = new TestPageContext();
        var child = new ListItem(new NoOpCommand()) { Title = "Child" };
        var band = new WrappedDockItem([child], "provider.band", "Band");
        var viewModel = new CommandItemViewModel(new(band), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.InitializeProperties();

        var items = DockBandViewModel.GetItemsForDisplay(viewModel);

        Assert.AreEqual(1, items.Length);
        Assert.AreSame(child, items[0]);
    }

    [TestMethod]
    public void SecondaryCommand_IgnoresLeadingSeparators()
    {
        // Separators remain in the menu but do not occupy an action slot.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
            MoreCommands =
            [
                new Separator("Group"),
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.IsTrue(viewModel.HasSubmenu);
        Assert.IsNotNull(viewModel.SecondaryCommand);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand.Name);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(1)]
    public void SingleSecondaryAction_HidesMoreButtonButKeepsContextMenuAvailable(int separatorIndex)
    {
        var pageContext = new TestPageContext();
        List<IContextItem> moreCommands = [new CommandContextItem(new NoOpCommand { Name = "Secondary" })];
        if (separatorIndex >= 0)
        {
            moreCommands.Insert(separatorIndex, new Separator("Group"));
        }

        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
            MoreCommands = [.. moreCommands],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.IsFalse(viewModel.HasOverflowCommands);
        Assert.IsTrue(viewModel.CanOpenContextMenu);
    }

    [TestMethod]
    public void ActionsBeyondSecondary_ShowMoreButton()
    {
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
                new CommandContextItem(new NoOpCommand { Name = "Additional" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.IsTrue(viewModel.HasOverflowCommands);
    }

    [TestMethod]
    public void FastInitializeProperties_CreatesPrimaryContextItem()
    {
        // Context menus are opened from fast-initialized list items before slow init completes.
        // The synthetic primary command must already exist so the first right-click can open the menu.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Title = "Primary",
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.FastInitializeProperties();

        Assert.AreEqual(1, viewModel.AllCommands.Count);
        Assert.IsTrue(viewModel.CanOpenContextMenu);
        Assert.AreEqual("Primary", ((CommandContextItemViewModel)viewModel.AllCommands[0]).Name);
    }

    [TestMethod]
    public void LatePrimaryCommandCreation_AddsPrimaryToAllCommands()
    {
        // Reproduces issue where SlowInitializeProperties runs before a real primary command exists.
        // The late-arriving command should still create the synthetic primary context item and prepend it to AllCommands.
        var pageContext = new TestPageContext();
        var item = new CommandItem()
        {
            Command = null,
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.AreEqual(1, viewModel.AllCommands.Count);
        Assert.AreEqual("Secondary", ((CommandContextItemViewModel)viewModel.AllCommands[0]).Name);

        item.Command = new NoOpCommand { Name = "Primary" };

        Assert.AreEqual(2, viewModel.AllCommands.Count);
        Assert.AreEqual("Primary", ((CommandContextItemViewModel)viewModel.AllCommands[0]).Name);
        Assert.AreEqual("Secondary", ((CommandContextItemViewModel)viewModel.AllCommands[1]).Name);
        Assert.IsTrue(viewModel.HasSubmenu);
        Assert.AreEqual("Secondary", viewModel.SecondaryCommand?.Name);
    }

    [TestMethod]
    public void SyntheticPrimaryContextItem_UpdatesSubtitleAndCachedSubtitleTarget()
    {
        // The synthetic primary context item copies subtitle state from the parent CommandItemViewModel.
        // When subtitle changes later, both the exposed subtitle and its cached fuzzy-search target must refresh.
        var pageContext = new TestPageContext();
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            Subtitle = "before",
            MoreCommands =
            [
                new CommandContextItem(new NoOpCommand { Name = "Secondary" }),
            ],
        };

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        var primaryContextItem = (CommandContextItemViewModel)viewModel.AllCommands[0];
        var matcher = new PrecomputedFuzzyMatcher(new PrecomputedFuzzyMatcherOptions());

        Assert.AreEqual("before", primaryContextItem.Subtitle);
        Assert.AreEqual("before", primaryContextItem.GetSubtitleTarget(matcher).Original);

        item.Subtitle = "after unique";

        Assert.AreEqual("after unique", primaryContextItem.Subtitle);
        Assert.AreEqual("after unique", primaryContextItem.GetSubtitleTarget(matcher).Original);
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(false, 1)]
    [DataRow(false, 2)]
    [DataRow(true, 0)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    public void MenuRoles_HandleMissingPrimaryAndSeparators(bool hasPrimary, int childCount)
    {
        var pageContext = new TestPageContext();
        List<IContextItem> entries = [new Separator("Before")];
        for (var i = 0; i < childCount; i++)
        {
            entries.Add(new CommandContextItem(new NoOpCommand { Name = $"Child {i}" }));
        }

        entries.Add(new Separator("After"));
        var item = new CommandItem(new NoOpCommand { Name = hasPrimary ? "Primary" : string.Empty })
        {
            MoreCommands = [.. entries],
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.AreSame(viewModel, viewModel.PrimaryCommand);
        Assert.AreSame(item, viewModel.PrimaryCommand?.Model.Unsafe);
        Assert.AreEqual(childCount > 0, viewModel.HasSubmenu);
        Assert.AreEqual(childCount > 1, viewModel.HasOverflowCommands);
        Assert.AreEqual(hasPrimary || childCount > 0, viewModel.CanOpenContextMenu);
        Assert.AreEqual(childCount + 2 + (hasPrimary ? 1 : 0), viewModel.AllCommands.Count);
    }

    [TestMethod]
    public void PresentationAndSdkAdapter_UseCachedEntries()
    {
        var pageContext = new TestPageContext();
        var key = new KeyChord(0, (int)VirtualKey.F6, 0);
        var secondary = new CommandContextItem(new NoOpCommand { Name = "Secondary" }) { RequestedShortcut = key };
        var item = new CountingCommandItem
        {
            Command = new NoOpCommand { Name = "Primary" },
            MoreCommands = [new Separator("Group"), secondary],
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var readCount = item.MenuReadCount;
        var snapshot = viewModel.AllCommands;

        for (var i = 0; i < 3; i++)
        {
            Assert.AreSame(snapshot, viewModel.AllCommands);
            Assert.IsTrue(viewModel.HasSubmenu);
            Assert.IsFalse(viewModel.HasOverflowCommands);
            Assert.IsTrue(viewModel.CanOpenContextMenu);
            Assert.AreSame(viewModel.SecondaryCommand, ((IContextMenuContext)viewModel).Keybindings()[key]);

            List<IContextItem?> sdkEntries = [];
            viewModel.CopySdkContextItemsTo(sdkEntries);
            Assert.AreEqual(2, sdkEntries.Count);
            Assert.IsInstanceOfType<SeparatorViewModel>(sdkEntries[0]);
            Assert.AreSame(secondary, sdkEntries[1]);
        }

        Assert.AreEqual(readCount, item.MenuReadCount, "Presentation reads must not fetch SDK menu entries again.");
    }

    [TestMethod]
    public async Task SdkMenuUpdate_PublishesPresentationStateAndCleanupClearsIt()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var pageContext = new TestPageContext(uiTasks.Scheduler);
        var item = new CountingCommandItem
        {
            Command = new NoOpCommand { Name = "Primary" },
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Secondary" })],
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        viewModel.ApplyPendingUpdates();
        await uiTasks.StartNew(() => { });

        using var services = new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();
        var adapter = new TopLevelViewModel(viewModel, TopLevelType.Normal, CommandPaletteHost.Instance, CommandProviderContext.Empty, new(), services, item, DefaultContextMenuFactory.Instance);
        var wrappedViewModel = new CommandItemViewModel(new(adapter), new(pageContext), DefaultContextMenuFactory.Instance);
        wrappedViewModel.SlowInitializeProperties();
        var wrappedNotified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        wrappedViewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(viewModel.AllCommands) && wrappedViewModel.HasOverflowCommands)
            {
                wrappedNotified.TrySetResult();
            }
        };

        var snapshot = viewModel.AllCommands;
        ConcurrentQueue<string?> notifications = new();
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            notifications.Enqueue(args.PropertyName);
            if (notifications.Contains(nameof(viewModel.AllCommands)) && notifications.Contains(nameof(viewModel.HasOverflowCommands)))
            {
                notified.TrySetResult();
            }
        };

        var menuReadCount = item.MenuReadCount;
        item.MoreCommands =
        [
            new Separator("Group"),
            new CommandContextItem(new NoOpCommand { Name = "Updated secondary" }),
            new CommandContextItem(new NoOpCommand { Name = "Additional" }),
        ];
        viewModel.ApplyPendingUpdates();

        Assert.AreEqual(2, snapshot.Count);
        Assert.AreEqual(4, viewModel.AllCommands.Count);
        Assert.AreEqual("Updated secondary", viewModel.SecondaryCommand?.Name);
        Assert.IsTrue(viewModel.HasOverflowCommands);
        wrappedViewModel.ApplyPendingUpdates();
        await Task.WhenAll(notified.Task, wrappedNotified.Task).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(menuReadCount + 1, item.MenuReadCount);
        Assert.AreEqual(4, wrappedViewModel.AllCommands.Count);
        Assert.AreEqual("Updated secondary", wrappedViewModel.SecondaryCommand?.Name);
        Assert.IsTrue(wrappedViewModel.HasOverflowCommands);
        Assert.AreSame(adapter, wrappedViewModel.PrimaryCommand?.Model.Unsafe);
        await uiTasks.StartNew(() =>
        {
            Assert.IsTrue(notifications.Contains(nameof(viewModel.AllCommands)));
            Assert.IsTrue(notifications.Contains(nameof(viewModel.HasOverflowCommands)));
            Assert.IsFalse(notifications.Contains(nameof(ICommandItem.MoreCommands)));
        });

        wrappedViewModel.SafeCleanup();
        viewModel.SafeCleanup();

        Assert.AreEqual(0, viewModel.AllCommands.Count);
        Assert.IsFalse(viewModel.HasSubmenu);
        Assert.IsFalse(viewModel.HasOverflowCommands);
        Assert.IsFalse(viewModel.CanOpenContextMenu);
    }

    [TestMethod]
    public void ShortcutNavigation_UsesTheCurrentSubmenuAndPreservesInvocationContext()
    {
        var pageContext = new TestPageContext();
        var key = new KeyChord(0, (int)VirtualKey.F6, 0);
        var child = new CommandContextItem(new NoOpCommand { Name = "Child" }) { RequestedShortcut = key };
        var parent = new CommandContextItem(new NoOpCommand { Name = "Parent" })
        {
            RequestedShortcut = key,
            MoreCommands = [child],
        };
        var item = new CommandItem(new NoOpCommand { Name = "Root" }) { MoreCommands = [parent] };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new())) { SelectedItem = viewModel };
        PerformCommandMessage? invocation = null;
        menu.CommandInvoking += (_, message) => invocation = message;

        Assert.AreEqual(ContextKeybindingResult.KeepOpen, menu.CheckKeybinding(false, false, false, false, VirtualKey.F6));
        Assert.IsTrue(menu.CanPopContextStack());
        Assert.IsNull(invocation);
        Assert.AreSame(child, menu.FilteredItems.OfType<CommandContextItemViewModel>().Last().Model.Unsafe);

        Assert.AreEqual(ContextKeybindingResult.Hide, menu.CheckKeybinding(false, false, false, false, VirtualKey.F6));
        Assert.IsNotNull(invocation);
        Assert.AreSame(child, invocation.Context);
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(false, 1)]
    [DataRow(false, 2)]
    [DataRow(true, 0)]
    [DataRow(true, 1)]
    [DataRow(true, 2)]
    public void HiddenContextCommands_DoNotOccupyActionSlots(bool hasPrimary, int visibleChildren)
    {
        var pageContext = new TestPageContext();
        List<IContextItem> entries =
        [
            new CommandContextItem(new NoOpCommand()),
            new Separator("Group"),
            new CommandContextItem(new NoOpCommand()),
            new CommandContextItem(new NoOpCommand()),
        ];
        for (var i = 0; i < visibleChildren; i++)
        {
            entries.Add(new CommandContextItem(new NoOpCommand { Name = $"Visible {i}" }));
        }

        var item = new CommandItem(new NoOpCommand { Name = hasPrimary ? "Primary" : string.Empty }) { MoreCommands = [.. entries] };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.IsTrue(viewModel.HasSubmenu);
        Assert.AreEqual(visibleChildren > 1, viewModel.HasOverflowCommands);
        Assert.AreEqual(hasPrimary || visibleChildren > 0, viewModel.CanOpenContextMenu);
        Assert.AreEqual(visibleChildren > 0 ? "Visible 0" : null, viewModel.SecondaryCommand?.Name);
        Assert.AreEqual(entries.Count + (hasPrimary ? 1 : 0), viewModel.AllCommands.Count);
        viewModel.SafeCleanup();
    }

    [TestMethod]
    public void UnnamedSubmenuChildren_NavigateWithoutInvokingParent()
    {
        var pageContext = new TestPageContext();
        var parent = new CommandContextItem(new NoOpCommand())
        {
            Title = "Submenu",
            MoreCommands = [new Separator("Group"), new CommandContextItem(new NoOpCommand())],
        };
        var item = new CommandItem(new NoOpCommand { Name = "Root" }) { MoreCommands = [parent] };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var parentViewModel = viewModel.AllCommands.OfType<CommandContextItemViewModel>()
            .Single(command => ReferenceEquals(command.Model.Unsafe, parent));
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new())) { SelectedItem = viewModel };
        var invoked = false;
        menu.CommandInvoking += (_, _) => invoked = true;

        try
        {
            Assert.AreEqual(ContextKeybindingResult.KeepOpen, menu.InvokeCommand(parentViewModel));
            Assert.IsTrue(menu.CanPopContextStack());
            Assert.IsFalse(invoked);
            CollectionAssert.AreEqual(parentViewModel.AllCommands.ToArray(), menu.FilteredItems.ToArray());
            Assert.IsTrue(parentViewModel.HasSubmenu);
            Assert.IsNull(parentViewModel.SecondaryCommand);
            Assert.IsFalse(parentViewModel.HasOverflowCommands);
            Assert.IsFalse(parentViewModel.CanOpenContextMenu);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public async Task ContextCommandVisibilityChanges_RefreshActionRoles()
    {
        var pageContext = new TestPageContext();
        var first = new NoOpCommand { Name = "First" };
        var second = new NoOpCommand { Name = "Second" };
        var item = new CommandItem(new NoOpCommand())
        {
            MoreCommands = [new CommandContextItem(first), new CommandContextItem(second)],
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, _) =>
        {
            if (!viewModel.CanOpenContextMenu && !viewModel.HasOverflowCommands)
            {
                hidden.TrySetResult();
            }

            if (viewModel.SecondaryCommand?.Name == "Restored" && !viewModel.HasOverflowCommands)
            {
                restored.TrySetResult();
            }
        };

        try
        {
            Assert.IsTrue(viewModel.HasOverflowCommands);
            first.Name = string.Empty;
            second.Name = string.Empty;
            await hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsNull(viewModel.SecondaryCommand);
            Assert.IsTrue(viewModel.HasSubmenu);

            first.Name = "Restored";
            await restored.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(viewModel.CanOpenContextMenu);
            Assert.IsTrue(viewModel.HasSubmenu);
            Assert.AreEqual(2, viewModel.AllCommands.Count);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public async Task SyntheticPrimary_DoesNotRebuildWrappedSdkMenu()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var pageContext = new TestPageContext(uiTasks.Scheduler);
        var primary = new NoOpCommand();
        var item = new CountingCommandItem
        {
            Command = primary,
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Secondary" })],
        };
        var source = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        source.SlowInitializeProperties();
        source.ApplyPendingUpdates();
        await uiTasks.StartNew(() => { });

        using var services = new ServiceCollection().AddSingleton(Mock.Of<ISettingsService>()).BuildServiceProvider();
        var adapter = new TopLevelViewModel(source, TopLevelType.Normal, CommandPaletteHost.Instance, CommandProviderContext.Empty, new(), services, item, DefaultContextMenuFactory.Instance);
        var wrapped = new ListItemViewModel(adapter, new(pageContext), DefaultContextMenuFactory.Instance);
        wrapped.SlowInitializeProperties();
        var secondary = (CommandContextItemViewModel)wrapped.SecondaryCommand!;
        var reads = item.MenuReadCount;
        ConcurrentQueue<string> sdkNotifications = new();
        adapter.PropChanged += (_, args) => sdkNotifications.Enqueue(args.PropertyName);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        source.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(source.AllCommands) && source.AllCommands.Count == 2)
            {
                published.TrySetResult();
            }
        };

        try
        {
            primary.Name = "Late primary";
            await published.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await uiTasks.StartNew(() => { });

            Assert.IsFalse(sdkNotifications.Contains(nameof(ICommandItem.MoreCommands)));
            Assert.AreSame(secondary, wrapped.SecondaryCommand);
            Assert.IsFalse(secondary.Initialized.HasFlag(InitializedState.CleanedUp));
            Assert.AreEqual(reads, item.MenuReadCount);
        }
        finally
        {
            wrapped.SafeCleanup();
            source.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public async Task ContextCommandNameChanges_DoNotRepublishUnchangedMenu()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var commands = Enumerable.Range(0, 32).Select(index => new NoOpCommand { Name = $"Child {index}" }).ToArray();
        var pageContext = new TestPageContext(uiTasks.Scheduler);
        var item = new CommandItem(new NoOpCommand())
        {
            MoreCommands = commands.Select(command => new CommandContextItem(command)).ToArray(),
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
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
                if (args.PropertyName is nameof(viewModel.AllCommands) or nameof(viewModel.SecondaryCommand))
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
            Assert.AreEqual("Renamed 0", viewModel.SecondaryCommandName);
        }
        finally
        {
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    private sealed partial class CountingCommandItem : CommandItem
    {
        public int MenuReadCount { get; private set; }

        public override IContextItem[] MoreCommands
        {
            get
            {
                MenuReadCount++;
                return base.MoreCommands;
            }

            set => base.MoreCommands = value;
        }
    }
}
