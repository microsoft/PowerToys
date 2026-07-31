// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class DiscreteVcpInitializerTests
{
    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void Initialize_FailedReadSkipsOnlyThatFeature(int errorCode)
    {
        // Initialize never inspects read.ErrorCode, so the handle-class codes are the rows worth
        // pinning: they are what would fail if someone copied ContinuousVcpInitializer's
        // drop-the-monitor branch into this stage. Handle liveness is decided by the
        // maximum-compatibility probe and by ContinuousVcpInitializer before this stage runs.
        var reader = new RecordingVcpReader(
            VcpReadAttempt.Failure(errorCode),
            VcpReadAttempt.Success(current: 0x11, maximum: 0),
            VcpReadAttempt.Success(current: 0x01, maximum: 0));
        var initializer = new DiscreteVcpInitializer(reader);
        var monitor = DiscreteMonitor();

        initializer.Initialize(monitor);

        CollectionAssert.AreEqual(new byte[] { 0x14, 0x60, 0xD6 }, reader.Codes);
        Assert.IsFalse(monitor.ReadValues.HasFlag(MonitorReadFlags.ColorTemperature));
        Assert.AreEqual(0x11, monitor.CurrentInputSource);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.InputSource));
        Assert.AreEqual(0x01, monitor.CurrentPowerState);
        Assert.IsTrue(monitor.ReadValues.HasFlag(MonitorReadFlags.PowerState));
    }

    private static Monitor DiscreteMonitor()
    {
        var capabilities = new VcpCapabilities();
        capabilities.SupportedVcpCodes[0x14] = new VcpCodeInfo(0x14, "Select Color Preset");
        capabilities.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source");
        capabilities.SupportedVcpCodes[0xD6] = new VcpCodeInfo(0xD6, "Power Mode");

        return new Monitor
        {
            Id = MonitorId,
            Handle = new IntPtr(1),
            SupportsColorTemperature = true,
            VcpCapabilitiesInfo = capabilities,
        };
    }
}
