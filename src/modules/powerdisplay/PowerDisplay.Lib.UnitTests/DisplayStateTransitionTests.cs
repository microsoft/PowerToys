// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Helpers;

namespace PowerDisplay.UnitTests;

[TestClass]
public class DisplayStateTransitionTests
{
    [TestMethod]
    public void ShouldTriggerOn_OffToOn_ReturnsTrue()
    {
        Assert.IsTrue(DisplayStateTransition.ShouldTriggerOn(
            newState: PowerSettingsNative.DisplayStateOn,
            lastState: PowerSettingsNative.DisplayStateOff));
    }

    [TestMethod]
    public void ShouldTriggerOn_DimmedToOn_ReturnsTrue()
    {
        Assert.IsTrue(DisplayStateTransition.ShouldTriggerOn(
            newState: PowerSettingsNative.DisplayStateOn,
            lastState: PowerSettingsNative.DisplayStateDimmed));
    }

    [TestMethod]
    public void ShouldTriggerOn_OnToOn_ReturnsFalse()
    {
        // Initial-state echo from the subscription, or a no-op event.
        Assert.IsFalse(DisplayStateTransition.ShouldTriggerOn(
            newState: PowerSettingsNative.DisplayStateOn,
            lastState: PowerSettingsNative.DisplayStateOn));
    }

    [TestMethod]
    public void ShouldTriggerOn_OnToOff_ReturnsFalse()
    {
        // We only rescan on wake (off → on), not on blank.
        Assert.IsFalse(DisplayStateTransition.ShouldTriggerOn(
            newState: PowerSettingsNative.DisplayStateOff,
            lastState: PowerSettingsNative.DisplayStateOn));
    }

    [TestMethod]
    public void ShouldTriggerOn_OnToDimmed_ReturnsFalse()
    {
        Assert.IsFalse(DisplayStateTransition.ShouldTriggerOn(
            newState: PowerSettingsNative.DisplayStateDimmed,
            lastState: PowerSettingsNative.DisplayStateOn));
    }
}
