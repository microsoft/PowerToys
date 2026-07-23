// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public void SubsequentAdjustment_ExtendsDeadline()
    {
        var session = new TrayWheelFeedbackSession();
        _ = session.ShowAdjustment("55%", 1000);
        _ = session.ShowAdjustment("60%", 2500);

        Assert.AreEqual(Kind.Adjustment, session.Tick(4499, pointerInside: true).Kind);
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
}
