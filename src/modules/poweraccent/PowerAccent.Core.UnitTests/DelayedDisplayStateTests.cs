// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerAccent.Core;

namespace PowerAccent.Core.UnitTests;

[TestClass]
public sealed class DelayedDisplayStateTests
{
    [TestMethod]
    public void Cancel_InvalidatesPendingDisplay()
    {
        var state = new DelayedDisplayState();
        var pendingDisplay = state.Begin(displayDelay: 500);

        state.Cancel();

        Assert.IsFalse(state.ShouldShow(pendingDisplay));
        Assert.IsFalse(state.IsVisible);
    }

    [TestMethod]
    public void Begin_AfterCancellation_RearmsDisplay()
    {
        var state = new DelayedDisplayState();
        var cancelledDisplay = state.Begin(displayDelay: 500);
        state.Cancel();

        var rearmedDisplay = state.Begin(displayDelay: 1000);

        Assert.IsFalse(state.ShouldShow(cancelledDisplay));
        Assert.IsTrue(state.ShouldShow(rearmedDisplay));
        Assert.IsTrue(state.IsVisible);
    }

    [TestMethod]
    public void Begin_SnapshotsDisplayDelay()
    {
        var state = new DelayedDisplayState();

        var pendingDisplay = state.Begin(displayDelay: 500);

        Assert.AreEqual(500, pendingDisplay.Delay);
    }
}
