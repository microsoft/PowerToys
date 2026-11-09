// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ShortcutGuide.Helpers;

namespace ShortcutGuide.UnitTests.ActivationTests;

[TestClass]
public sealed class ShortcutGuideActivationPolicyTests
{
    [TestMethod]
    [DataRow(ShortcutGuideWindowsKeyAction.Off, ShortcutGuideActivationAction.None)]
    [DataRow(ShortcutGuideWindowsKeyAction.TaskbarIndicators, ShortcutGuideActivationAction.ShowTaskbarIndicators)]
    [DataRow(ShortcutGuideWindowsKeyAction.OpenShortcutGuide, ShortcutGuideActivationAction.ShowFullGuide)]
    public void GetActivationAction_WindowsKeyHold_UsesConfiguredAction(
        ShortcutGuideWindowsKeyAction windowsKeyAction,
        ShortcutGuideActivationAction expected)
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.WindowsKeyHold,
            isOverlayVisible: false,
            isCurrentWindowExcluded: false,
            ShortcutGuideActivationSource.None,
            ShortcutGuideOverlaySurface.Hidden,
            windowsKeyAction);

        Assert.AreEqual(expected, action);
    }

    [TestMethod]
    [DataRow(ShortcutGuideWindowsKeyAction.Off)]
    [DataRow(ShortcutGuideWindowsKeyAction.TaskbarIndicators)]
    [DataRow(ShortcutGuideWindowsKeyAction.OpenShortcutGuide)]
    public void GetActivationAction_RegularHotkey_AlwaysOpensFullGuide(ShortcutGuideWindowsKeyAction windowsKeyAction)
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.RegularHotkey,
            isOverlayVisible: false,
            isCurrentWindowExcluded: false,
            ShortcutGuideActivationSource.None,
            ShortcutGuideOverlaySurface.Hidden,
            windowsKeyAction);

        Assert.AreEqual(ShortcutGuideActivationAction.ShowFullGuide, action);
    }

    [TestMethod]
    public void GetActivationAction_RegularHotkey_PromotesHoldIndicators()
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.RegularHotkey,
            isOverlayVisible: true,
            isCurrentWindowExcluded: false,
            ShortcutGuideActivationSource.WindowsKeyHold,
            ShortcutGuideOverlaySurface.TaskbarIndicators,
            ShortcutGuideWindowsKeyAction.TaskbarIndicators);

        Assert.AreEqual(ShortcutGuideActivationAction.ShowFullGuide, action);
    }

    [TestMethod]
    public void GetActivationAction_RegularHotkey_TakesOwnershipOfHoldGuide()
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.RegularHotkey,
            isOverlayVisible: true,
            isCurrentWindowExcluded: false,
            ShortcutGuideActivationSource.WindowsKeyHold,
            ShortcutGuideOverlaySurface.FullGuide,
            ShortcutGuideWindowsKeyAction.OpenShortcutGuide);

        Assert.AreEqual(ShortcutGuideActivationAction.ShowFullGuide, action);
    }

    [TestMethod]
    public void GetActivationAction_RegularHotkey_ClosesRegularGuide()
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.RegularHotkey,
            isOverlayVisible: true,
            isCurrentWindowExcluded: false,
            ShortcutGuideActivationSource.RegularHotkey,
            ShortcutGuideOverlaySurface.FullGuide,
            ShortcutGuideWindowsKeyAction.TaskbarIndicators);

        Assert.AreEqual(ShortcutGuideActivationAction.Close, action);
    }

    [TestMethod]
    public void GetActivationAction_HoldWhileRegularGuideVisible_DoesNothing()
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.WindowsKeyHold,
            isOverlayVisible: true,
            isCurrentWindowExcluded: false,
            ShortcutGuideActivationSource.RegularHotkey,
            ShortcutGuideOverlaySurface.FullGuide,
            ShortcutGuideWindowsKeyAction.OpenShortcutGuide);

        Assert.AreEqual(ShortcutGuideActivationAction.None, action);
    }

    [TestMethod]
    [DataRow(ShortcutGuideActivationSource.RegularHotkey, ShortcutGuideWindowsKeyAction.TaskbarIndicators)]
    [DataRow(ShortcutGuideActivationSource.WindowsKeyHold, ShortcutGuideWindowsKeyAction.TaskbarIndicators)]
    [DataRow(ShortcutGuideActivationSource.WindowsKeyHold, ShortcutGuideWindowsKeyAction.OpenShortcutGuide)]
    public void GetActivationAction_ExcludedApp_SuppressesHiddenOverlay(
        ShortcutGuideActivationSource activationSource,
        ShortcutGuideWindowsKeyAction windowsKeyAction)
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            activationSource,
            isOverlayVisible: false,
            isCurrentWindowExcluded: true,
            ShortcutGuideActivationSource.None,
            ShortcutGuideOverlaySurface.Hidden,
            windowsKeyAction);

        Assert.AreEqual(ShortcutGuideActivationAction.None, action);
    }

    [TestMethod]
    [DataRow(
        ShortcutGuideActivationSource.RegularHotkey,
        ShortcutGuideOverlaySurface.FullGuide,
        ShortcutGuideActivationAction.Close)]
    [DataRow(
        ShortcutGuideActivationSource.WindowsKeyHold,
        ShortcutGuideOverlaySurface.TaskbarIndicators,
        ShortcutGuideActivationAction.ShowFullGuide)]
    public void GetActivationAction_ExcludedApp_DoesNotSuppressVisibleOverlay(
        ShortcutGuideActivationSource activeSource,
        ShortcutGuideOverlaySurface activeSurface,
        ShortcutGuideActivationAction expected)
    {
        var action = ShortcutGuideActivationPolicy.GetActivationAction(
            ShortcutGuideActivationSource.RegularHotkey,
            isOverlayVisible: true,
            isCurrentWindowExcluded: true,
            activeSource,
            activeSurface,
            ShortcutGuideWindowsKeyAction.TaskbarIndicators);

        Assert.AreEqual(expected, action);
    }

    [TestMethod]
    [DataRow(ShortcutGuideActivationSource.RegularHotkey, ShortcutGuideOverlaySurface.FullGuide, true, false)]
    [DataRow(ShortcutGuideActivationSource.RegularHotkey, ShortcutGuideOverlaySurface.FullGuide, false, false)]
    [DataRow(ShortcutGuideActivationSource.WindowsKeyHold, ShortcutGuideOverlaySurface.TaskbarIndicators, false, true)]
    [DataRow(ShortcutGuideActivationSource.WindowsKeyHold, ShortcutGuideOverlaySurface.FullGuide, true, true)]
    [DataRow(ShortcutGuideActivationSource.WindowsKeyHold, ShortcutGuideOverlaySurface.FullGuide, false, false)]
    public void ShouldCloseOnWindowsKeyRelease_UsesActivationOwnership(
        ShortcutGuideActivationSource activeSource,
        ShortcutGuideOverlaySurface activeSurface,
        bool closeFullGuideOnRelease,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            ShortcutGuideActivationPolicy.ShouldCloseOnWindowsKeyRelease(activeSource, activeSurface, closeFullGuideOnRelease));
    }
}
