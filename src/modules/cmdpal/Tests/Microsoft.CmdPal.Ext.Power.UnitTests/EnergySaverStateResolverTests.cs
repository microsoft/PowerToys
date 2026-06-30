// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Classes;
using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Windows.System.Power;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class EnergySaverStateResolverTests
{
    [TestMethod]
    public void ResolveIsOn_WinRtOff_IgnoresStaleRegistryOn()
    {
        var signals = new EnergySaverSignals(
            HasWinRt: true,
            WinRtStatus: EnergySaverStatus.Off,
            HasOverlay: true,
            OverlayGuid: PowerModeCatalog.Balanced.Guid,
            HasSystemStatus: false,
            SystemOn: false,
            HasRegistry: true,
            RegistryOn: true);

        Assert.IsFalse(EnergySaverStateResolver.ResolveIsOn(in signals));
    }

    [TestMethod]
    public void ResolveIsOn_WinRtOn_ReturnsOn()
    {
        var signals = new EnergySaverSignals(
            HasWinRt: true,
            WinRtStatus: EnergySaverStatus.On,
            HasOverlay: true,
            OverlayGuid: PowerModeCatalog.Balanced.Guid,
            HasSystemStatus: false,
            SystemOn: false,
            HasRegistry: true,
            RegistryOn: false);

        Assert.IsTrue(EnergySaverStateResolver.ResolveIsOn(in signals));
    }

    [TestMethod]
    public void ResolveIsOn_OverlayOnly_UsesEfficiencyOverlay()
    {
        Assert.IsTrue(EnergySaverStateResolver.ResolveIsOn(new EnergySaverSignals(
            HasWinRt: false,
            WinRtStatus: EnergySaverStatus.Off,
            HasOverlay: true,
            OverlayGuid: PowerModeCatalog.BestEfficiency.Guid,
            HasSystemStatus: false,
            SystemOn: false,
            HasRegistry: false,
            RegistryOn: false)));

        Assert.IsFalse(EnergySaverStateResolver.ResolveIsOn(new EnergySaverSignals(
            HasWinRt: false,
            WinRtStatus: EnergySaverStatus.Off,
            HasOverlay: true,
            OverlayGuid: PowerModeCatalog.Balanced.Guid,
            HasSystemStatus: false,
            SystemOn: false,
            HasRegistry: false,
            RegistryOn: false)));
    }

    [TestMethod]
    public void ResolveIsOn_RegistryOnly_UsesRegistryWhenNoRuntimeSignals()
    {
        Assert.IsTrue(EnergySaverStateResolver.ResolveIsOn(new EnergySaverSignals(
            HasWinRt: false,
            WinRtStatus: EnergySaverStatus.Off,
            HasOverlay: false,
            OverlayGuid: Guid.Empty,
            HasSystemStatus: false,
            SystemOn: false,
            HasRegistry: true,
            RegistryOn: true)));

        Assert.IsFalse(EnergySaverStateResolver.ResolveIsOn(new EnergySaverSignals(
            HasWinRt: false,
            WinRtStatus: EnergySaverStatus.Off,
            HasOverlay: false,
            OverlayGuid: Guid.Empty,
            HasSystemStatus: false,
            SystemOn: false,
            HasRegistry: true,
            RegistryOn: false)));
    }

    [TestMethod]
    public void ResolveIsOn_SystemStatusOnly_UsesSystemFlag()
    {
        Assert.IsTrue(EnergySaverStateResolver.ResolveIsOn(new EnergySaverSignals(
            HasWinRt: false,
            WinRtStatus: EnergySaverStatus.Off,
            HasOverlay: false,
            OverlayGuid: Guid.Empty,
            HasSystemStatus: true,
            SystemOn: true,
            HasRegistry: false,
            RegistryOn: false)));
    }
}
