// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AccessKeyModeControllerTests
{
    [TestMethod]
    public void InputHandlerFactoryTransfersOwnershipToController()
    {
        var controller = new AccessKeyModeController();
        var firstHandler = new Mock<IDisposable>();
        var replacementHandler = new Mock<IDisposable>();

        controller.AttachInput(owner =>
        {
            Assert.AreSame(controller, owner);
            return firstHandler.Object;
        });

        controller.Exit();
        firstHandler.Verify(handler => handler.Dispose(), Times.Never);

        controller.AttachInput(_ => replacementHandler.Object);
        firstHandler.Verify(handler => handler.Dispose(), Times.Once);
        replacementHandler.Verify(handler => handler.Dispose(), Times.Never);

        controller.Dispose();
        controller.Dispose();
        firstHandler.Verify(handler => handler.Dispose(), Times.Once);
        replacementHandler.Verify(handler => handler.Dispose(), Times.Once);
    }

    [TestMethod]
    public void AltTapTogglesMode()
    {
        var controller = new AccessKeyModeController();
        var nativeExitRequests = 0;
        controller.ExitRequested += (_, _) => nativeExitRequests++;

        AltTap(controller);
        Assert.IsTrue(controller.IsActive);

        AltTap(controller);
        Assert.IsFalse(controller.IsActive);
        Assert.AreEqual(0, nativeExitRequests);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeferredExitKeepsCuesInSyncWithNativeMode(bool nativeModeEnabled)
    {
        var controller = new AccessKeyModeController();
        AltTap(controller);
        if (nativeModeEnabled)
        {
            controller.HandleNativeDisplayModeChanged(true);
        }

        var generation = controller.HandleKeyDown(Chord(VirtualKey.A));

        Assert.IsTrue(generation.HasValue);
        Assert.IsTrue(controller.IsActive);

        controller.ExitIfCurrent(generation.GetValueOrDefault(), nativeModeEnabled);
        Assert.AreEqual(nativeModeEnabled, controller.IsActive);

        AltTap(controller);
        Assert.AreEqual(!nativeModeEnabled, controller.IsActive);
        controller.HandleNativeDisplayModeChanged(!nativeModeEnabled);
        Assert.AreEqual(!nativeModeEnabled, controller.IsActive);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExitRequestsNativeCleanupEvenWhenManagedModeIsInactive(bool initiallyActive)
    {
        var controller = new AccessKeyModeController();
        if (initiallyActive)
        {
            AltTap(controller);
        }

        var nativeExitRequests = 0;
        controller.ExitRequested += (_, _) =>
        {
            Assert.IsFalse(controller.IsActive);
            nativeExitRequests++;
        };

        controller.Exit();
        controller.Exit();

        Assert.AreEqual(2, nativeExitRequests);
    }

    [TestMethod]
    public void NativeExitClearsCuesAndInvalidatesDeferredDismissal()
    {
        var controller = new AccessKeyModeController();
        AltTap(controller);
        controller.HandleNativeDisplayModeChanged(true);
        var generation = controller.HandleKeyDown(Chord(VirtualKey.A));
        var nativeExitRequests = 0;
        controller.ExitRequested += (_, _) => nativeExitRequests++;

        controller.HandleNativeDisplayModeChanged(false);
        Assert.IsFalse(controller.IsActive);
        Assert.AreEqual(0, nativeExitRequests);

        AltTap(controller);
        Assert.IsTrue(generation.HasValue);
        controller.ExitIfCurrent(generation.GetValueOrDefault(), isNativeDisplayModeEnabled: false);
        Assert.IsTrue(controller.IsActive);
        Assert.AreEqual(0, nativeExitRequests);
    }

    [TestMethod]
    public void ScopeInvalidationRejectsStaleDeferredExit()
    {
        var controller = new AccessKeyModeController();
        AltTap(controller);
        var staleGeneration = controller.HandleKeyDown(Chord(VirtualKey.Number1));

        controller.InvalidateScope();
        AltTap(controller);
        Assert.IsTrue(staleGeneration.HasValue);
        controller.ExitIfCurrent(staleGeneration.GetValueOrDefault(), isNativeDisplayModeEnabled: false);

        Assert.IsTrue(controller.IsActive);
    }

    [TestMethod]
    public void ScopeInvalidationCancelsPendingAltTap()
    {
        var controller = new AccessKeyModeController();
        var nativeExitRequests = 0;
        controller.ExitRequested += (_, _) => nativeExitRequests++;
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));

        controller.InvalidateScope();
        controller.HandleKeyUp(VirtualKey.Menu);

        Assert.IsFalse(controller.IsActive);
        Assert.AreEqual(1, nativeExitRequests);
    }

    [TestMethod]
    [DataRow(VirtualKey.Menu, false)]
    [DataRow(VirtualKey.Menu, true)]
    [DataRow(VirtualKey.LeftMenu, false)]
    [DataRow(VirtualKey.LeftMenu, true)]
    [DataRow(VirtualKey.RightMenu, false)]
    [DataRow(VirtualKey.RightMenu, true)]
    public void HandledAltShortcutSuppressesNativeModeThroughAltRelease(VirtualKey altKey, bool releaseNumberFirst)
    {
        var controller = new AccessKeyModeController();
        controller.HandleKeyDown(Chord(altKey, VirtualKeyModifiers.Menu));
        controller.HandleKeyDown(Chord(VirtualKey.Number1, VirtualKeyModifiers.Menu));
        controller.SuppressNativeDisplayMode();

        Assert.IsTrue(controller.IsNativeDisplayModeSuppressed);

        if (releaseNumberFirst)
        {
            controller.HandleKeyUp(VirtualKey.Number1);
            Assert.IsTrue(controller.IsNativeDisplayModeSuppressed);
        }

        controller.HandleKeyUp(altKey);
        controller.HandleNativeDisplayModeChanged(false);

        Assert.IsTrue(controller.IsNativeDisplayModeSuppressed);
        Assert.IsFalse(controller.IsActive);
        controller.HandleKeyUp(VirtualKey.Number1);
        Assert.IsTrue(controller.IsNativeDisplayModeSuppressed);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void ManagedModeExitPreservesNativeModeSuppression(bool navigate, bool releaseAltFirst)
    {
        var controller = new AccessKeyModeController();
        AltTap(controller);
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));
        var managedGeneration = controller.HandleKeyDown(Chord(VirtualKey.Number1, VirtualKeyModifiers.Menu | VirtualKeyModifiers.Shift));
        controller.SuppressNativeDisplayMode();
        if (releaseAltFirst)
        {
            controller.HandleKeyUp(VirtualKey.Menu);
        }

        if (navigate)
        {
            controller.InvalidateScope();
        }
        else
        {
            Assert.IsTrue(managedGeneration.HasValue);
            controller.ExitIfCurrent(managedGeneration.GetValueOrDefault(), isNativeDisplayModeEnabled: false);
        }

        if (!releaseAltFirst)
        {
            Assert.IsTrue(controller.IsNativeDisplayModeSuppressed);
            controller.HandleKeyUp(VirtualKey.Menu);
        }

        Assert.IsTrue(controller.IsNativeDisplayModeSuppressed);
        Assert.IsFalse(controller.IsActive);
    }

    [TestMethod]
    public void NewAltTapCancelsNativeModeSuppression()
    {
        var controller = new AccessKeyModeController();
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));
        controller.HandleKeyDown(Chord(VirtualKey.Number1, VirtualKeyModifiers.Menu));
        controller.SuppressNativeDisplayMode();
        controller.HandleKeyUp(VirtualKey.Menu);

        AltTap(controller);

        Assert.IsFalse(controller.IsNativeDisplayModeSuppressed);
        Assert.IsTrue(controller.IsActive);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void UnhandledAltChordDoesNotSuppressNativeMode(bool handleNumberFirst)
    {
        var controller = new AccessKeyModeController();
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));
        if (handleNumberFirst)
        {
            controller.HandleKeyDown(Chord(VirtualKey.Number1, VirtualKeyModifiers.Menu));
            controller.SuppressNativeDisplayMode();
        }

        controller.HandleKeyDown(Chord(VirtualKey.A, VirtualKeyModifiers.Menu));

        Assert.IsFalse(controller.IsNativeDisplayModeSuppressed);
        controller.HandleKeyUp(VirtualKey.A);
        controller.HandleKeyUp(VirtualKey.Menu);
        Assert.IsFalse(controller.IsNativeDisplayModeSuppressed);
        Assert.IsFalse(controller.IsActive);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExitCancelsNativeModeSuppression(bool releaseAlt)
    {
        var controller = new AccessKeyModeController();
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));
        controller.HandleKeyDown(Chord(VirtualKey.Number1, VirtualKeyModifiers.Menu));
        controller.SuppressNativeDisplayMode();
        if (releaseAlt)
        {
            controller.HandleKeyUp(VirtualKey.Menu);
        }

        controller.Exit();

        controller.HandleKeyUp(VirtualKey.Menu);
        Assert.IsFalse(controller.IsNativeDisplayModeSuppressed);

        AltTap(controller);
        Assert.IsTrue(controller.IsActive);
    }

    private static void AltTap(AccessKeyModeController controller)
    {
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));
        controller.HandleKeyUp(VirtualKey.Menu);
    }

    private static KeyChord Chord(
        VirtualKey key,
        VirtualKeyModifiers modifiers = VirtualKeyModifiers.None) =>
        new(modifiers, (int)key, 0);
}
