// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.System;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class DockItemActionsTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    private readonly TestPageContext _pageContext = new();
    private DockItemViewModel? _item;

    [TestCleanup]
    public void Cleanup() => _item?.SafeCleanup();

    [TestMethod]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Windows)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.Control)]
    public void ButtonActivation_InvokesUnnamedPrimaryWithoutContext(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var item = CreateItem(new CommandContextItem(new NoOpCommand { Name = "Visible secondary" })
        {
            RequestedShortcut = new KeyChord(VirtualKeyModifiers.None, (int)VirtualKey.Enter, 0),
        });
        List<CommandItemViewModel> invocations = [];

        Assert.IsTrue(DockItemActions.TryHandleKey(
            item,
            new KeyChord(modifiers, (int)key, 0),
            invocations.Add,
            _ => Assert.Fail("Button activation must not open the menu.")));

        CollectionAssert.AreEqual(new[] { item }, invocations);
        var message = DockItemActions.CreateInvocationMessage(invocations[0]);
        Assert.AreSame(item.Command.Model, message.Command);
        Assert.IsNull(message.Context, "Direct dock activation must not change the sender or enter home-page history.");
        Assert.IsFalse(message.WithAnimation);
        Assert.IsTrue(message.TransientPage);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CtrlEnter_InvokesSecondaryWithItsContextEvenWhenItHasChildren(bool hasSubmenu)
    {
        var secondary = new CommandContextItem(new NoOpCommand { Name = "Secondary" })
        {
            MoreCommands = hasSubmenu ? [new CommandContextItem(new NoOpCommand { Name = "Child" })] : [],
        };
        var chord = new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0);
        var item = CreateItem(secondary, new CommandContextItem(new NoOpCommand { Name = "Reserved request" })
        {
            RequestedShortcut = chord,
        });
        List<CommandItemViewModel> invocations = [];

        Assert.IsTrue(DockItemActions.TryHandleKey(item, chord, invocations.Add, _ => Assert.Fail("Ctrl+Enter executes the secondary action.")));

        CollectionAssert.AreEqual(new[] { item.SecondaryCommand }, invocations);
        var message = DockItemActions.CreateInvocationMessage(invocations[0]);
        Assert.AreSame(secondary, message.Context);
        Assert.IsFalse(message.WithAnimation);
        Assert.IsTrue(message.TransientPage);
    }

    [TestMethod]
    public void CtrlEnter_WithoutSecondaryIsConsumedWithoutActivatingPrimary()
    {
        Assert.IsTrue(DockItemActions.TryHandleKey(
            CreateItem(),
            new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0),
            _ => Assert.Fail("There is no secondary action."),
            _ => Assert.Fail("Ctrl+Enter must not open the menu.")));
    }

    [TestMethod]
    public void CtrlK_OpensRootMenuWithoutOverflow()
    {
        var item = CreateItem(new CommandContextItem(new NoOpCommand { Name = "Secondary" }));
        Assert.IsFalse(item.HasOverflowCommands);
        List<CommandContextItemViewModel?> opened = [];

        Assert.IsTrue(DockItemActions.TryHandleKey(
            item,
            new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.K, 0),
            _ => Assert.Fail("Ctrl+K must not execute an action."),
            opened.Add));

        Assert.HasCount(1, opened);
        Assert.IsNull(opened[0], "A null submenu opens the root context menu.");
    }

    [TestMethod]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None, false)]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None, true)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, false)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, true)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Menu, false)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Menu, true)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, false)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, true)]
    public void RequestedShortcut_ExecutesActionOrOpensSubmenu(VirtualKey key, VirtualKeyModifiers modifiers, bool hasSubmenu)
    {
        var chord = new KeyChord(modifiers, (int)key, 0);
        var requested = new CommandContextItem(new NoOpCommand { Name = "Requested action" })
        {
            RequestedShortcut = chord,
            MoreCommands = hasSubmenu ? [new CommandContextItem(new NoOpCommand { Name = "Child" })] : [],
        };
        var item = CreateItem(requested);
        List<CommandItemViewModel> invocations = [];
        List<CommandContextItemViewModel?> opened = [];

        Assert.IsTrue(DockItemActions.TryHandleKey(item, chord, invocations.Add, opened.Add));

        if (hasSubmenu)
        {
            Assert.IsEmpty(invocations);
            Assert.HasCount(1, opened);
            Assert.AreSame(requested, opened[0]?.Model.Unsafe);
        }
        else
        {
            Assert.IsEmpty(opened);
            Assert.HasCount(1, invocations);
            Assert.AreSame(requested, DockItemActions.CreateInvocationMessage(invocations[0]).Context);
        }
    }

    [TestMethod]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control)]
    [DataRow(VirtualKey.F9, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    public void UnavailableOrUnmatchedShortcut_IsLeftUnhandled(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        Assert.IsFalse(DockItemActions.TryHandleKey(
            CreateItem(),
            new KeyChord(modifiers, (int)key, 0),
            _ => Assert.Fail("Unmatched input must not execute an action."),
            _ => Assert.Fail("Unmatched input must not open the menu.")));
    }

    [TestMethod]
    public void PaletteSummoning_PreservesNonInvokableCommandBehavior()
    {
        (ICommand? Command, bool ShowPalette)[] cases =
        [
            (null, false),
            (new NoOpCommand(), false),
            (Mock.Of<IPage>(), true),
            (Mock.Of<ICommand>(), true),
        ];
        foreach (var (command, showPalette) in cases)
        {
            var viewModel = new CommandViewModel(command, new(_pageContext));
            viewModel.FastInitializeProperties();
            try
            {
                Assert.AreEqual(showPalette, DockItemActions.ShouldShowPalette(viewModel));
            }
            finally
            {
                viewModel.SafeCleanup();
            }
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void CommandPublication_WaitsForCachedCommandShape(bool replaceExisting, bool isPage)
    {
        var command = new Mock<ICommand>();
        if (isPage)
        {
            command.As<IPage>();
        }
        else
        {
            command.As<IInvokableCommand>();
        }

        var model = new ListItem(replaceExisting ? new NoOpCommand { Name = "Previous" } : command.Object);
        _item = new DockItemViewModel(new(model), new(_pageContext), true, true, DefaultContextMenuFactory.Instance);
        if (replaceExisting)
        {
            _item.SlowInitializeProperties();
        }

        var previous = _item.Command;
        var idReads = 0;
        command.SetupGet(value => value.Id).Returns(() =>
        {
            idReads++;
            Assert.AreSame(previous, _item.Command, "A command still initializing must not be exposed to dock input.");
            Assert.IsFalse(DockItemActions.ShouldShowPalette(_item.Command));
            return "dock.test";
        });
        command.SetupGet(value => value.Name).Returns("Ready");

        if (replaceExisting)
        {
            model.Command = command.Object;
        }
        else
        {
            _item.FastInitializeProperties();
        }

        Assert.AreEqual(1, idReads);
        Assert.AreSame(command.Object, _item.Command.Model.Unsafe);
        Assert.AreEqual(isPage, DockItemActions.ShouldShowPalette(_item.Command));
    }

    private DockItemViewModel CreateItem(params IContextItem[] commands)
    {
        var item = new ListItem(new NoOpCommand()) { MoreCommands = commands };
        _item = new DockItemViewModel(new(item), new(_pageContext), true, true, DefaultContextMenuFactory.Instance);
        _item.SlowInitializeProperties();
        return _item;
    }
}
