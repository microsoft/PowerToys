// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers;
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
        // on a fabricated value. The probed range is deliberately not 0-100, so both the raw
        // maximum and the percent scaling have to survive the seam for the assertions to hold.
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(1));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = BrightnessMonitor();

        var result = initializer.Initialize(monitor, Evidence((0x10, new VcpFeatureValue(15, 0, 50))));

        Assert.IsTrue(result);
        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(30, monitor.CurrentBrightness);
        Assert.AreEqual(50, monitor.BrightnessVcpMax);
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

    [TestMethod]
    public void Initialize_EveryContinuousCodeIsReadAndApplied()
    {
        // Pins the invariant ContinuousVcpInitializer's own remarks declare but nothing else
        // enforced: every entry in ContinuousVcpCodes needs an arm in both IsSupported and
        // ApplyValue. Without an IsSupported arm the code is never read, which the Codes assertion
        // catches; without an ApplyValue arm it is read and then discarded, which the per-feature
        // assertions catch. Ranges and percentages are all distinct so a cross-wired arm cannot
        // pass by coincidence.
        Assert.AreEqual(
            3,
            NativeConstants.ContinuousVcpCodes.Length,
            "ContinuousVcpCodes grew — give the new code an IsSupported and an ApplyValue arm, then extend this test.");

        var reader = new RecordingVcpReader(
            VcpReadAttempt.Success(15, 50),
            VcpReadAttempt.Success(20, 40),
            VcpReadAttempt.Success(7, 10));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = AllContinuousMonitor();

        var result = initializer.Initialize(monitor, Evidence());

        Assert.IsTrue(result);
        CollectionAssert.AreEqual(NativeConstants.ContinuousVcpCodes, reader.Codes);

        Assert.AreEqual(30, monitor.CurrentBrightness);
        Assert.AreEqual(50, monitor.BrightnessVcpMax);
        Assert.AreEqual(50, monitor.CurrentContrast);
        Assert.AreEqual(40, monitor.ContrastVcpMax);
        Assert.AreEqual(70, monitor.CurrentVolume);
        Assert.AreEqual(10, monitor.VolumeVcpMax);
        Assert.AreEqual(
            MonitorReadFlags.Brightness | MonitorReadFlags.Contrast | MonitorReadFlags.Volume,
            monitor.ReadValues);
    }

    [TestMethod]
    public void Initialize_ProbedVolumeIsAppliedWithoutReadingAgain()
    {
        // Volume is the one continuous code the probe path is not otherwise exercised against, and
        // it is the one whose ApplyValue arm has no neighbour to shadow a mistake.
        var reader = new RecordingVcpReader(VcpReadAttempt.Failure(1));
        var initializer = new ContinuousVcpInitializer(reader);
        var monitor = VolumeMonitor();

        var result = initializer.Initialize(monitor, Evidence((0x62, new VcpFeatureValue(7, 0, 10))));

        Assert.IsTrue(result);
        Assert.AreEqual(0, reader.CallCount);
        Assert.AreEqual(70, monitor.CurrentVolume);
        Assert.AreEqual(10, monitor.VolumeVcpMax);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.Volume));
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

    private static Monitor VolumeMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi | MonitorCapabilities.Volume,
    };

    private static Monitor AllContinuousMonitor() => new()
    {
        Id = MonitorId,
        Handle = new IntPtr(1),
        Capabilities = MonitorCapabilities.DdcCi |
            MonitorCapabilities.Brightness |
            MonitorCapabilities.Contrast |
            MonitorCapabilities.Volume,
    };

    /// <summary>
    /// Builds evidence carrying only the probed values: <see cref="ContinuousVcpInitializer"/>
    /// reads <see cref="VcpDiscoveryEvidence.InitialValues"/> and nothing else, so a capabilities
    /// object here would be scaffolding no assertion depends on. Support is expressed by the
    /// fixture's <see cref="MonitorCapabilities"/> flags instead, which is what the initializer
    /// actually gates on.
    /// </summary>
    private static VcpDiscoveryEvidence Evidence(params (byte Code, VcpFeatureValue Value)[] probed)
    {
        var values = new Dictionary<byte, VcpFeatureValue>();
        foreach (var (code, value) in probed)
        {
            values[code] = value;
        }

        return new VcpDiscoveryEvidence(string.Empty, new VcpCapabilities(), values);
    }
}
