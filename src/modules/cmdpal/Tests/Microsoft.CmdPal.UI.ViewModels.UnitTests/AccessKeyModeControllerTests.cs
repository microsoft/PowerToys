// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class AccessKeyModeControllerTests
{
    [TestMethod]
    public void AltTapTogglesMode()
    {
        var controller = new AccessKeyModeController();

        AltTap(controller);
        Assert.IsTrue(controller.IsActive);

        AltTap(controller);
        Assert.IsFalse(controller.IsActive);
    }

    [TestMethod]
    public void NonModifierKeyReturnsDeferredExitGeneration()
    {
        var controller = new AccessKeyModeController();
        AltTap(controller);

        var generation = controller.HandleKeyDown(Chord(VirtualKey.Number1));

        Assert.IsTrue(generation.HasValue);
        Assert.IsTrue(controller.IsActive);

        controller.ExitIfCurrent(generation.GetValueOrDefault());
        Assert.IsFalse(controller.IsActive);
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
        controller.ExitIfCurrent(staleGeneration.GetValueOrDefault());

        Assert.IsTrue(controller.IsActive);
    }

    [TestMethod]
    public void ScopeInvalidationCancelsPendingAltTap()
    {
        var controller = new AccessKeyModeController();
        controller.HandleKeyDown(Chord(VirtualKey.Menu, VirtualKeyModifiers.Menu));

        controller.InvalidateScope();
        controller.HandleKeyUp(VirtualKey.Menu);

        Assert.IsFalse(controller.IsActive);
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
