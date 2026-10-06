// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class QuickAccessShelfItemActionsTests
{
    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }

    private readonly TestPageContext _pageContext = new();
    private ListItemViewModel? _item;

    [TestCleanup]
    public void Cleanup() => _item?.SafeCleanup();

    [TestMethod]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Left, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Right, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Up, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Down, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Home, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.End, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Tab, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Tab, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Escape, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.Application, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.F10, VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Number0, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Number5, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Number9, VirtualKeyModifiers.Menu)]
    public void NativeButtonKeys_DoNotDispatchShelfCommands(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var chord = new KeyChord(modifiers, (int)key, 0);
        var item = CreateItem(new CommandContextItem(new NoOpCommand { Name = "Requested action" }) { RequestedShortcut = chord });

        Assert.IsFalse(QuickAccessShelfItemActions.ShouldHandleKey(chord));
        Assert.IsFalse(QuickAccessShelfItemActions.TryHandleKey(
            item,
            chord,
            _ => Assert.Fail("Native button keys must retain their existing activation and navigation."),
            _ => Assert.Fail("Native context requests are handled by the button.")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CtrlEnter_UsesShelfItemContextAndNeverFallsBackToPrimary(bool hasSecondary)
    {
        var secondary = new CommandContextItem(new NoOpCommand { Name = "Secondary" });
        var item = CreateItem(hasSecondary ? [secondary] : []);
        List<PerformCommandMessage> invoked = [];

        Assert.IsTrue(QuickAccessShelfItemActions.TryHandleKey(
            item,
            new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.Enter, 0),
            invoked.Add,
            _ => Assert.Fail("Ctrl+Enter must execute the secondary action.")));

        Assert.HasCount(hasSecondary ? 1 : 0, invoked);
        if (hasSecondary)
        {
            Assert.AreSame(item.SecondaryCommand!.Command.Model, invoked[0].Command);
            Assert.AreSame(item.Model.Unsafe, invoked[0].Context);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CtrlK_ConsumesChordAndOpensAvailableRootMenu(bool hasMenu)
    {
        var item = CreateItem(hasMenu ? [new CommandContextItem(new NoOpCommand { Name = "Secondary" })] : []);
        List<CommandContextItemViewModel?> opened = [];

        Assert.IsTrue(QuickAccessShelfItemActions.TryHandleKey(
            item,
            new KeyChord(VirtualKeyModifiers.Control, (int)VirtualKey.K, 0),
            _ => Assert.Fail("Ctrl+K must not activate the selected list item."),
            opened.Add));

        Assert.HasCount(hasMenu ? 1 : 0, opened);
        if (hasMenu)
        {
            Assert.IsNull(opened[0]);
        }
    }

    [TestMethod]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None, false)]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None, true)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, false)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, true)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, false)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, true)]
    public void RequestedShortcut_UsesCurrentCommandOrSubmenu(VirtualKey key, VirtualKeyModifiers modifiers, bool hasSubmenu)
    {
        var chord = new KeyChord(modifiers, (int)key, 0);
        var requested = new CommandContextItem(new NoOpCommand { Name = "Requested action" })
        {
            RequestedShortcut = chord,
            MoreCommands = hasSubmenu ? [new CommandContextItem(new NoOpCommand { Name = "Child" })] : [],
        };
        var item = CreateItem(requested);
        List<PerformCommandMessage> invoked = [];
        List<CommandContextItemViewModel?> opened = [];

        Assert.IsTrue(QuickAccessShelfItemActions.TryHandleKey(item, chord, invoked.Add, opened.Add));

        Assert.HasCount(hasSubmenu ? 0 : 1, invoked);
        Assert.HasCount(hasSubmenu ? 1 : 0, opened);
        Assert.AreSame(requested, hasSubmenu ? opened[0]?.Model.Unsafe : invoked[0].Context);
    }

    private ListItemViewModel CreateItem(params IContextItem[] commands)
    {
        _item = new ListItemViewModel(new ListItem(new NoOpCommand()) { MoreCommands = commands }, new(_pageContext), DefaultContextMenuFactory.Instance);
        _item.SlowInitializeProperties();
        return _item;
    }
}
