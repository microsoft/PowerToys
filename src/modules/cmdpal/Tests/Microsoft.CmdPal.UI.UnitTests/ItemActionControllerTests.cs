// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
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
    private readonly WeakReferenceMessenger _messenger = new();
    private readonly Queue<Action> _callbacks = new();
    private readonly List<ContextMenuRequest> _opened = [];
    private readonly TestPageContext _pageContext = new();
    private ICommandBarContext? _context = Mock.Of<ICommandBarContext>();
    private bool _canAct = true;
    private bool _canEnqueue = true;
    private int _closeCount;

    [TestMethod]
    [DataRow(VirtualKeyModifiers.None, false, "primary")]
    [DataRow(VirtualKeyModifiers.None, true, "primary")]
    [DataRow(VirtualKeyModifiers.Control, false, "secondary")]
    [DataRow(VirtualKeyModifiers.Control, true, "secondary")]
    public void Activation_UsesPageMessagesWithoutRequiringAMenu(VirtualKeyModifiers modifiers, bool hasContext, string expected)
    {
        _context = hasContext ? Mock.Of<ICommandBarContext>() : null;
        List<string> activations = [];
        _messenger.Register<ActivateSelectedListItemMessage>(this, (_, _) => activations.Add("primary"));
        _messenger.Register<ActivateSecondaryCommandMessage>(this, (_, _) => activations.Add("secondary"));
        _messenger.Register<PerformCommandMessage>(this, (_, _) => Assert.Fail("Page activation must retain its existing invocation path."));
        using var controller = CreateController();

        Assert.IsTrue(controller.TryHandleKey(new KeyChord(modifiers, (int)VirtualKey.Enter, 0)));

        CollectionAssert.AreEqual(new[] { expected }, activations);
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

        Assert.IsFalse(controller.TryHandleShortcut(new KeyChord(0, (int)VirtualKey.F6, 0)));
        Assert.IsFalse(controller.TryHandleKey(new KeyChord(0, (int)VirtualKey.Enter, 0)));
        Assert.IsFalse(controller.TryHandleKey(CtrlK));
        _messenger.Send(new OpenContextMenuMessage(new ContextMenuRequest(_context!)));

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
                _messenger.Send<ClosePaletteContextMenuMessage>();
                break;
            case "dispose":
                controller.Dispose();
                Assert.AreEqual(1, _closeCount);
                Assert.IsFalse(controller.TryHandleKey(CtrlK));
                _messenger.Send(new OpenContextMenuMessage(new ContextMenuRequest(_context!)));
                Assert.IsFalse(_messenger.IsRegistered<OpenContextMenuMessage>(controller));
                Assert.IsFalse(_messenger.IsRegistered<ClosePaletteContextMenuMessage>(controller));
                _messenger.Send<ClosePaletteContextMenuMessage>();
                Assert.AreEqual(1, _closeCount, "Disposal must unregister palette close requests too.");
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

        _messenger.Send(new OpenContextMenuMessage(request));
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
        _messenger.Send(new OpenContextMenuMessage(new ContextMenuRequest(Mock.Of<IContextMenuContext>())));
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
            _messenger.Send<ClosePaletteContextMenuMessage>();
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

    [TestMethod]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None, false)]
    [DataRow(VirtualKey.F6, VirtualKeyModifiers.None, true)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, false)]
    [DataRow(VirtualKey.K, VirtualKeyModifiers.Control, true)]
    public void RequestedShortcut_InvokesOrDefersItsSubmenu(VirtualKey key, VirtualKeyModifiers modifiers, bool hasSubmenu)
    {
        var chord = new KeyChord(modifiers, (int)key, 0);
        var model = new CommandContextItem(new NoOpCommand { Name = "Requested action" })
        {
            RequestedShortcut = chord,
            MoreCommands = hasSubmenu ? [new CommandContextItem(new NoOpCommand { Name = "Child" })] : [],
        };
        var command = new CommandContextItemViewModel(model, new(_pageContext));
        try
        {
            command.SlowInitializeProperties();
            var context = new Mock<ICommandBarContext>();
            context.Setup(value => value.FindKeybinding(chord)).Returns(command);
            _context = context.Object;
            List<PerformCommandMessage> invocations = [];
            _messenger.Register<PerformCommandMessage>(this, (_, message) => invocations.Add(message));
            using var controller = CreateController();

            Assert.IsTrue(controller.TryHandleShortcut(chord));
            Assert.IsEmpty(_opened);
            DrainCallbacks();

            if (hasSubmenu)
            {
                Assert.IsEmpty(invocations);
                Assert.HasCount(1, _opened);
                Assert.AreSame(command, _opened[0].InitialSubmenu);
                Assert.AreSame(_context, _opened[0].Context);
            }
            else
            {
                Assert.IsEmpty(_opened);
                Assert.HasCount(1, invocations);
                Assert.AreSame(model, invocations[0].Context);
            }
        }
        finally
        {
            command.SafeCleanup();
        }
    }

    private static KeyChord CtrlK => new(VirtualKeyModifiers.Control, (int)VirtualKey.K, 0);

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
            () => _closeCount++),
        _messenger);

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
