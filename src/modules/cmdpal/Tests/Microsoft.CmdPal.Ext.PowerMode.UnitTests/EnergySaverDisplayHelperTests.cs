// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Power.Helpers;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Power.UnitTests;

[TestClass]
public sealed class EnergySaverDisplayHelperTests
{
    [TestMethod]
    public void GetEnergySaverStatusLabel_WhenOn_ShowsOn()
    {
        var snapshot = new EnergySaverSnapshot(ResolvedEnergySaverState.On, CanReadStatus: true, CanAttemptSet: true);
        Assert.AreEqual(Resources.power_mode_energy_saver_on, PowerModeDisplayHelper.GetEnergySaverStatusLabel(snapshot));
    }

    [TestMethod]
    public void GetEnergySaverStatusLabel_WhenOff_ShowsOff()
    {
        var snapshot = new EnergySaverSnapshot(ResolvedEnergySaverState.Off, CanReadStatus: true, CanAttemptSet: true);
        Assert.AreEqual(Resources.power_mode_energy_saver_off, PowerModeDisplayHelper.GetEnergySaverStatusLabel(snapshot));
    }

    [TestMethod]
    public void GetEnergySaverStatusLabel_WhenNotAvailable_ShowsNotAvailable()
    {
        var snapshot = new EnergySaverSnapshot(ResolvedEnergySaverState.NotAvailable, CanReadStatus: true, CanAttemptSet: true);
        Assert.AreEqual(Resources.power_mode_energy_saver_not_available, PowerModeDisplayHelper.GetEnergySaverStatusLabel(snapshot));
    }

    [TestMethod]
    public void GetEnergySaverStatusLabel_WhenUnreadable_ShowsUnknown()
    {
        var snapshot = new EnergySaverSnapshot(ResolvedEnergySaverState.Unknown, CanReadStatus: false, CanAttemptSet: true);
        Assert.AreEqual(Resources.power_mode_energy_saver_unknown, PowerModeDisplayHelper.GetEnergySaverStatusLabel(snapshot));
    }
}
