// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShortcutBreakthroughDetectorTests
{
    [TestMethod]
    public void ThreePressesWithinTwoSecondsTriggerAndReset()
    {
        var detector = new ShortcutBreakthroughDetector();
        Assert.IsFalse(detector.RegisterPress(0));
        Assert.IsFalse(detector.RegisterPress(Stopwatch.Frequency));
        Assert.IsTrue(detector.RegisterPress(2 * Stopwatch.Frequency));
        Assert.IsFalse(detector.RegisterPress(2 * Stopwatch.Frequency));
    }

    [TestMethod]
    public void ExpiredPressesDoNotCountTowardBreakthrough()
    {
        var detector = new ShortcutBreakthroughDetector();
        Assert.IsFalse(detector.RegisterPress(0));
        Assert.IsFalse(detector.RegisterPress(Stopwatch.Frequency));
        Assert.IsFalse(detector.RegisterPress((2 * Stopwatch.Frequency) + 1));
        Assert.IsTrue(detector.RegisterPress(3 * Stopwatch.Frequency));
    }

    [TestMethod]
    public void ResetDiscardsPreviousPresses()
    {
        var detector = new ShortcutBreakthroughDetector();
        Assert.IsFalse(detector.RegisterPress(0));
        Assert.IsFalse(detector.RegisterPress(1));
        detector.Reset();
        Assert.IsFalse(detector.RegisterPress(2));
    }

    [TestMethod]
    public void OlderQueuedInputCannotCombineWithNewerPresses()
    {
        var detector = new ShortcutBreakthroughDetector();
        Assert.IsFalse(detector.RegisterPress(10 * Stopwatch.Frequency));
        Assert.IsFalse(detector.RegisterPress(11 * Stopwatch.Frequency));
        Assert.IsFalse(detector.RegisterPress(0));
    }
}
