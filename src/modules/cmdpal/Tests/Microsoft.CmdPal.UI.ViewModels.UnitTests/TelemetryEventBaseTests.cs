// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Telemetry.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public sealed class TelemetryEventBaseTests
{
    [TestMethod]
    public void EventBase_ContainsRequiredWinExtIdentity()
    {
        var telemetryEvent = new EventBase();

        Assert.AreEqual((ushort)13, telemetryEvent.PartA_PrivacyProduct);
        Assert.AreEqual((ushort)1, telemetryEvent.PartA_PrivacyDataCategory);
    }
}
