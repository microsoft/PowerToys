// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Drivers.DDC;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Models;
using static PowerDisplay.UnitTests.DdcFakes;

namespace PowerDisplay.UnitTests;

/// <summary>
/// Exercises the shared write boundary with an injected writer. The nonzero handles used here
/// are test tokens; every read and write is injected, so these tests never access a monitor.
/// </summary>
[TestClass]
public sealed class DdcCiControllerVcpValueBlockTests
{
    private const string AffectedMonitorId = @"\\?\DISPLAY#SAM105C#5&ABC&0&UID1";
    private const string OtherMonitorId = @"\\?\DISPLAY#AOCB326#5&ABC&0&UID2";

    [TestMethod]
    public async Task HardwareRule_HardOffNeverReachesWriterEvenWhenUserAllowsIt()
    {
        var writer = new RecordingVcpWriter();
        using var controller = NewController(writer, (_, _, _) => false);

        var result = await controller.SetPowerStateAsync(NewMonitor(AffectedMonitorId), 0x05);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage!, "disabled");
        Assert.AreEqual(0, writer.Writes.Count);
    }

    [TestMethod]
    [DataRow(AffectedMonitorId, 0xD6, 0x01)]
    [DataRow(AffectedMonitorId, 0xD6, 0x04)]
    [DataRow(AffectedMonitorId, 0x14, 0x05)]
    [DataRow(AffectedMonitorId, 0x60, 0x05)]
    [DataRow(MonitorId, 0xD6, 0x05)]
    public async Task HardwareRule_LeavesOtherStatesCodesAndMonitorsWritable(string monitorId, int code, int value)
    {
        var writer = new RecordingVcpWriter();
        using var controller = NewController(writer);
        var monitor = NewMonitor(monitorId);

        var result = await WriteAsync(controller, monitor, code, value);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(1, writer.Writes.Count);
        Assert.AreEqual((monitor.Handle, (byte)code, (uint)value), writer.Writes[0]);
    }

    [TestMethod]
    [DataRow(0x10)]
    [DataRow(0x12)]
    [DataRow(0x62)]
    [DataRow(0x14)]
    [DataRow(0x60)]
    [DataRow(0xD6)]
    public async Task UserRule_CoversEveryVcpWriteEntryPoint(int code)
    {
        var writer = new RecordingVcpWriter();
        using var controller = NewController(
            writer,
            (id, vcpCode, value) => id == MonitorId && vcpCode == code && value == 30);

        var result = await WriteAsync(controller, NewMonitor(), code, 30);

        Assert.IsFalse(result.IsSuccess);
        StringAssert.Contains(result.ErrorMessage!, "disabled");
        Assert.AreEqual(0, writer.Writes.Count);
    }

    [TestMethod]
    [DataRow(OtherMonitorId, 0x14, 0x05)]
    [DataRow(MonitorId, 0x60, 0x05)]
    [DataRow(MonitorId, 0x14, 0x06)]
    public async Task UserRule_DoesNotBlockADifferentMonitorCodeOrValue(string monitorId, int code, int value)
    {
        var writer = new RecordingVcpWriter();
        using var controller = NewController(
            writer,
            (id, vcpCode, rawValue) => id == MonitorId && vcpCode == 0x14 && rawValue == 0x05);

        var result = await WriteAsync(controller, NewMonitor(monitorId), code, value);

        Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
        Assert.AreEqual(1, writer.Writes.Count);
        Assert.AreEqual((byte)code, writer.Writes[0].Code);
        Assert.AreEqual((uint)value, writer.Writes[0].Value);
    }

    [TestMethod]
    public async Task UserRule_ConsultsTheLatestSnapshotForEveryWrite()
    {
        using var manager = new MonitorManager(new RecordingKnownGoodStore());
        var writer = new RecordingVcpWriter();
        using var controller = NewController(writer, manager.IsVcpValueBlocked);
        var monitor = NewMonitor();

        var initiallyAllowed = await controller.SetPowerStateAsync(monitor, 0x05);
        manager.SetDisabledVcpValues(new Dictionary<string, List<VcpValueBlock>>
        {
            [MonitorId] = new() { new() { VcpCode = 0xD6, Values = new() { 0x05 } } },
        });
        var disabled = await controller.SetPowerStateAsync(monitor, 0x05);
        var wake = await controller.SetPowerStateAsync(monitor, 0x01);
        manager.SetDisabledVcpValues(Array.Empty<KeyValuePair<string, List<VcpValueBlock>>>());
        var allowedAgain = await controller.SetPowerStateAsync(monitor, 0x05);

        Assert.IsTrue(initiallyAllowed.IsSuccess, initiallyAllowed.ErrorMessage);
        Assert.IsFalse(disabled.IsSuccess);
        StringAssert.Contains(disabled.ErrorMessage!, "disabled");
        Assert.IsTrue(wake.IsSuccess, wake.ErrorMessage);
        Assert.IsTrue(allowedAgain.IsSuccess, allowedAgain.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                (monitor.Handle, (byte)0xD6, 0x05U),
                (monitor.Handle, (byte)0xD6, 0x01U),
                (monitor.Handle, (byte)0xD6, 0x05U),
            },
            writer.Writes);
    }

    [TestMethod]
    [DataRow(0x10, 50, 40, 60, 30)]
    [DataRow(0x12, 80, 25, 50, 40)]
    [DataRow(0x62, 200, 10, 20, 40)]
    public async Task ContinuousWrites_CheckTheScaledRawValue(
        int code,
        int maximum,
        int blockedPercentage,
        int allowedPercentage,
        int allowedRawValue)
    {
        var writer = new RecordingVcpWriter();
        var checkedValues = new List<int>();
        using var controller = NewController(writer, (id, vcpCode, value) =>
        {
            Assert.AreEqual(MonitorId, id);
            Assert.AreEqual((byte)code, vcpCode);
            checkedValues.Add(value);
            return value == 20;
        });
        var monitor = NewMonitor();
        monitor.BrightnessVcpMax = maximum;
        monitor.ContrastVcpMax = maximum;
        monitor.VolumeVcpMax = maximum;

        var disabled = await WriteAsync(controller, monitor, code, blockedPercentage);
        var allowed = await WriteAsync(controller, monitor, code, allowedPercentage);

        Assert.IsFalse(disabled.IsSuccess);
        StringAssert.Contains(disabled.ErrorMessage!, "disabled");
        Assert.IsTrue(allowed.IsSuccess, allowed.ErrorMessage);
        CollectionAssert.AreEqual(new[] { 20, allowedRawValue }, checkedValues);
        Assert.AreEqual(1, writer.Writes.Count);
        Assert.AreEqual((monitor.Handle, (byte)code, (uint)allowedRawValue), writer.Writes[0]);
    }

    [TestMethod]
    public async Task WriteRestriction_IsCheckedBeforeHandleValidation()
    {
        var writer = new RecordingVcpWriter();
        var blockWrite = true;
        using var controller = NewController(writer, (_, _, _) => blockWrite);
        var monitor = NewMonitor();
        monitor.Handle = IntPtr.Zero;

        var disabled = await controller.SetPowerStateAsync(monitor, 0x05);
        blockWrite = false;
        var invalidHandle = await controller.SetPowerStateAsync(monitor, 0x05);

        Assert.IsFalse(disabled.IsSuccess);
        StringAssert.Contains(disabled.ErrorMessage!, "disabled");
        Assert.IsFalse(invalidHandle.IsSuccess);
        Assert.AreEqual("Invalid monitor handle", invalidHandle.ErrorMessage);
        Assert.AreEqual(0, writer.Writes.Count);
    }

    [TestMethod]
    [DataRow(0x10)]
    [DataRow(0x12)]
    [DataRow(0x62)]
    [DataRow(0x14)]
    [DataRow(0x60)]
    [DataRow(0xD6)]
    public async Task Reads_DoNotConsultWriteRestrictions(int code)
    {
        var writer = new RecordingVcpWriter();
        var reader = new RecordingVcpReader(VcpReadAttempt.Success(5, 100));
        var restrictionCalls = 0;
        using var controller = NewController(
            writer,
            (_, _, _) =>
            {
                restrictionCalls++;
                return true;
            },
            reader);

        var value = await ReadAsync(controller, NewMonitor(AffectedMonitorId), code);

        Assert.IsTrue(value.IsValid);
        Assert.AreEqual(5, value.Current);
        Assert.AreEqual(0, restrictionCalls);
        CollectionAssert.AreEqual(new[] { (byte)code }, reader.Codes);
        Assert.AreEqual(0, writer.Writes.Count);
    }

    private static DdcCiController NewController(
        RecordingVcpWriter writer,
        Func<string, byte, int, bool>? isBlocked = null,
        IVcpFeatureReader? reader = null) =>
        new(
            new RecordingKnownGoodStore(),
            reader ?? new RecordingVcpReader(),
            isVcpValueBlocked: isBlocked,
            writeVcpFeature: writer.Write);

    private static Monitor NewMonitor(string id = MonitorId) => new()
    {
        Id = id,
        Handle = new IntPtr(123),
        BrightnessVcpMax = 100,
        ContrastVcpMax = 100,
        VolumeVcpMax = 100,
    };

    private static Task<MonitorOperationResult> WriteAsync(DdcCiController controller, Monitor monitor, int code, int value) => code switch
    {
        0x10 => controller.SetBrightnessAsync(monitor, value),
        0x12 => controller.SetContrastAsync(monitor, value),
        0x62 => controller.SetVolumeAsync(monitor, value),
        0x14 => controller.SetColorTemperatureAsync(monitor, value),
        0x60 => controller.SetInputSourceAsync(monitor, value),
        0xD6 => controller.SetPowerStateAsync(monitor, value),
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private static Task<VcpFeatureValue> ReadAsync(DdcCiController controller, Monitor monitor, int code) => code switch
    {
        0x10 => controller.GetBrightnessAsync(monitor),
        0x12 => controller.GetContrastAsync(monitor),
        0x62 => controller.GetVolumeAsync(monitor),
        0x14 => controller.GetColorTemperatureAsync(monitor),
        0x60 => controller.GetInputSourceAsync(monitor),
        0xD6 => controller.GetPowerStateAsync(monitor),
        _ => throw new ArgumentOutOfRangeException(nameof(code)),
    };

    private sealed class RecordingVcpWriter
    {
        public List<(IntPtr Handle, byte Code, uint Value)> Writes { get; } = new();

        public bool Write(IntPtr handle, byte code, uint value)
        {
            Writes.Add((handle, code, value));
            return true;
        }
    }
}
