// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using Kind = PowerDisplay.Common.Services.TrayWheelFeedbackSession.PresentationKind;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelFeedbackSessionTests
{
    [TestMethod]
    public void StartHover_BeforeDelay_IsHidden()
    {
        var session = new TrayWheelFeedbackSession();

        var result = session.StartHover(1000);

        Assert.AreEqual(Kind.Hidden, result.Kind);
        Assert.AreEqual(Kind.Hidden, session.Tick(1499, pointerInside: true).Kind);
    }

    [TestMethod]
    public void Tick_AtHoverDelay_ShowsAppName()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);

        Assert.AreEqual(Kind.AppName, session.Tick(1500, pointerInside: true).Kind);
    }

    [TestMethod]
    public void RepeatedStartHover_DoesNotResetDelay()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);
        _ = session.StartHover(1300);

        Assert.AreEqual(Kind.AppName, session.Tick(1500, pointerInside: true).Kind);
    }

    [TestMethod]
    public void ShowAdjustment_IsImmediate()
    {
        var session = new TrayWheelFeedbackSession();

        var result = session.ShowAdjustment("Primary display · 55%", 1000);

        Assert.AreEqual(Kind.Adjustment, result.Kind);
        Assert.AreEqual("Primary display · 55%", result.Text);
    }

    [TestMethod]
    public void ShowAdjustment_Whitespace_ThrowsArgumentException()
    {
        var session = new TrayWheelFeedbackSession();

        Assert.ThrowsException<ArgumentException>(() => session.ShowAdjustment("   ", 1000));
    }

    [TestMethod]
    public void SubsequentAdjustment_ExtendsDeadline()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);
        _ = session.ShowAdjustment("60%", 2500);

        var result = session.Tick(4499, pointerInside: true);

        Assert.AreEqual(Kind.Adjustment, result.Kind);
        Assert.AreEqual("60%", result.Text);
        Assert.AreEqual(Kind.AppName, session.Tick(4500, pointerInside: true).Kind);
    }

    [TestMethod]
    public void AdjustmentExpiryInside_ReturnsAppName()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(Kind.AppName, session.Tick(3000, pointerInside: true).Kind);
    }

    [TestMethod]
    public void PointerLeave_HidesAndClearsSession()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(Kind.Hidden, session.Tick(1100, pointerInside: false).Kind);
        Assert.IsFalse(session.IsHovering);
    }

    [TestMethod]
    public void ClearAdjustmentInside_ShowsAppNameImmediately()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(
            Kind.AppName,
            session.ClearAdjustment(1100, pointerInside: true).Kind);
    }

    [TestMethod]
    public void ClearAdjustmentOutside_Hides()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(
            Kind.Hidden,
            session.ClearAdjustment(1100, pointerInside: false).Kind);
    }

    [TestMethod]
    public void Stop_IsIdempotent()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);

        Assert.AreEqual(Kind.Hidden, session.Stop().Kind);
        Assert.AreEqual(Kind.Hidden, session.Stop().Kind);
    }

    [TestMethod]
    public void Tick_HandlesMonotonicWraparound()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(long.MaxValue - 100);

        Assert.AreEqual(
            Kind.AppName,
            session.Tick(long.MinValue + 399, pointerInside: true).Kind);
    }

    [TestMethod]
    public void NextTransitionDelay_Idle_IsNull()
    {
        var session = new TrayWheelFeedbackSession();

        Assert.IsNull(session.NextTransitionDelay(1000));
    }

    [TestMethod]
    public void NextTransitionDelay_DuringHoverDelay_IsRemainingDelay()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);

        Assert.AreEqual(500L, session.NextTransitionDelay(1000));
        Assert.AreEqual(200L, session.NextTransitionDelay(1300));
    }

    [TestMethod]
    public void NextTransitionDelay_AfterHoverDelay_IsNull()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(1000);
        _ = session.Tick(1500, pointerInside: true);

        // AppName is terminal: only the pointer leaving ends it, so nothing needs to be armed.
        Assert.IsNull(session.NextTransitionDelay(1500));
        Assert.IsNull(session.NextTransitionDelay(9000));
    }

    [TestMethod]
    public void NextTransitionDelay_WhileAdjustmentVisible_IsRemainingLifetime()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        Assert.AreEqual(2000L, session.NextTransitionDelay(1000));
        Assert.AreEqual(500L, session.NextTransitionDelay(2500));
    }

    [TestMethod]
    public void NextTransitionDelay_PastAdjustmentDeadline_IsZero()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);

        // Never negative: the caller arms an immediate tick that retires the adjustment.
        Assert.AreEqual(0L, session.NextTransitionDelay(5000));
    }

    [TestMethod]
    public void NextTransitionDelay_AfterClearAdjustment_IsNull()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);
        _ = session.ClearAdjustment(1100, pointerInside: true);

        Assert.IsNull(session.NextTransitionDelay(1100));
    }

    [TestMethod]
    public void NextTransitionDelay_AfterStop_IsNull()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);
        _ = session.Stop();

        Assert.IsNull(session.NextTransitionDelay(1100));
    }

    [TestMethod]
    public void NextTransitionDelay_HandlesMonotonicWraparound()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.StartHover(long.MaxValue - 100);

        // Wrapping past long.MaxValue puts 300 ms on the clock, leaving 200 ms of hover delay.
        Assert.AreEqual(200L, session.NextTransitionDelay(long.MinValue + 199));
    }
}
