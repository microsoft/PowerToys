// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using static PowerDisplay.Common.Services.LinkedBrightnessPlanner;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Behavior tests for pure linked-brightness decision logic. These cover review-flagged seed
/// cases without needing a WinUI DispatcherQueue.
/// </summary>
[TestClass]
public class LinkedBrightnessPlannerTests
{
    private static LinkTarget Monitor(
        string id,
        int number,
        int brightness)
        => new LinkTarget(id, number, brightness);

    [TestMethod]
    public void Seed_EmptyList_Null()
    {
        Assert.IsNull(LinkedBrightnessPlanner.Seed(new List<LinkTarget>()));
    }

    [TestMethod]
    public void Seed_PrefersLowestDisplayNumber_RegardlessOfListOrder()
    {
        // Enumeration order is deliberately reversed; the seed must still come from Display 1.
        var monitors = new[]
        {
            Monitor("c", 3, 90),
            Monitor("a", 1, 30),
            Monitor("b", 2, 60),
        };
        Assert.AreEqual(30, LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_UnknownDisplayNumbers_FallBackToIdOrder()
    {
        // MonitorNumber 0 means "unknown"; those sort last and tie-break by Id for determinism.
        var monitors = new[]
        {
            Monitor("z", 0, 90),
            Monitor("m", 0, 45),
        };
        Assert.AreEqual(45, LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_SingleControllableDisplay_UsesItsBrightness()
    {
        var monitors = new[] { Monitor("only", 1, 64) };
        Assert.AreEqual(64, LinkedBrightnessPlanner.Seed(monitors));
    }
}
