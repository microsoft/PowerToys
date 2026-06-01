// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using static PowerDisplay.Common.Services.LinkedBrightnessPlanner;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Behavior tests for the pure linked-brightness decision logic: which monitors the master drives,
/// whether the slider is usable, and the initial seed value. These cover review-flagged cases:
/// restart with no displays yet, all-excluded, single controllable display, and the
/// primary-display seed preference — without needing a WinUI DispatcherQueue.
/// </summary>
[TestClass]
public class LinkedBrightnessPlannerTests
{
    private static LinkTarget Monitor(
        string id,
        int number,
        int brightness,
        bool supports = true,
        bool excluded = false,
        bool isPrimary = false,
        bool hasValidBrightness = true)
        => new LinkTarget(id, number, brightness, supports, excluded, isPrimary, hasValidBrightness);

    [TestMethod]
    public void HasAnyTarget_EmptyList_False()
    {
        Assert.IsFalse(LinkedBrightnessPlanner.HasAnyTarget(new List<LinkTarget>()));
    }

    [TestMethod]
    public void HasAnyTarget_OnlyNonBrightnessMonitors_False()
    {
        var monitors = new[] { Monitor("a", 1, 50, supports: false) };
        Assert.IsFalse(LinkedBrightnessPlanner.HasAnyTarget(monitors));
    }

    [TestMethod]
    public void HasAnyTarget_AllExcluded_False()
    {
        // Regression for: excluding every monitor must disable the master slider rather than leave
        // it enabled with an empty broadcast target set.
        var monitors = new[]
        {
            Monitor("a", 1, 40, excluded: true),
            Monitor("b", 2, 60, excluded: true),
        };
        Assert.IsFalse(LinkedBrightnessPlanner.HasAnyTarget(monitors));
        Assert.IsNull(LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void HasAnyTarget_OneIncludedAmongExcluded_True()
    {
        var monitors = new[]
        {
            Monitor("a", 1, 40, excluded: true),
            Monitor("b", 2, 60, excluded: false),
        };
        Assert.IsTrue(LinkedBrightnessPlanner.HasAnyTarget(monitors));
    }

    [TestMethod]
    public void CountTargets_IgnoresExcludedAndNonBrightness()
    {
        var monitors = new[]
        {
            Monitor("a", 1, 40),
            Monitor("b", 2, 60, excluded: true),
            Monitor("c", 3, 70, supports: false),
            Monitor("d", 4, 80),
        };
        Assert.AreEqual(2, LinkedBrightnessPlanner.CountTargets(monitors));
    }

    [TestMethod]
    public void ResolveIsPrimary_CurrentLookupMatchesGdiDeviceName()
    {
        Assert.IsTrue(LinkedBrightnessPlanner.ResolveIsPrimary(
            @"\\.\DISPLAY3",
            currentPrimaryGdiDeviceName: @"\\.\display3"));
    }

    [TestMethod]
    public void ResolveIsPrimary_FailedCurrentLookup_ReturnsFalse()
    {
        Assert.IsFalse(LinkedBrightnessPlanner.ResolveIsPrimary(
            @"\\.\DISPLAY1",
            currentPrimaryGdiDeviceName: null));
    }

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
    public void Seed_PrefersPrimaryDisplay_RegardlessOfDisplayNumber()
    {
        var monitors = new[]
        {
            Monitor("a", 1, 30),
            Monitor("b", 2, 60, isPrimary: true),
            Monitor("c", 3, 90),
        };
        Assert.AreEqual(60, LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_MirroredPrimaryDisplays_UsesDeterministicDisplayNumberTieBreak()
    {
        var monitors = new[]
        {
            Monitor("b", 2, 60, isPrimary: true),
            Monitor("a", 1, 30, isPrimary: true),
        };
        Assert.AreEqual(30, LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_UnreadablePrimaryDisplay_FallsBackToReadableDisplay()
    {
        var monitors = new[]
        {
            Monitor("a", 1, 30, isPrimary: true, hasValidBrightness: false),
            Monitor("b", 2, 60),
        };
        Assert.AreEqual(60, LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_ExcludedPrimaryDisplay_FallsBackToLinkedDisplay()
    {
        var monitors = new[]
        {
            Monitor("a", 1, 30, excluded: true, isPrimary: true),
            Monitor("b", 2, 60),
        };
        Assert.AreEqual(60, LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_NoReadableLinkedTarget_Null()
    {
        var monitors = new[]
        {
            Monitor("a", 1, 30, isPrimary: true, hasValidBrightness: false),
            Monitor("b", 2, 60, hasValidBrightness: false),
        };
        Assert.IsNull(LinkedBrightnessPlanner.Seed(monitors));
    }

    [TestMethod]
    public void Seed_SkipsExcludedAndNonBrightness_WhenPickingLowestNumber()
    {
        // Display 1 is excluded and Display 2 has no brightness control, so the seed is Display 3.
        var monitors = new[]
        {
            Monitor("a", 1, 30, excluded: true),
            Monitor("b", 2, 55, supports: false),
            Monitor("c", 3, 72),
        };
        Assert.AreEqual(72, LinkedBrightnessPlanner.Seed(monitors));
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
        Assert.IsTrue(LinkedBrightnessPlanner.HasAnyTarget(monitors));
    }

    [TestMethod]
    public void ShouldDisableLinkedModeBeforeProfileApply_LinkedModeActive_True()
    {
        Assert.IsTrue(LinkedBrightnessPlanner.ShouldDisableLinkedModeBeforeProfileApply(linkedLevelsActive: true));
    }

    [TestMethod]
    public void ShouldDisableLinkedModeBeforeProfileApply_LinkedModeInactive_False()
    {
        Assert.IsFalse(LinkedBrightnessPlanner.ShouldDisableLinkedModeBeforeProfileApply(linkedLevelsActive: false));
    }
}
