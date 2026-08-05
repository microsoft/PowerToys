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
        int generation = state.Begin();

        state.Cancel();

        Assert.IsFalse(state.ShouldShow(generation));
        Assert.IsFalse(state.IsVisible);
    }

    [TestMethod]
    public void Begin_AfterCancellation_RearmsDisplay()
    {
        var state = new DelayedDisplayState();
        int cancelledGeneration = state.Begin();
        state.Cancel();

        int rearmedGeneration = state.Begin();

        Assert.IsFalse(state.ShouldShow(cancelledGeneration));
        Assert.IsTrue(state.ShouldShow(rearmedGeneration));
        Assert.IsTrue(state.IsVisible);
    }
}
