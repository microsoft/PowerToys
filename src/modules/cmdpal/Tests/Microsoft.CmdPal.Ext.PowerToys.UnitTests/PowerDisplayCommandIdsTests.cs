// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToysExtension.Helpers;

namespace Microsoft.CmdPal.Ext.PowerToys.UnitTests;

[TestClass]
public class PowerDisplayCommandIdsTests
{
    [DataTestMethod]
    [DataRow(1)]
    [DataRow(42)]
    [DataRow(int.MaxValue)]
    public void ApplyProfileId_RoundTrips(int profileId)
    {
        var commandId = PowerDisplayCommandIds.BuildApplyProfileCommandId(profileId);

        Assert.IsTrue(PowerDisplayCommandIds.TryParseApplyProfileCommandId(commandId, out var parsedId));
        Assert.AreEqual(profileId, parsedId);
        var expectedSuffix = "." + profileId.ToString(CultureInfo.InvariantCulture);
        Assert.IsTrue(commandId.EndsWith(expectedSuffix, StringComparison.Ordinal));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("com.microsoft.powertoys.powerDisplay.applyProfile.0")]
    [DataRow("com.microsoft.powertoys.powerDisplay.applyProfile.-1")]
    [DataRow("com.microsoft.powertoys.powerDisplay.applyProfile.+1")]
    [DataRow("com.microsoft.powertoys.powerDisplay.applyProfile.01")]
    [DataRow("com.microsoft.powertoys.powerDisplay.applyProfile.1.extra")]
    [DataRow("com.microsoft.powertoys.powerdisplay.applyProfile.1")]
    public void TryParseApplyProfileCommandId_RejectsNonCanonicalIds(string commandId)
    {
        Assert.IsFalse(PowerDisplayCommandIds.TryParseApplyProfileCommandId(commandId, out var profileId));
        Assert.AreEqual(0, profileId);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void BuildApplyProfileCommandId_RejectsNonPositiveIds(int profileId)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => PowerDisplayCommandIds.BuildApplyProfileCommandId(profileId));
    }
}
