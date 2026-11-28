// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
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
    public void LaunchContextParsesVisibilityEvents()
    {
        var context = QuickAccessLaunchContext.Parse(["--visibility-state-event=state", "--visibility-changed-event=changed", "--exit-event=exit"]);
        Assert.AreEqual("state", context.VisibilityStateEventName);
        Assert.AreEqual("changed", context.VisibilityChangedEventName);
        Assert.AreEqual("exit", context.ExitEventName);
    }

    [TestMethod]
    public void LaunchContextParsesPipeArgumentsWithoutVisibilityEvents()
    {
        var context = QuickAccessLaunchContext.Parse(["--runner-pipe=runner", "--app-pipe=app", "--exit-event=exit"]);
        Assert.IsNull(context.VisibilityStateEventName);
        Assert.IsNull(context.VisibilityChangedEventName);
        Assert.AreEqual("runner", context.RunnerPipeName);
        Assert.AreEqual("app", context.AppPipeName);
    }

    [TestMethod]
    public void CoalescedVisibilitySignalsExposeLatestRequestedState()
    {
        AssertLatestVisibilityState(false, true, expected: true);
        AssertLatestVisibilityState(true, false, expected: false);
    }

    private static void AssertLatestVisibilityState(bool firstRequest, bool latestRequest, bool expected)
    {
        using var stateEvent = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var changedEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        SetVisibilityRequest(firstRequest, stateEvent, changedEvent);
        SetVisibilityRequest(latestRequest, stateEvent, changedEvent);

        Assert.AreEqual(0, WaitHandle.WaitAny([changedEvent], 0));
        Assert.AreEqual(expected, stateEvent.WaitOne(0));
    }

    private static void SetVisibilityRequest(bool isVisible, EventWaitHandle stateEvent, EventWaitHandle changedEvent)
    {
        if (isVisible)
        {
            stateEvent.Set();
        }
        else
        {
            stateEvent.Reset();
        }

        changedEvent.Set();
    }
}
