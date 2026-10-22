// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Foundation;
using Windows.System;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ItemActionControllerTests
{
    private readonly Queue<Action> _callbacks = new();
    private readonly List<ContextMenuRequest> _opened = [];
    private readonly TestPageContext _pageContext = new();
    private ICommandBarContext? _context = Mock.Of<ICommandBarContext>();
    private IContextMenuContext? _pageMenu;
    private bool _canAct = true;
    private bool _canEnqueue = true;
    private int _closeCount;

    [TestMethod]
    [DataRow(VirtualKeyModifiers.None, false)]
    [DataRow(VirtualKeyModifiers.None, true)]
    [DataRow(VirtualKeyModifiers.Control, false)]
    [DataRow(VirtualKeyModifiers.Control, true)]
    public void Activation_IsLeftToThePageInteractionTarget(VirtualKeyModifiers modifiers, bool hasContext)
    {
        _context = hasContext ? Mock.Of<ICommandBarContext>() : null;
        using var controller = CreateController();

        Assert.IsFalse(controller.TryHandleKey(new KeyChord(modifiers, (int)VirtualKey.Enter, 0)));

        Assert.IsEmpty(_callbacks);
        Assert.IsEmpty(_opened);
    }

    [TestMethod]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    [DataRow(VirtualKey.Enter, VirtualKeyModifiers.Menu)]
    [DataRow(VirtualKey.Space, VirtualKeyModifiers.None)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)]
    public void OtherKeys_AreLeftForTheirExistingHandlers(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        using var controller = CreateController();
        Assert.IsFalse(controller.TryHandleKey(new KeyChord(modifiers, (int)key, 0)));
        Assert.IsEmpty(_callbacks);
        Assert.IsEmpty(_opened);
    }

    [TestMethod]
    public void InactiveShell_DoesNotHandleKeysOrOpenMessages()
    {
        _canAct = false;
        using var controller = CreateController();

        Assert.IsFalse(controller.TryHandleKey(new KeyChord(0, (int)VirtualKey.Enter, 0)));
        Assert.IsFalse(controller.TryHandleKey(CtrlK));
        controller.ShowContextMenu(new ContextMenuRequest(_context!));
        controller.ShowContextMenuAfterKeyEvent(_context!);

        Assert.IsEmpty(_callbacks);
        Assert.IsEmpty(_opened);
    }

    [TestMethod]
    public void KeyboardOpen_IsDeferredAndUsesTheLatestLiveContext()
    {
        using var controller = CreateController();
        Assert.IsTrue(controller.TryHandleKey(CtrlK));
        _context = Mock.Of<ICommandBarContext>();
        Assert.IsTrue(controller.TryHandleKey(CtrlK));
        Assert.IsEmpty(_opened);

        DrainCallbacks();

        Assert.HasCount(1, _opened);
        Assert.AreSame(_context, _opened[0].Context);
        Assert.IsNull(_opened[0].Anchor, "Keyboard requests must use the shell's default presentation.");
    }

    [TestMethod]
    [DataRow("selection")]
    [DataRow("inactive")]
    [DataRow("close")]
    [DataRow("dispose")]
    public void DeferredOpen_IsCancelledWhenItsRequestIsNoLongerValid(string change)
    {
        using var controller = CreateController();
        Assert.IsTrue(controller.TryHandleKey(CtrlK));
        switch (change)
        {
            case "selection":
                _context = Mock.Of<ICommandBarContext>();
                break;
            case "inactive":
                _canAct = false;
                break;
            case "close":
                controller.CloseContextMenu();
                break;
            case "dispose":
                controller.Dispose();
                Assert.AreEqual(1, _closeCount);
                Assert.IsFalse(controller.TryHandleKey(CtrlK));
                controller.ShowContextMenu(new ContextMenuRequest(_context!));
                controller.ShowContextMenuAfterKeyEvent(_context!);
                break;
        }

        DrainCallbacks();
        Assert.IsEmpty(_opened);
    }

    [TestMethod]
    public void ExplicitOpen_PreservesItsContextAndSupersedesDeferredKeyboardOpen()
    {
        using var controller = CreateController();
        Assert.IsTrue(controller.TryHandleKey(CtrlK));
        var request = new ContextMenuRequest(Mock.Of<IContextMenuContext>())
        {
            Anchor = new ContextMenuAnchor(null!, new Point(12, 34), FlyoutPlacementMode.BottomEdgeAlignedLeft, ContextMenuFilterLocation.Top),
        };

        controller.ShowContextMenu(request);
        DrainCallbacks();

        Assert.HasCount(1, _opened);
        Assert.AreSame(request, _opened[0]);
    }

    [TestMethod]
    public void RejectedExplicitOpen_PreservesPendingKeyboardOpen()
    {
        using var controller = CreateController();
        Assert.IsTrue(controller.TryHandleKey(CtrlK));

        _canAct = false;
        controller.ShowContextMenu(new ContextMenuRequest(Mock.Of<IContextMenuContext>()));
        _canAct = true;
        DrainCallbacks();

        Assert.HasCount(1, _opened);
        Assert.AreSame(_context, _opened[0].Context);
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void Close_CancelsOnlyItsOwnSurface(bool closePalette)
    {
        using var controller = CreateController();
        List<ContextMenuRequest> dockOpened = [];
        var dockHost = new ContextMenuHost(
            callback =>
            {
                _callbacks.Enqueue(callback);
                return true;
            },
            dockOpened.Add,
            () => { });

        Assert.IsTrue(controller.TryHandleKey(CtrlK));
        dockHost.ShowAfterKeyEvent(() => new ContextMenuRequest(Mock.Of<IContextMenuContext>()));
        if (closePalette)
        {
            controller.CloseContextMenu();
        }
        else
        {
            dockHost.Close();
        }

        DrainCallbacks();
        Assert.HasCount(closePalette ? 0 : 1, _opened);
        Assert.HasCount(closePalette ? 1 : 0, dockOpened);
    }

    [TestMethod]
    [DataRow("close")]
    [DataRow("newer")]
    [DataRow("selection")]
    public void SubmenuKeyOpen_DoesNotReopenAfterCloseOrReplaceANewerRequest(string change)
    {
        using var controller = CreateController();
        var submenuContext = _context!;
        var submenu = CreateSubmenu();
        Mock.Get(submenuContext).SetupGet(value => value.AllCommands).Returns([submenu]);
        try
        {
            controller.ShowContextMenuAfterKeyEvent(submenuContext, submenu);
            var newer = new ContextMenuRequest(Mock.Of<IContextMenuContext>());
            switch (change)
            {
                case "close":
                    controller.CloseContextMenu();
                    break;
                case "newer":
                    controller.ShowContextMenu(newer);
                    break;
                case "selection":
                    _context = Mock.Of<ICommandBarContext>();
                    break;
            }

            DrainCallbacks();

            if (change == "newer")
            {
                Assert.HasCount(1, _opened);
                Assert.AreSame(newer, _opened[0]);
            }
            else
            {
                Assert.IsEmpty(_opened);
            }
        }
        finally
        {
            submenu.SafeCleanup();
        }
    }

    [TestMethod]
    public void FailedEnqueue_DoesNotPreventALaterOpen()
    {
        using var controller = CreateController();
        _canEnqueue = false;
        Assert.IsTrue(controller.TryHandleKey(CtrlK));
        Assert.IsEmpty(_callbacks);
        _canEnqueue = true;
        Assert.IsTrue(controller.TryHandleKey(CtrlK));

        DrainCallbacks();
        Assert.HasCount(1, _opened);
    }

    private static KeyChord CtrlK => new(VirtualKeyModifiers.Control, (int)VirtualKey.K, 0);

    [TestMethod]
    public void PageSubmenuOpen_PreservesAnchorAndLaterSelectedItemMenu()
    {
        var selectedItem = _context;
        var command = CreateSubmenu();
        var menu = new Mock<IContextMenuContext>();
        menu.SetupGet(value => value.AllCommands).Returns([command]);
        _pageMenu = menu.Object;
        var anchor = new ContextMenuAnchor(null!, null, FlyoutPlacementMode.TopEdgeAlignedLeft, ContextMenuFilterLocation.Bottom);
        using var controller = CreateController();
        try
        {
            CommandBarViewModel.InvokeContextCommand(command, submenu =>
                controller.ShowContextMenuAfterKeyEvent(_pageMenu, submenu, anchor, () => ReferenceEquals(menu.Object, _pageMenu)));
            Assert.IsEmpty(_opened);
            Assert.HasCount(1, _callbacks);
            DrainCallbacks();

            Assert.HasCount(1, _opened);
            Assert.AreSame(_pageMenu, _opened[0].Context);
            Assert.AreSame(command, _opened[0].InitialSubmenu);
            Assert.AreEqual(anchor, _opened[0].Anchor);

            Assert.IsTrue(controller.TryHandleKey(CtrlK));
            DrainCallbacks();

            Assert.HasCount(2, _opened);
            Assert.AreSame(selectedItem, _opened[1].Context);
            Assert.IsNull(_opened[1].InitialSubmenu);
            Assert.IsNull(_opened[1].Anchor);
        }
        finally
        {
            command.SafeCleanup();
        }
    }

    [TestMethod]
    [DataRow("page")]
    [DataRow("commands")]
    public void PageSubmenuOpen_IsCancelledWhenThePageOrCommandChanges(string change)
    {
        var command = CreateSubmenu();
        IContextItemViewModel[] commands = [command];
        var menu = new Mock<IContextMenuContext>();
        menu.SetupGet(value => value.AllCommands).Returns(() => commands);
        _pageMenu = menu.Object;
        using var controller = CreateController();
        try
        {
            CommandBarViewModel.InvokeContextCommand(command, submenu =>
                controller.ShowContextMenuAfterKeyEvent(_pageMenu, submenu, isContextCurrent: () => ReferenceEquals(menu.Object, _pageMenu)));
            if (change == "page")
            {
                _pageMenu = Mock.Of<IContextMenuContext>();
            }
            else
            {
                commands = [];
            }

            DrainCallbacks();
            Assert.IsEmpty(_opened);
        }
        finally
        {
            command.SafeCleanup();
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SubmenuOpen_RevalidatesLiveContextIndependentlyOfDisplayedContext(bool changeLiveContext)
    {
        var command = CreateSubmenu();
        var menu = new Mock<IContextMenuContext>();
        menu.SetupGet(value => value.AllCommands).Returns([command]);
        var requestContext = menu.Object;
        IContextMenuContext liveContext = requestContext;
        using var controller = CreateController();
        try
        {
            CommandBarViewModel.InvokeContextCommand(command, submenu =>
                controller.ShowContextMenuAfterKeyEvent(requestContext, submenu, isContextCurrent: () => ReferenceEquals(requestContext, liveContext)));
            if (changeLiveContext)
            {
                liveContext = Mock.Of<IContextMenuContext>();
            }

            DrainCallbacks();

            Assert.HasCount(changeLiveContext ? 0 : 1, _opened);
            if (!changeLiveContext)
            {
                Assert.AreSame(requestContext, _opened[0].Context);
                Assert.AreSame(command, _opened[0].InitialSubmenu);
            }
        }
        finally
        {
            command.SafeCleanup();
        }
    }

    private CommandContextItemViewModel CreateSubmenu()
    {
        var model = new CommandContextItem(new NoOpCommand { Name = "Tools" })
        {
            MoreCommands = [new CommandContextItem(new NoOpCommand { Name = "Child" })],
        };
        var command = new CommandContextItemViewModel(model, new(_pageContext));
        command.SlowInitializeProperties();
        return command;
    }

    private ItemActionController CreateController() => new(
        () => _context,
        () => _canAct,
        new ContextMenuHost(
            callback =>
            {
                if (!_canEnqueue)
                {
                    return false;
                }

                _callbacks.Enqueue(callback);
                return true;
            },
            _opened.Add,
            () => _closeCount++));

    private void DrainCallbacks()
    {
        while (_callbacks.TryDequeue(out var callback))
        {
            callback();
        }
    }

    private sealed class TestPageContext : IPageContext
    {
        public TaskScheduler Scheduler => TaskScheduler.Default;

        public ICommandProviderContext ProviderContext => CommandProviderContext.Empty;

        public void ShowException(Exception ex, string? extensionHint = null) =>
            throw new AssertFailedException($"Unexpected exception from view model: {ex}");
    }
}
