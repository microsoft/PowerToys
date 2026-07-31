// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class ContinuousVcpInitializerTests
{
    [TestMethod]
    public void Initialize_ProbedValueIsAppliedWithoutReadingAgain()
    {
        // The reader is primed with a failure it must never reach: if the initializer re-reads a
        // code the probe already answered, this test fails on the CallCount assertion rather than
        // on a fabricated value.
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(1));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = BrightnessMonitor();

        var result = initializer.Initialize(monitor, Evidence((0x10, new VcpFeatureValue(30, 0, 100))));

        Assert.IsTrue(result);
        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(30, monitor.CurrentBrightness);
        Assert.AreEqual(100, monitor.BrightnessVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
    }

    [TestMethod]
    public void Initialize_CodeWithoutAProbedValueIsReadOnce()
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(55, 100));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = BrightnessMonitor();

        var result = initializer.Initialize(monitor, Evidence());

        Assert.IsTrue(result);
        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(55, monitor.CurrentBrightness);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
    }

    [TestMethod]
    public void Initialize_InvalidReadRangeIsNotApplied()
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(55, 0));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = BrightnessMonitor();

        var result = initializer.Initialize(monitor, Evidence());

        Assert.IsTrue(result);
        Assert.AreEqual(1, reader.CallCount);
        Assert.AreEqual(0, monitor.CurrentBrightness);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
    }

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void Initialize_HandleClassFailureStopsAndReportsToTheCaller(int errorCode)
    {
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(errorCode));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = BrightnessAndContrastMonitor();

        var result = initializer.Initialize(monitor, Evidence());

        Assert.IsFalse(result, "A handle-class read failure must tell the caller to drop the monitor.");
        CollectionAssert.AreEqual(new byte[] { 0x10 }, reader.Codes, "The dead handle must not be used again.");
        Assert.AreEqual(MonitorReadFlags.None, monitor.ReadValues);
    }

    [TestMethod]
    public void Initialize_FeatureLevelFailureSkipsOnlyThatCode()
    {
        // DDCCI_VCP_NOT_SUPPORTED is the device's answer about one opcode, not about the handle,
        // so the remaining codes still get their read.
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(DdcErrorClassifier.ErrorGraphicsDdcCiVcpNotSupported),
            VcpReadAttempt.Success(60, 100));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = BrightnessAndContrastMonitor();

        var result = initializer.Initialize(monitor, Evidence());

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(new byte[] { 0x10, 0x12 }, reader.Codes);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.Brightness));
        Assert.AreEqual(60, monitor.CurrentContrast);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Contrast));
    }

    private static Monitor BrightnessMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi | MonitorCapabilities.Brightness,
    };

    private static Monitor BrightnessAndContrastMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi |
            MonitorCapabilities.Brightness |
            MonitorCapabilities.Contrast,
    };

    private static VcpDiscoveryEvidence Evidence(params (byte Code, VcpFeatureValue Value)[] probed)
    {
        var capabilities = new VcpCapabilities();
        capabilities.SupportedVcpCodes[0x10] = new VcpCodeInfo(0x10, "Brightness");
        capabilities.SupportedVcpCodes[0x12] = new VcpCodeInfo(0x12, "Contrast");

        var values = new Dictionary<byte, VcpFeatureValue>();
        foreach (var (code, value) in probed)
        {
            values[code] = value;
        }

        return new VcpDiscoveryEvidence(string.Empty, capabilities, values);
    }
}
