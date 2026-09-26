// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
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
    private sealed partial class PropertiesTestItem : ListItem
    {
        public void NotifyPropertiesChanged() => OnPropertyChanged("Properties");
    }

    private sealed class TestPageContext(TaskScheduler? scheduler = null) : IPageContext
    {
        public TaskScheduler Scheduler => scheduler ?? TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null)
        {
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
        }
    }

    private sealed partial class ThrowingFiltersListPage : ListPage
    {
        public override IFilters? Filters
        {
            get => throw new AssertFailedException("Command shape detection must not access filters.");
            set => throw new AssertFailedException("Command shape detection must not access filters.");
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
    public void PropertiesNotification_RefreshesAndClearsDockCommandId()
    {
        var pageContext = new TestPageContext();
        var item = new PropertiesTestItem();
        item.GetProperties()[WellKnownExtensionAttributes.DockCommandId] = "provider.item.dock";

        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        try
        {
            viewModel.InitializeProperties();

            item.GetProperties()[WellKnownExtensionAttributes.DockCommandId] = "provider.updated.dock";
            item.NotifyPropertiesChanged();

            Assert.AreEqual("provider.updated.dock", viewModel.DockCommandId);

            item.GetProperties().Remove(WellKnownExtensionAttributes.DockCommandId);
            item.NotifyPropertiesChanged();

            Assert.IsNull(viewModel.DockCommandId);
        }
        finally
        {
            viewModel.SafeCleanup();
        }
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
        Assert.IsTrue(viewModel.Command.IsInvokableCommand);
        Assert.IsFalse(viewModel.Command.IsPage);
        Assert.IsFalse(viewModel.Command.IsListPage);
    }

    [TestMethod]
    public void FastInitializeProperties_CachesListPageShapeWithoutAccessingFilters()
    {
        var pageContext = new TestPageContext();
        var page = new ThrowingFiltersListPage
        {
            Name = "List page",
        };
        var item = new CommandItem(page) { Title = page.Name };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);

        viewModel.FastInitializeProperties();

        Assert.IsTrue(viewModel.Command.IsPage);
        Assert.IsTrue(viewModel.Command.IsListPage);
        Assert.IsFalse(viewModel.Command.IsInvokableCommand);
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
    [DataRow(false, 0, false)]
    [DataRow(false, 1, false)]
    [DataRow(false, 2, false)]
    [DataRow(true, 0, false)]
    [DataRow(true, 1, false)]
    [DataRow(true, 2, false)]
    [DataRow(false, 0, true)]
    [DataRow(false, 1, true)]
    [DataRow(false, 2, true)]
    [DataRow(true, 0, true)]
    [DataRow(true, 1, true)]
    [DataRow(true, 2, true)]
    public void MenuRoles_HandleMissingPrimaryAndSeparators(bool hasPrimary, int childCount, bool isDock)
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
        var viewModel = isDock
            ? new DockItemViewModel(new(item), new(pageContext), true, true, DefaultContextMenuFactory.Instance)
            : new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();

        Assert.AreSame(viewModel, viewModel.PrimaryCommand);
        Assert.AreSame(item, viewModel.PrimaryCommand?.Model.Unsafe);
        Assert.AreEqual(childCount > 0, viewModel.HasSubmenu);
        Assert.AreEqual(childCount > 1, viewModel.HasOverflowCommands);
        Assert.AreEqual(hasPrimary || childCount > 0, viewModel.CanOpenContextMenu);
        Assert.AreEqual(childCount + 2 + (hasPrimary ? 1 : 0), viewModel.AllCommands.Count);

        Assert.IsTrue(viewModel.AllCommands.OfType<CommandContextItemViewModel>().All(command => command.DisplayShortcut is null), "Initializing an item must not prepare menu hints.");
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);

        if (hasPrimary)
        {
            Assert.AreEqual(CommandContextItemViewModel.PrimaryShortcut, ((CommandContextItemViewModel)viewModel.AllCommands[0]).DisplayShortcut);
        }

        if (childCount > 0)
        {
            Assert.AreEqual(new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0), ((CommandContextItemViewModel)viewModel.SecondaryCommand!).DisplayShortcut);
        }

        Assert.IsNull(((IContextMenuContext)viewModel).FindKeybinding(CommandContextItemViewModel.PrimaryShortcut), "Display hints must not become requested shortcuts.");
        Assert.IsNull(((IContextMenuContext)viewModel).FindKeybinding(CommandContextItemViewModel.SecondaryShortcut));
        menu.Close();
        Assert.IsTrue(viewModel.AllCommands.OfType<CommandContextItemViewModel>().All(command => command.DisplayShortcut is null));
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 0)]
    [DataRow(false, 28)]
    [DataRow(true, 28)]
    public void ReservedRequestedShortcut_DoesNotOverrideDefaultAction(bool primaryShortcut, int scanCode)
    {
        var shortcut = primaryShortcut ? CommandContextItemViewModel.PrimaryShortcut : CommandContextItemViewModel.SecondaryShortcut;
        var pageContext = new TestPageContext();
        var requested = new CommandContextItem(new NoOpCommand { Name = "Requested" })
        {
            RequestedShortcut = new KeyChord(shortcut.Modifiers, shortcut.Vkey, scanCode),
        };
        var item = new CommandItem(new NoOpCommand { Name = "Primary" })
        {
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Secondary" }), requested],
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        try
        {
            viewModel.SlowInitializeProperties();
            menu.PrepareForOpen(viewModel);
            var action = primaryShortcut ? viewModel.PrimaryCommand : viewModel.SecondaryCommand;
            var menuAction = primaryShortcut ? (CommandContextItemViewModel)viewModel.AllCommands[0] : (CommandContextItemViewModel)action!;
            Assert.AreEqual(shortcut, menuAction.DisplayShortcut);
            Assert.IsNull(((IContextMenuContext)viewModel).FindKeybinding(shortcut));
            Assert.IsNull(menu.FindKeybinding(shortcut));
            Assert.IsNull(menu.FindKeybinding(requested.RequestedShortcut));
            var requestedCommand = viewModel.AllCommands.OfType<CommandContextItemViewModel>().Single(command => ReferenceEquals(command.Model.Unsafe, requested));
            Assert.IsFalse(requestedCommand.HasRequestedShortcut);
            Assert.IsNull(requestedCommand.DisplayShortcut, "An ignored extension request must not advertise a reserved shortcut.");
            menu.Close();
        }
        finally
        {
            menu.Close();
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PresentationAndSdkAdapter_UseCachedEntries(bool isDock)
    {
        var pageContext = new TestPageContext();
        var key = new KeyChord(0, (int)VirtualKey.F6, 0);
        var secondary = new CountingContextItem(new NoOpCommand { Name = "Secondary" }) { RequestedShortcut = key };
        var item = new CountingCommandItem
        {
            Command = new NoOpCommand { Name = "Primary" },
            MoreCommands = [new Separator("Group"), secondary],
        };
        var viewModel = isDock
            ? new DockItemViewModel(new(item), new(pageContext), true, true, DefaultContextMenuFactory.Instance)
            : new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var readCount = item.MenuReadCount;
        var snapshot = viewModel.AllCommands;
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));

        for (var i = 0; i < 3; i++)
        {
            Assert.AreSame(snapshot, viewModel.AllCommands);
            Assert.IsTrue(viewModel.HasSubmenu);
            Assert.IsFalse(viewModel.HasOverflowCommands);
            Assert.IsTrue(viewModel.CanOpenContextMenu);
            Assert.AreSame(viewModel.SecondaryCommand, ((IContextMenuContext)viewModel).FindKeybinding(key)!);
            menu.PrepareForOpen(viewModel);
            menu.SetSearchText("Secondary");
            Assert.AreSame(viewModel.SecondaryCommand, menu.FindKeybinding(key));
            menu.Close();

            List<IContextItem?> sdkEntries = [];
            viewModel.CopySdkContextItemsTo(sdkEntries);
            Assert.AreEqual(2, sdkEntries.Count);
            Assert.IsInstanceOfType<SeparatorViewModel>(sdkEntries[0]);
            Assert.AreSame(secondary, sdkEntries[1]);
        }

        Assert.AreEqual(readCount, item.MenuReadCount, "Presentation reads must not fetch SDK menu entries again.");
        Assert.AreEqual(1, secondary.ShortcutReadCount, "Read the extension shortcut once during initialization; menu preparation, filtering and lookups use its cached value.");
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
    [DataRow(false)]
    [DataRow(true)]
    public void ShortcutNavigation_UsesTheCurrentSubmenuAndPreservesInvocationContext(bool isDock)
    {
        var pageContext = new TestPageContext();
        var key = new KeyChord(0, (int)VirtualKey.F6, 0);
        var child = new CommandContextItem(new NoOpCommand { Name = "Child" }) { RequestedShortcut = key };
        var parent = new CommandContextItem(new NoOpCommand { Name = "Parent" })
        {
            RequestedShortcut = key,
            MoreCommands = [child],
        };
        var item = new CommandItem(new NoOpCommand { Name = isDock ? string.Empty : "Root" }) { MoreCommands = [parent] };
        var viewModel = isDock
            ? new DockItemViewModel(new(item), new(pageContext), true, true, DefaultContextMenuFactory.Instance)
            : new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);
        PerformCommandMessage? invocation = null;
        var events = new List<string>();
        var recipient = new object();
        menu.CommandInvoking += (_, message) =>
        {
            invocation = message;
            events.Add("invoking");
        };
        menu.CommandInvoked += (_, _) => events.Add("invoked");
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(recipient, (_, _) => events.Add("dispatch"));

        try
        {
            var rootMatch = ((IContextMenuContext)viewModel).FindKeybinding(key);
            Assert.IsNotNull(rootMatch);
            Assert.AreSame(parent, rootMatch.Model.Unsafe);
            Assert.IsFalse(menu.CanPopContextStack(), "Looking up a shortcut must not enter its submenu.");
            Assert.AreEqual(0, events.Count);

            menu.PrepareForOpen(viewModel, rootMatch);
            Assert.IsTrue(menu.CanPopContextStack());
            Assert.AreEqual(0, events.Count, "Opening a submenu must not dispatch its parent command.");
            Assert.IsNull(((CommandContextItemViewModel)menu.FilteredItems[0]).DisplayShortcut, "The submenu's first row has no fixed Enter shortcut.");

            Assert.AreSame(rootMatch, ((IContextMenuContext)viewModel).FindKeybinding(key));
            Assert.IsNull(menu.FindKeybinding(new KeyChord(0, (int)VirtualKey.F7, 0)));
            Assert.IsTrue(menu.CanPopContextStack(), "Root and unmatched lookups must preserve the current submenu.");

            menu.PrepareForOpen(viewModel, rootMatch);
            menu.PopContextStack();
            Assert.IsFalse(menu.CanPopContextStack(), "Opening a submenu must reset the previous navigation stack.");
            Assert.AreSame(rootMatch, menu.FindKeybinding(key));
            menu.PrepareForOpen(viewModel, rootMatch);

            menu.SetSearchText("No matching commands");
            Assert.AreEqual(0, menu.FilteredItems.Count);
            var childMatch = menu.FindKeybinding(key);
            Assert.IsNotNull(childMatch);
            Assert.AreSame(child, childMatch.Model.Unsafe, "Shortcuts use the current menu, independently of its filter.");

            Assert.AreEqual(ContextKeybindingResult.Hide, menu.InvokeCommand(childMatch));
            Assert.IsNotNull(invocation);
            Assert.AreSame(child, invocation.Context);
            string[] expected = ["invoking", "dispatch", "invoked"];
            CollectionAssert.AreEqual(expected, events);
        }
        finally
        {
            WeakReferenceMessenger.Default.UnregisterAll(recipient);
            menu.Close();
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public void ShortcutLookup_UsesTheOpenedContextAndClearsOnClose()
    {
        var pageContext = new TestPageContext();
        var key = new KeyChord(0, (int)VirtualKey.F6, 0);
        var first = new CommandContextItem(new NoOpCommand { Name = "First" }) { RequestedShortcut = key };
        var second = new CommandContextItem(new NoOpCommand { Name = "Second" }) { RequestedShortcut = key };
        var firstItem = new CommandItem(new NoOpCommand()) { MoreCommands = [first] };
        var secondItem = new CommandItem(new NoOpCommand()) { MoreCommands = [second] };
        var firstViewModel = new CommandItemViewModel(new(firstItem), new(pageContext), DefaultContextMenuFactory.Instance);
        var secondViewModel = new CommandItemViewModel(new(secondItem), new(pageContext), DefaultContextMenuFactory.Instance);
        firstViewModel.SlowInitializeProperties();
        secondViewModel.SlowInitializeProperties();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));

        try
        {
            menu.PrepareForOpen(firstViewModel);
            Assert.AreSame(first, menu.FindKeybinding(key)?.Model.Unsafe);

            menu.PrepareForOpen(secondViewModel);
            Assert.AreSame(second, menu.FindKeybinding(key)?.Model.Unsafe);

            menu.Close();
            Assert.IsNull(menu.FindKeybinding(key));
        }
        finally
        {
            menu.Close();
            firstViewModel.SafeCleanup();
            secondViewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SecondaryActivation_UsesCurrentMenuAndExecutesAnActionWithChildren(bool inSubmenu)
    {
        var pageContext = new TestPageContext();
        var child = new CommandContextItem(new NoOpCommand { Name = "Child" })
        {
            RequestedShortcut = CommandContextItemViewModel.PrimaryShortcut,
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Grandchild" })],
        };
        var parent = new CommandContextItem(new NoOpCommand { Name = "Parent" })
        {
            RequestedShortcut = CommandContextItemViewModel.SecondaryShortcut,
            MoreCommands = [child],
        };
        var item = new CommandItem(new NoOpCommand { Name = "Root" }) { MoreCommands = [parent] };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var parentCommand = (CommandContextItemViewModel)viewModel.SecondaryCommand!;
        parentCommand.SetDefaultShortcut(DefaultShortcutRole.None);
        ((CommandContextItemViewModel)parentCommand.SecondaryCommand!).SetDefaultShortcut(DefaultShortcutRole.None);
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);
        var invocations = new List<PerformCommandMessage>();
        menu.CommandInvoking += (_, message) => invocations.Add(message);

        try
        {
            if (inSubmenu)
            {
                Assert.AreEqual(ContextKeybindingResult.KeepOpen, menu.InvokeCommand(parentCommand));
                Assert.AreSame(child, menu.SecondaryCommand?.Model.Unsafe);
                menu.PopContextStack();
                Assert.AreSame(parentCommand, menu.SecondaryCommand);
                Assert.AreEqual(ContextKeybindingResult.KeepOpen, menu.InvokeCommand(parentCommand));
            }

            var secondary = menu.SecondaryCommand;
            Assert.IsNotNull(secondary, "Action roles must not depend on the displayed shortcut hints.");
            Assert.IsNull(menu.FindKeybinding(CommandContextItemViewModel.PrimaryShortcut));
            Assert.IsNull(menu.FindKeybinding(CommandContextItemViewModel.SecondaryShortcut));
            Assert.AreSame(inSubmenu ? child : parent, secondary.Model.Unsafe);
            Assert.IsTrue(secondary.HasSubmenu);
            menu.SetSearchText("No matching commands");
            Assert.AreEqual(0, menu.FilteredItems.Count);
            Assert.AreSame(secondary, menu.SecondaryCommand, "Filtering must not change the secondary action.");

            Assert.AreEqual(ContextKeybindingResult.Hide, menu.InvokeCommand(secondary, navigateSubmenus: false));
            Assert.AreEqual(1, invocations.Count);
            Assert.AreSame(inSubmenu ? child : parent, invocations[0].Context);
            menu.Close();
            Assert.IsFalse(menu.CanPopContextStack());
            Assert.IsNull(menu.SecondaryCommand);
        }
        finally
        {
            menu.Close();
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public void SecondaryActivation_RequiresTheCommandInTheDisplayedSnapshot()
    {
        var pageContext = new TestPageContext();
        var first = new CommandContextItemViewModel(new CommandContextItem(new NoOpCommand { Name = "First" }), new(pageContext));
        var second = new CommandContextItemViewModel(new CommandContextItem(new NoOpCommand { Name = "Second" }), new(pageContext));
        IContextItemViewModel[] commands = [first];
        CommandItemViewModel secondary = first;
        var context = new Mock<ICommandBarContext>();
        context.SetupGet(value => value.AllCommands).Returns(() => commands);
        context.SetupGet(value => value.SecondaryCommand).Returns(() => secondary);
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(context.Object);

        try
        {
            secondary = second;
            Assert.IsNull(menu.SecondaryCommand, "A replacement command must not run before its row is displayed.");
            Assert.AreSame(first, menu.FilteredItems.Single());

            context.Raise(value => value.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IContextMenuContext.AllCommands)));
            Assert.IsNull(menu.SecondaryCommand, "A new role must not point outside the displayed snapshot.");

            commands = [second];
            Assert.IsNull(menu.SecondaryCommand);
            context.Raise(value => value.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IContextMenuContext.AllCommands)));
            Assert.AreSame(second, menu.SecondaryCommand);
            Assert.AreSame(second, menu.FilteredItems.Single());
        }
        finally
        {
            menu.Close();
            first.SafeCleanup();
            second.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
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
    public void CommandBarMessages_DoNotPrepareOrRetargetMenu()
    {
        var pageContext = new TestPageContext();
        var parent = new CommandContextItem(new NoOpCommand { Name = "Submenu" })
        {
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Leaf" })],
        };
        var item = new CommandItem(new NoOpCommand { Name = "Root" }) { MoreCommands = [parent] };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var parentViewModel = (CommandContextItemViewModel)viewModel.SecondaryCommand!;
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));

        try
        {
            WeakReferenceMessenger.Default.Send(new UpdateCommandBarMessage(viewModel));
            Assert.IsNull(menu.SelectedItem);
            Assert.AreEqual(0, menu.FilteredItems.Count);
            menu.PrepareForOpen(viewModel, parentViewModel);
            menu.SetSearchText("Leaf");
            var filteredItems = menu.FilteredItems.ToArray();
            Assert.AreEqual(1, filteredItems.Length);

            WeakReferenceMessenger.Default.Send(new UpdateCommandBarMessage(viewModel));
            WeakReferenceMessenger.Default.Send(new UpdateCommandBarMessage(null));
            Assert.AreSame(viewModel, menu.SelectedItem);
            Assert.IsTrue(menu.CanPopContextStack());
            CollectionAssert.AreEqual(filteredItems, menu.FilteredItems.ToArray());

            menu.PrepareForOpen(viewModel);
            Assert.IsFalse(menu.CanPopContextStack());
            CollectionAssert.AreEqual(viewModel.AllCommands.ToArray(), menu.FilteredItems.ToArray());
            menu.Close();
            menu.PopContextStack();
            Assert.IsNull(menu.SelectedItem);
            Assert.IsNull(menu.SecondaryCommand);
            Assert.AreEqual(0, menu.FilteredItems.Count);
            Assert.IsNull(parentViewModel.DisplayShortcut);
        }
        finally
        {
            menu.Close();
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OpenOrContextUpdate_RefreshesDisplayedRows(bool changeContext)
    {
        IContextItemViewModel[] commands = [Mock.Of<IContextItemViewModel>()];
        var context = new Mock<ICommandBarContext>();
        context.SetupGet(value => value.AllCommands).Returns(() => commands);
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(context.Object);
        commands = [Mock.Of<IContextItemViewModel>()];
        var nextContext = context.Object;
        if (changeContext)
        {
            var replacement = new Mock<ICommandBarContext>();
            replacement.SetupGet(value => value.AllCommands).Returns(() => commands);
            nextContext = replacement.Object;
            menu.PrepareForOpen(nextContext);
        }
        else
        {
            context.Raise(value => value.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IContextMenuContext.AllCommands)));
        }

        Assert.AreSame(nextContext, menu.SelectedItem);
        CollectionAssert.AreEqual(commands, menu.FilteredItems.ToArray());
        menu.Close();
        commands = [Mock.Of<IContextItemViewModel>()];
        context.Raise(value => value.PropertyChanged += null, new PropertyChangedEventArgs(nameof(IContextMenuContext.AllCommands)));
        Assert.IsNull(menu.SelectedItem);
        Assert.AreEqual(0, menu.FilteredItems.Count, "A closed menu must no longer observe its former context.");
        menu.PrepareForOpen(nextContext);
        CollectionAssert.AreEqual(commands, menu.FilteredItems.ToArray());
        menu.Close();
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
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        menu.PrepareForOpen(viewModel);
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
            menu.Close();
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    [TestMethod]
    public async Task ContextCommandVisibilityChanges_RefreshActionRoles()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var pageContext = new TestPageContext(uiTasks.Scheduler);
        var first = new NoOpCommand { Name = "First" };
        var second = new NoOpCommand { Name = "Second" };
        var item = new CommandItem(new NoOpCommand())
        {
            MoreCommands = [new CommandContextItem(first), new CommandContextItem(second)],
        };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        viewModel.SlowInitializeProperties();
        var commands = viewModel.AllCommands.OfType<CommandContextItemViewModel>().ToArray();
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        await uiTasks.StartNew(() => menu.PrepareForOpen(viewModel));
        var snapshot = viewModel.AllCommands;
        var secondaryChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(viewModel.AllCommands))
            {
                return;
            }

            if (ReferenceEquals(viewModel.SecondaryCommand, commands[1]))
            {
                secondaryChanged.TrySetResult();
            }

            if (!viewModel.CanOpenContextMenu && !viewModel.HasOverflowCommands &&
                commands.All(command => command.DisplayShortcut is null))
            {
                hidden.TrySetResult();
            }

            if (viewModel.SecondaryCommand?.Name == "Restored" && !viewModel.HasOverflowCommands &&
                commands[0].DisplayShortcut == CommandContextItemViewModel.SecondaryShortcut &&
                commands[1].DisplayShortcut is null)
            {
                restored.TrySetResult();
            }
        };

        try
        {
            Assert.IsTrue(viewModel.HasOverflowCommands);
            Assert.AreSame(commands[0], menu.SecondaryCommand);
            first.Name = string.Empty;
            await secondaryChanged.Task.WaitAsync(TimeSpan.FromSeconds(5));
            WeakReferenceMessenger.Default.Send(new UpdateCommandBarMessage(viewModel));
            Assert.AreSame(snapshot, viewModel.AllCommands);
            Assert.AreSame(commands[1], menu.SecondaryCommand, "Renaming a row must update the role even when the menu list is unchanged.");

            second.Name = string.Empty;
            await hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsNull(viewModel.SecondaryCommand);
            Assert.IsNull(menu.SecondaryCommand);
            Assert.IsTrue(viewModel.HasSubmenu);
            Assert.IsTrue(commands.All(command => command.DisplayShortcut is null));

            first.Name = "Restored";
            await restored.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(commands[0], menu.SecondaryCommand);
            Assert.IsTrue(viewModel.CanOpenContextMenu);
            Assert.IsTrue(viewModel.HasSubmenu);
            Assert.AreEqual(2, viewModel.AllCommands.Count);
            Assert.AreEqual(new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0), commands[0].DisplayShortcut);
            Assert.IsNull(commands[1].DisplayShortcut);
        }
        finally
        {
            await uiTasks.StartNew(menu.Close);
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

    [TestMethod]
    public async Task OpenSubmenu_RefreshesHydratedRowsAndReturnsWhenRemoved()
    {
        var uiTasks = new TaskFactory(new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler);
        var pageContext = new TestPageContext(uiTasks.Scheduler);
        var parent = new CommandContextItem(new NoOpCommand { Name = "Parent" })
        {
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Leaf before" })],
        };
        var item = new CommandItem(new NoOpCommand { Name = "Root" }) { MoreCommands = [parent] };
        var viewModel = new CommandItemViewModel(new(item), new(pageContext), DefaultContextMenuFactory.Instance);
        await Task.Run(viewModel.SlowInitializeProperties);
        var parentViewModel = (CommandContextItemViewModel)viewModel.SecondaryCommand!;
        var menu = new ContextMenuViewModel(new FuzzyMatcherProvider(new()));
        var replacement = new CommandContextItem(new NoOpCommand { Name = "Leaf after" });
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            await uiTasks.StartNew(() =>
            {
                menu.PrepareForOpen(viewModel, parentViewModel);
                menu.SetSearchText("Leaf");
                menu.FilteredItems.CollectionChanged += (_, _) =>
                {
                    if (menu.FilteredItems.OfType<CommandContextItemViewModel>().Any(command => ReferenceEquals(command.Model.Unsafe, replacement)))
                    {
                        refreshed.TrySetResult();
                    }
                };
                menu.PropertyChanged += (_, _) =>
                {
                    if (!menu.CanPopContextStack())
                    {
                        returned.TrySetResult();
                    }
                };
            });
            await Task.Run(() => parent.MoreCommands = [replacement, new CommandContextItem(new NoOpCommand { Name = "Other" })]);
            await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await uiTasks.StartNew(() =>
            {
                Assert.IsTrue(menu.CanPopContextStack());
                Assert.AreSame(replacement, ((CommandContextItemViewModel)menu.FilteredItems.Single()).Model.Unsafe, "Hydration must preserve the current level and search.");
                Assert.AreEqual(CommandContextItemViewModel.SecondaryShortcut, ((CommandContextItemViewModel)menu.SecondaryCommand!).DisplayShortcut);
            });

            await Task.Run(() => item.MoreCommands = []);
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await uiTasks.StartNew(() =>
            {
                Assert.IsFalse(menu.CanPopContextStack());
                Assert.AreEqual("Root", ((CommandContextItemViewModel)menu.FilteredItems.Single()).Name);
                Assert.AreEqual(CommandContextItemViewModel.PrimaryShortcut, ((CommandContextItemViewModel)menu.FilteredItems.Single()).DisplayShortcut);
            });
        }
        finally
        {
            await uiTasks.StartNew(menu.Close);
            viewModel.SafeCleanup();
            GC.KeepAlive(pageContext);
        }
    }

    private sealed partial class CountingContextItem(ICommand command) : CommandContextItem(command)
    {
        public int ShortcutReadCount { get; private set; }

        public override KeyChord RequestedShortcut
        {
            get
            {
                ShortcutReadCount++;
                return base.RequestedShortcut;
            }

            set => base.RequestedShortcut = value;
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
