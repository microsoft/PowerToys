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

    [TestMethod]
    public void FetchCapabilities_EmptyCaps_ProbeSucceeds_ReturnsValidWithSynthesizedCaps()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            Assert.AreEqual((byte)0x10, code);
            cur = 50;
            max = 100;
            return true;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => string.Empty,
            readVcpFeature: ProbeReader);

        Assert.IsTrue(result.IsValid, "Probe success should rescue the monitor");
        Assert.AreEqual(string.Empty, result.CapabilitiesString);
        Assert.IsNotNull(result.VcpCapabilitiesInfo);
        Assert.IsTrue(result.VcpCapabilitiesInfo!.SupportsVcpCode(0x10));
        Assert.AreEqual(1, result.VcpCapabilitiesInfo.SupportedVcpCodes.Count);
        Assert.AreEqual(string.Empty, result.VcpCapabilitiesInfo.Raw);
    }

    [TestMethod]
    public void FetchCapabilities_EmptyCaps_ProbeFails_ReturnsInvalid()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            cur = 0;
            max = 0;
            return false;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => string.Empty,
            readVcpFeature: ProbeReader);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void FetchCapabilities_EmptyCaps_ProbeReturnsZeroMax_ReturnsInvalid()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            cur = 50;
            max = 0;
            return true;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => string.Empty,
            readVcpFeature: ProbeReader);

        Assert.IsFalse(result.IsValid, "max=0 must not be treated as supported");
    }

    private const string CapsWithoutBrightness =
        "(prot(monitor)type(lcd)model(TestMonitor)vcp(12 14)mccs_ver(2.2))";

    // Cap string non-empty but parses to no usable VCP codes.
    // If this string ends up with capabilities==null or 1+ VCP entries in the parser,
    // change to "(prot(monitor)mccs_ver(2.1))".
    private const string CapsParsesToZeroVcpCodes = "(garbage)";

    [TestMethod]
    public void FetchCapabilities_CapsMissingBrightness_ProbeSucceeds_InjectsBrightness()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            Assert.AreEqual((byte)0x10, code);
            cur = 75;
            max = 100;
            return true;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => CapsWithoutBrightness,
            readVcpFeature: ProbeReader);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(CapsWithoutBrightness, result.CapabilitiesString);
        Assert.IsNotNull(result.VcpCapabilitiesInfo);
        Assert.IsTrue(result.VcpCapabilitiesInfo!.SupportsVcpCode(0x10));

        // Existing VCP entries from the cap string must remain intact
        Assert.IsTrue(result.VcpCapabilitiesInfo.SupportsVcpCode(0x12));
        Assert.IsTrue(result.VcpCapabilitiesInfo.SupportsVcpCode(0x14));
    }

    [TestMethod]
    public void FetchCapabilities_CapsMissingBrightness_ProbeFails_ReturnsInvalid()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            cur = 0;
            max = 0;
            return false;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => CapsWithoutBrightness,
            readVcpFeature: ProbeReader);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void FetchCapabilities_CapsParseToZeroVcp_ProbeSucceeds_RescuesWithSynthesized()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            cur = 50;
            max = 100;
            return true;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => CapsParsesToZeroVcpCodes,
            readVcpFeature: ProbeReader);

        Assert.IsTrue(result.IsValid);
        Assert.IsNotNull(result.VcpCapabilitiesInfo);
        Assert.IsTrue(result.VcpCapabilitiesInfo!.SupportsVcpCode(0x10));
    }

    [TestMethod]
    public void FetchCapabilities_CapsParseToZeroVcp_ProbeFails_ReturnsInvalid()
    {
        bool ProbeReader(IntPtr h, byte code, out uint cur, out uint max)
        {
            cur = 0;
            max = 0;
            return false;
        }

        var result = DdcCiNative.FetchCapabilitiesForTest(
            FakeHandle,
            readCapsString: _ => CapsParsesToZeroVcpCodes,
            readVcpFeature: ProbeReader);

        Assert.IsFalse(result.IsValid);
    }
}
