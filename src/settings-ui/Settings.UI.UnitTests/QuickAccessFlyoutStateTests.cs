// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.QuickAccess;
using Microsoft.PowerToys.QuickAccess.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Settings.UI.UnitTests;

[TestClass]
public sealed class QuickAccessFlyoutStateTests
{
    [TestMethod]
    public void ShowImmediatelyAfterImplicitDismissalReopens()
    {
        var state = new QuickAccessFlyoutState();
        Assert.AreEqual(QuickAccessFlyoutAction.Show, state.RequestShow());
        state.OnOpened();
        state.OnClosed();
        Assert.AreEqual(QuickAccessFlyoutAction.Show, state.RequestShow());
    }

    [TestMethod]
    public void RepeatedShowDoesNotToggleOrRestartAnimation()
    {
        var state = new QuickAccessFlyoutState();
        state.RequestShow();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.RequestShow());
        state.OnOpened();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.RequestShow());
    }

    [TestMethod]
    public void ActionDuringOpeningHidesWhenOpeningCompletes()
    {
        var state = new QuickAccessFlyoutState();
        state.RequestShow();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.RequestHide());
        Assert.AreEqual(QuickAccessFlyoutAction.Hide, state.OnOpened());
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.OnClosed());
    }

    [TestMethod]
    public void NewShowSupersedesPendingHideDuringOpening()
    {
        var state = new QuickAccessFlyoutState();
        state.RequestShow();
        state.RequestHide();
        state.RequestShow();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.OnOpened());
    }

    [TestMethod]
    public void ShowDuringExplicitCloseIsReplayedOnce()
    {
        var state = new QuickAccessFlyoutState();
        state.RequestShow();
        state.OnOpened();
        Assert.AreEqual(QuickAccessFlyoutAction.Hide, state.RequestHide());
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.RequestShow());
        Assert.AreEqual(QuickAccessFlyoutAction.Show, state.OnClosed());
        state.OnOpened();
        state.RequestHide();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.OnClosed());
    }

    [TestMethod]
    public void HideDuringClosingCancelsPendingReopen()
    {
        var state = new QuickAccessFlyoutState();
        state.RequestShow();
        state.OnOpened();
        state.RequestHide();
        state.RequestShow();
        state.RequestHide();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.OnClosed());
    }

    [TestMethod]
    public void TrayHideArrivingAfterImplicitCloseDoesNotReopen()
    {
        var state = new QuickAccessFlyoutState();
        state.RequestShow();
        state.OnOpened();
        state.OnClosed();
        Assert.AreEqual(QuickAccessFlyoutAction.None, state.RequestHide());
        Assert.AreEqual(QuickAccessFlyoutAction.Show, state.RequestShow());
    }

    [TestMethod]
    public void LaunchContextParsesSeparateHideEvent()
    {
        var context = QuickAccessLaunchContext.Parse(["--show-event=show", "--hide-event=hide", "--exit-event=exit"]);
        Assert.AreEqual("show", context.ShowEventName);
        Assert.AreEqual("hide", context.HideEventName);
        Assert.AreEqual("exit", context.ExitEventName);
    }

    [TestMethod]
    public void LaunchContextStillAcceptsExistingArguments()
    {
        var context = QuickAccessLaunchContext.Parse(["--show-event=show", "--exit-event=exit"]);
        Assert.IsNull(context.HideEventName);
        Assert.AreEqual("show", context.ShowEventName);
    }
}
