// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;

namespace PowerDisplay.UnitTests;

[TestClass]
public sealed class DiscreteVcpInitializerTests
{
    private const int VcpNotSupported = unchecked((int)0xC0262584);

    [DataTestMethod]
    [DataRow(DdcErrorClassifier.ErrorGraphicsInvalidPhysicalMonitorHandle)]
    [DataRow(DdcErrorClassifier.ErrorGraphicsMonitorNoLongerExists)]
    public void Initialize_PhysicalMonitorUnavailableStopsRemainingReads(int errorCode)
    {
        var reader = new RecordingReader(VcpReadAttempt.Failure(errorCode));
        var initializer = new DiscreteVcpInitializer(reader);
        var monitor = DiscreteMonitor();

        var result = initializer.Initialize(monitor, new IntPtr(1));

        Assert.AreEqual(VcpInitializationResult.PhysicalMonitorUnavailable, result);
        CollectionAssert.AreEqual(new byte[] { 0x14 }, reader.Codes);
        Assert.AreEqual(MonitorReadFlags.None, monitor.ReadValues);
    }

    [TestMethod]
    public void Initialize_VcpNotSupportedContinuesRemainingReads()
    {
        var reader = new RecordingReader(
            VcpReadAttempt.Failure(VcpNotSupported),
            VcpReadAttempt.Success(current: 0x11, maximum: 0),
            VcpReadAttempt.Success(current: 0x01, maximum: 0));
        var initializer = new DiscreteVcpInitializer(reader);
        var monitor = DiscreteMonitor();

        var result = initializer.Initialize(monitor, new IntPtr(1));

        Assert.AreEqual(VcpInitializationResult.Completed, result);
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
            Id = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID1",
            SupportsColorTemperature = true,
            VcpCapabilitiesInfo = capabilities,
        };
    }

    private sealed class RecordingReader(params VcpReadAttempt[] results) : IVcpFeatureReader
    {
        private readonly Queue<VcpReadAttempt> _results = new(results);

        public List<byte> Codes { get; } = new();

        public VcpReadAttempt Read(IntPtr handle, byte code)
        {
            Codes.Add(code);
            return _results.Dequeue();
        }
    }
}
