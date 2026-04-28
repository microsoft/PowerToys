// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;

namespace PowerDisplay.UnitTests;

[TestClass]
public class DdcCiNativeFetchCapabilitiesTests
{
    private static readonly IntPtr FakeHandle = new(1);

    private const string CapsWithBrightness =
        "(prot(monitor)type(lcd)model(TestMonitor)vcp(10 12)mccs_ver(2.2))";

    [TestMethod]
    public void FetchCapabilities_HappyPath_BrightnessInCaps_NoProbeCalled()
    {
        bool probeWasCalled = false;
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            probeWasCalled = true;
            cur = 0;
            max = 0;
            return false;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => CapsWithBrightness,
            readVcpFeature: ProbeReader);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(CapsWithBrightness, result.CapabilitiesString);
        Assert.IsNotNull(result.VcpCapabilitiesInfo);
        Assert.IsTrue(result.VcpCapabilitiesInfo!.SupportsVcpCode(0x10));
        Assert.IsFalse(probeWasCalled, "Probe must not run when caps string already lists 0x10");
    }
}
