// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Unit tests for <see cref="AdjustCommandExecutor"/> (relative up/down on continuous settings).
/// </summary>
[TestClass]
public class AdjustCommandExecutorTests
{
    private const int DefaultStep = 5;
    private const string StepExpectedRange = "[0, 2147483647]";

    private static readonly IReadOnlySet<string> EmptyHidden =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>Brightness-capable monitor with the given current value.</summary>
    private static Monitor BrightnessMon(int current) => new()
    {
        Id = "A",
        MonitorNumber = 1,
        Name = "TestMon",
        CommunicationMethod = "DDC/CI",
        Capabilities = MonitorCapabilities.Brightness,
        ReadValues = MonitorReadFlags.Brightness,
        CurrentBrightness = current,
    };

    private sealed class RecordingManager : IMonitorManager
    {
        public int Calls { get; private set; }

        public Task<MonitorOperationResult> SetBrightnessAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public Task<MonitorOperationResult> SetContrastAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public Task<MonitorOperationResult> SetVolumeAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public Task<MonitorOperationResult> SetColorTemperatureAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public Task<MonitorOperationResult> SetInputSourceAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public Task<MonitorOperationResult> SetPowerStateAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }

        public Task<MonitorOperationResult> SetRotationAsync(string id, int v, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(MonitorOperationResult.Success());
        }
    }

    private static async Task AssertNegativeStepRejectedAsync(int? requestedStep, int defaultStep, int expectedStep)
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = requestedStep };
        var manager = new RecordingManager();

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(manager, snapshot, EmptyHidden, req, isUp: true, defaultStep, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
        Assert.AreEqual(CliMessageIds.OutOfRange, error.Error.MessageId);
        Assert.AreEqual("step", error.Error.Setting);
        Assert.AreEqual(expectedStep.ToString(CultureInfo.InvariantCulture), error.Error.Value);
        Assert.AreEqual(StepExpectedRange, error.Error.ExpectedRange);
        Assert.AreEqual(0, manager.Calls);
        Assert.AreNotEqual(CliErrorCodes.HardwareFailure, error.Error.Code);
    }

    [TestMethod]
    public async Task Up_ExplicitNegativeStep_ReturnsOutOfRange_AndSkipsHardware()
        => await AssertNegativeStepRejectedAsync(-1, DefaultStep, -1);

    [TestMethod]
    public async Task Up_ExplicitIntMinStep_ReturnsOutOfRange_AndSkipsHardware()
        => await AssertNegativeStepRejectedAsync(int.MinValue, DefaultStep, int.MinValue);

    [TestMethod]
    public async Task Up_NegativeDefaultStep_ReturnsOutOfRange_AndSkipsHardware()
        => await AssertNegativeStepRejectedAsync(null, -1, -1);

    [TestMethod]
    public async Task Up_AddsStep_AndReportsBeforeAfter()
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 20 };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual("brightness", result!.Setting);
        Assert.AreEqual("50%", result.BeforeDisplay);
        Assert.AreEqual("70%", result.AfterDisplay);
        Assert.AreEqual("up", result.Command);
    }

    [TestMethod]
    public async Task Up_ClampsToMax100()
    {
        var snapshot = new List<Monitor> { BrightnessMon(95) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 10 };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(error);
        Assert.AreEqual("100%", result!.AfterDisplay);
    }

    [TestMethod]
    public async Task Up_HugeStep_ClampsToMax_WithoutOverflow()
    {
        // A pathologically large step must not overflow `current + delta` (which, computed in int,
        // would wrap negative and clamp to 0 — turning an `up` into a slam-to-minimum). It must
        // clamp to 100.
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = int.MaxValue };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(error);
        Assert.AreEqual("100%", result!.AfterDisplay);
    }

    [TestMethod]
    public async Task Up_CurrentValueUnread_ReturnsHardwareFailure()
    {
        // The monitor advertises brightness (Supports passes) but discovery never read the live value
        // (ReadValues lacks Brightness, so CurrentBrightness is the fabricated default 0). Relative
        // adjust must NOT compute from that default and silently write an absolute value; it must
        // surface a hardware failure so the caller knows the starting point was unknown.
        var monitor = new Monitor
        {
            Id = "A",
            MonitorNumber = 1,
            Name = "TestMon",
            CommunicationMethod = "DDC/CI",
            Capabilities = MonitorCapabilities.Brightness,
            ReadValues = MonitorReadFlags.None,
            CurrentBrightness = 0,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 10 };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.HardwareFailure, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.HardwareFailure, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Down_ClampsToMin0()
    {
        var snapshot = new List<Monitor> { BrightnessMon(3) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 10 };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: false, DefaultStep, default);

        Assert.IsNull(error);
        Assert.AreEqual("0%", result!.AfterDisplay);
    }

    [TestMethod]
    public async Task NullStep_UsesDefaultStep()
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = null };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(error);
        Assert.AreEqual("55%", result!.AfterDisplay, "null step must fall back to the supplied default (5)");
    }

    [TestMethod]
    public async Task StepZero_IsNoOp_BeforeEqualsAfter()
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 0 };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(error);
        Assert.AreEqual("50%", result!.BeforeDisplay);
        Assert.AreEqual("50%", result.AfterDisplay);
    }

    [TestMethod]
    public async Task UnknownMonitor_ReturnsMonitorNotFound()
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 9, Setting = "brightness" };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.MonitorNotFound, error!.Error.ExitCode);
    }

    [TestMethod]
    public async Task Brightness_NotSupported_ReturnsUnsupportedFeature()
    {
        var monitor = new Monitor { Id = "F", MonitorNumber = 6, Name = "NoBrightnessMon", Capabilities = MonitorCapabilities.None };
        var snapshot = new List<Monitor> { monitor };
        var req = new AdjustRequest { MonitorNumber = 6, Setting = "brightness" };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.UnsupportedFeature, error!.Error.ExitCode);
    }

    [TestMethod]
    public async Task DiscreteSetting_ReturnsUnsupportedFeature()
    {
        // color-temperature is a known but DISCRETE setting: relative adjust rejects it as UNSUPPORTED
        // via the Kind!=Continuous check, which runs BEFORE the Supports check. SupportsColorTemperature
        // is deliberately left false: pinning the kind-specific message makes the branch order
        // load-bearing — a reorder that ran Supports first would emit the generic "is not supported".
        var monitor = new Monitor { Id = "C", MonitorNumber = 3, Name = "ColorMon" };
        var snapshot = new List<Monitor> { monitor };
        var req = new AdjustRequest { MonitorNumber = 3, Setting = "color-temperature" };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliErrorCodes.UnsupportedFeature, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.UnsupportedFeature, error.Error.ExitCode);
        Assert.AreEqual(CliMessageIds.NotAdjustable, error.Error.MessageId);
    }

    [TestMethod]
    public async Task UnknownSetting_ReturnsArgumentError()
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "flicker-rate" };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.ArgumentError, error!.Error.ExitCode);
    }

    [TestMethod]
    public async Task HardwareFailure_ReturnsHardwareFailure()
    {
        var snapshot = new List<Monitor> { BrightnessMon(50) };
        var req = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 10 };

        var (result, error) = await AdjustCommandExecutor.ExecuteAsync(new FailingManager(), snapshot, EmptyHidden, req, isUp: true, DefaultStep, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.HardwareFailure, error!.Error.ExitCode);
    }
}
