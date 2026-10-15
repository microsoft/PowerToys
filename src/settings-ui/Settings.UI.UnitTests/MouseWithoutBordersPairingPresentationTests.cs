// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;

namespace Microsoft.PowerToys.Settings.UI.UnitTests;

[TestClass]
public sealed class MouseWithoutBordersPairingPresentationTests
{
    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:TEST", true, true)]
    [DataRow("mwb-dpapi:v1:", true, false)]
    [DataRow("mwb-dpapi:v1:!", true, false)]
    [DataRow("mwb-dpapi:v2:TEST", true, false)]
    [DataRow("MWB-DPAPI:v1:TEST", true, false)]
    [DataRow("mwb-dpapi:v1:A QID", true, false)]
    [DataRow("ordinary", false, false)]
    [DataRow(null, false, false)]
    public void ClassifierReservesMalformedAndUnsupportedEnvelopes(string value, bool reserved, bool valid)
    {
        Assert.AreEqual(reserved, PairingKeyCodec.IsEncryptedValue(value));
        Assert.AreEqual(valid, PairingKeyCodec.HasValidEncryptedFormat(value));
    }

    [TestMethod]
    public void ServiceDisplayRequiresFreshRpcResultForPlaintext()
    {
        const string encrypted = "mwb-dpapi:v1:TEST";
        var state = new MouseWithoutBordersPairingState();
        Assert.AreEqual("********", state.GetDisplayValue(true, true, encrypted));
        state.TransientSecurityKey = "RPC-KEY";
        Assert.AreEqual("********", state.GetDisplayValue(false, true, encrypted));
        Assert.AreEqual("RPC-KEY", state.GetDisplayValue(true, true, encrypted));
        state.TransientSecurityKey = null;
        Assert.AreEqual(string.Empty, state.GetDisplayValue(true, true, "SAVED-PLAINTEXT"));
        Assert.AreEqual("SAVED-PLAINTEXT", state.GetDisplayValue(false, false, "SAVED-PLAINTEXT"));
    }

    [TestMethod]
    public void ModeTransitionsRequireSetupRatherThanAcceptingMarkers()
    {
        Assert.IsTrue(MouseWithoutBordersPairingState.PairingSetupRequired("mwb-dpapi:v1:TEST", false));
        Assert.IsTrue(MouseWithoutBordersPairingState.PairingSetupRequired("ordinary", true));
        Assert.IsTrue(MouseWithoutBordersPairingState.PairingSetupRequired("********", false));
        Assert.IsTrue(MouseWithoutBordersPairingState.PairingSetupRequired("Pairing setup required", false));
        Assert.IsFalse(MouseWithoutBordersPairingState.PairingSetupRequired("ordinary", false));
        Assert.IsFalse(MouseWithoutBordersPairingState.PairingSetupRequired("mwb-dpapi:v1:TEST", true));
    }

    [DataTestMethod]
    [DataRow(false, false, "ordinary", false, "ordinary")]
    [DataRow(true, false, "ordinary", false, "ordinary")]
    [DataRow(false, true, "ordinary", true, "")]
    [DataRow(true, true, "ordinary", true, "")]
    [DataRow(false, true, "mwb-dpapi:v1:TEST", false, "********")]
    [DataRow(true, true, "mwb-dpapi:v1:TEST", false, "********")]
    [DataRow(false, false, "mwb-dpapi:v1:TEST", true, "")]
    [DataRow(true, false, "mwb-dpapi:v1:TEST", true, "")]
    [DataRow(true, true, "mwb-dpapi:v1:!", true, "")]
    [DataRow(true, true, "********", true, "")]
    public void PresentationUsesPersistedFormatAndSettingsElevation(bool elevated, bool serviceEnabled, string persisted, bool setupRequired, string display)
    {
        Assert.AreEqual(setupRequired, MouseWithoutBordersPairingState.PairingSetupRequired(persisted, serviceEnabled));
        Assert.AreEqual(display, new MouseWithoutBordersPairingState().GetDisplayValue(elevated, serviceEnabled, persisted));
    }
}
