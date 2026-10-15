// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public sealed class MouseWithoutBordersPairingHelperTests
{
    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:AQID", true, true)]
    [DataRow("mwb-dpapi:v1:", true, false)]
    [DataRow("mwb-dpapi:v1:!", true, false)]
    [DataRow("mwb-dpapi:v2:AQID", true, false)]
    [DataRow("MWB-DPAPI:v1:AQID", true, false)]
    [DataRow("mwb-dpapi:v1:A QID", true, false)]
    [DataRow("ordinary", false, false)]
    [DataRow(null, false, false)]
    public void ClassifierReservesMalformedAndUnsupportedEnvelopes(string value, bool reserved, bool valid)
    {
        Assert.AreEqual(reserved, MouseWithoutBordersPairingHelper.IsEncrypted(value));
        Assert.AreEqual(valid, MouseWithoutBordersPairingHelper.HasValidEncryptedFormat(value));
    }

    [TestMethod]
    public void ServiceDisplayRequiresFreshRpcResultForPlaintext()
    {
        const string encrypted = "mwb-dpapi:v1:AQID";
        Assert.AreEqual("********", MouseWithoutBordersPairingHelper.GetDisplayValue(encrypted, true, true));
        Assert.AreEqual("********", MouseWithoutBordersPairingHelper.GetDisplayValue(encrypted, false, true, "RPC-KEY"));
        Assert.AreEqual("RPC-KEY", MouseWithoutBordersPairingHelper.GetDisplayValue(encrypted, true, true, "RPC-KEY"));
        Assert.AreEqual(string.Empty, MouseWithoutBordersPairingHelper.GetDisplayValue("SAVED-PLAINTEXT", true, true));
        Assert.AreEqual("SAVED-PLAINTEXT", MouseWithoutBordersPairingHelper.GetDisplayValue("SAVED-PLAINTEXT", false, false, "IGNORED"));
    }

    [TestMethod]
    public void ModeTransitionsRequireSetupRatherThanAcceptingMarkers()
    {
        Assert.IsTrue(MouseWithoutBordersPairingHelper.PairingSetupRequired("mwb-dpapi:v1:AQID", false));
        Assert.IsTrue(MouseWithoutBordersPairingHelper.PairingSetupRequired("ordinary", true));
        Assert.IsTrue(MouseWithoutBordersPairingHelper.PairingSetupRequired("********", false));
        Assert.IsFalse(MouseWithoutBordersPairingHelper.PairingSetupRequired("ordinary", false));
        Assert.IsFalse(MouseWithoutBordersPairingHelper.PairingSetupRequired("mwb-dpapi:v1:AQID", true));
        Assert.IsFalse(MouseWithoutBordersPairingHelper.CanManagePairing(false, true));
        Assert.IsTrue(MouseWithoutBordersPairingHelper.CanManagePairing(true, true));
        Assert.IsTrue(MouseWithoutBordersPairingHelper.CanManagePairing(false, false));
    }

    [DataTestMethod]
    [DataRow(false, false, "ordinary", true, false, "ordinary")]
    [DataRow(true, false, "ordinary", true, false, "ordinary")]
    [DataRow(false, true, "ordinary", false, true, "")]
    [DataRow(true, true, "ordinary", true, true, "")]
    [DataRow(false, true, "mwb-dpapi:v1:AQID", false, false, "********")]
    [DataRow(true, true, "mwb-dpapi:v1:AQID", true, false, "********")]
    [DataRow(false, false, "mwb-dpapi:v1:AQID", true, true, "")]
    [DataRow(true, false, "mwb-dpapi:v1:AQID", true, true, "")]
    [DataRow(true, true, "mwb-dpapi:v1:!", true, true, "")]
    [DataRow(true, true, "********", true, true, "")]
    public void PresentationUsesPersistedFormatAndSettingsElevation(bool elevated, bool serviceEnabled, string persisted, bool canManage, bool setupRequired, string display)
    {
        Assert.AreEqual(canManage, MouseWithoutBordersPairingHelper.CanManagePairing(elevated, serviceEnabled));
        Assert.AreEqual(setupRequired, MouseWithoutBordersPairingHelper.PairingSetupRequired(persisted, serviceEnabled));
        Assert.AreEqual(display, MouseWithoutBordersPairingHelper.GetDisplayValue(persisted, elevated, serviceEnabled));
    }
}
