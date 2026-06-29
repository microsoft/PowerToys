// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class EnergySaverStateHelperTests
{
    [TestMethod]
    public void ResolveEffectiveState_WinRtOff_IgnoresStaleRegistryOn()
    {
        var isOn = EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: true,
            winRtStatus: EnergySaverStatus.Off,
            hasOverlay: true,
            overlayGuid: PowerModeGuids.Balanced,
            hasSystemStatus: false,
            systemOn: false,
            hasRegistry: true,
            registryOn: true);

        Assert.IsFalse(isOn);
    }

    [TestMethod]
    public void ResolveEffectiveState_WinRtOn_ReturnsOn()
    {
        var isOn = EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: true,
            winRtStatus: EnergySaverStatus.On,
            hasOverlay: true,
            overlayGuid: PowerModeGuids.Balanced,
            hasSystemStatus: false,
            systemOn: false,
            hasRegistry: true,
            registryOn: false);

        Assert.IsTrue(isOn);
    }

    [TestMethod]
    public void ResolveEffectiveState_OverlayOnly_UsesEfficiencyOverlay()
    {
        Assert.IsTrue(EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: false,
            winRtStatus: EnergySaverStatus.Off,
            hasOverlay: true,
            overlayGuid: PowerModeGuids.BestEfficiency,
            hasSystemStatus: false,
            systemOn: false,
            hasRegistry: false,
            registryOn: false));

        Assert.IsFalse(EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: false,
            winRtStatus: EnergySaverStatus.Off,
            hasOverlay: true,
            overlayGuid: PowerModeGuids.Balanced,
            hasSystemStatus: false,
            systemOn: false,
            hasRegistry: false,
            registryOn: false));
    }

    [TestMethod]
    public void ResolveEffectiveState_RegistryOnly_UsesRegistryWhenNoRuntimeSignals()
    {
        Assert.IsTrue(EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: false,
            winRtStatus: EnergySaverStatus.Off,
            hasOverlay: false,
            overlayGuid: Guid.Empty,
            hasSystemStatus: false,
            systemOn: false,
            hasRegistry: true,
            registryOn: true));

        Assert.IsFalse(EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: false,
            winRtStatus: EnergySaverStatus.Off,
            hasOverlay: false,
            overlayGuid: Guid.Empty,
            hasSystemStatus: false,
            systemOn: false,
            hasRegistry: true,
            registryOn: false));
    }

    [TestMethod]
    public void ResolveEffectiveState_SystemStatusOnly_UsesSystemFlag()
    {
        Assert.IsTrue(EnergySaverStateHelper.ResolveEffectiveState(
            hasWinRt: false,
            winRtStatus: EnergySaverStatus.Off,
            hasOverlay: false,
            overlayGuid: Guid.Empty,
            hasSystemStatus: true,
            systemOn: true,
            hasRegistry: false,
            registryOn: false));
    }
}
