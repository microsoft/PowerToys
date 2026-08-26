// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Unit tests for <see cref="SetCommandExecutor"/>. Uses fake <see cref="IMonitorManager"/>
/// implementations to cover all structured error categories (exit codes 1–5) and the
/// success path (exit code 0 with before→after values).
/// </summary>
[TestClass]
public class SetCommandExecutorTests
{
    // ─── Shared test fixtures ─────────────────────────────────────────────────
    private static readonly IReadOnlySet<string> EmptyHidden =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Builds a <see cref="VcpCapabilities"/> that advertises the given VCP codes (no discrete values).
    /// Used to make <see cref="Monitor.SupportsPowerState"/> / <see cref="Monitor.SupportsInputSource"/> return true.
    /// </summary>
    private static VcpCapabilities VcpCapsWithCodes(params byte[] codes)
    {
        var caps = new VcpCapabilities();
        foreach (var code in codes)
        {
            caps.SupportedVcpCodes[code] = new VcpCodeInfo(code, $"0x{code:X2}");
        }

        return caps;
    }

    /// <summary>A monitor with brightness support, current value 42, GDI device name present.</summary>
    private static Monitor BrightnessMon() => new()
    {
        Id = "A",
        MonitorNumber = 1,
        Name = "TestMon",
        CommunicationMethod = "DDC/CI",
        GdiDeviceName = @"\\.\DISPLAY1",
        Capabilities = MonitorCapabilities.Brightness,
        ReadValues = MonitorReadFlags.Brightness,
        CurrentBrightness = 42,
    };

    /// <summary>A monitor with contrast support, current value 55.</summary>
    private static Monitor ContrastMon() => new()
    {
        Id = "B",
        MonitorNumber = 2,
        Name = "ContrastMon",
        CommunicationMethod = "DDC/CI",
        Capabilities = MonitorCapabilities.Contrast,
        ReadValues = MonitorReadFlags.Contrast,
        CurrentContrast = 55,
    };

    // ─── MonitorNotFound (exit code 1) ────────────────────────────────────────
    [TestMethod]
    public async Task Set_UnknownMonitorNumber_ReturnsMonitorNotFound()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 9, Setting = "brightness", RawValue = "50" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.MonitorNotFound, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_HiddenMonitor_ReturnsMonitorNotFound()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var hidden = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase) { "A" };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "50" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, hidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliExitCodes.MonitorNotFound, error!.Error.ExitCode);
    }

    // ─── OutOfRange (exit code 2) ─────────────────────────────────────────────
    [TestMethod]
    public async Task Set_Brightness_OutOfRange_High_ReturnsOutOfRange()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "999" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.OutOfRange, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.OutOfRange, error.Error.ExitCode);
        Assert.IsNotNull(error.Error.ExpectedRange);
        StringAssert.Contains(error.Error.ExpectedRange, "100");
    }

    [TestMethod]
    public async Task Set_Brightness_OutOfRange_Negative_ReturnsOutOfRange()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "-1" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.OutOfRange, error!.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_Contrast_OutOfRange_ReturnsOutOfRange()
    {
        var snapshot = new List<Monitor> { ContrastMon() };
        var req = new SetRequest { MonitorNumber = 2, Setting = "contrast", RawValue = "101" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliErrorCodes.OutOfRange, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.OutOfRange, error.Error.ExitCode);
    }

    // ─── InvalidDiscreteValue (exit code 3) ───────────────────────────────────
    [TestMethod]
    public async Task Set_ColorTemperature_InvalidValue_ReturnsInvalidDiscreteValue()
    {
        var monitor = new Monitor
        {
            Id = "C",
            MonitorNumber = 3,
            Name = "ColorMon",
            SupportsColorTemperature = true,
            ReadValues = MonitorReadFlags.ColorTemperature,
            CurrentColorTemperature = 0x05,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 3, Setting = "color-temperature", RawValue = "not-a-color" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.InvalidDiscreteValue, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_Orientation_InvalidDegrees_ReturnsInvalidDiscreteValue()
    {
        var monitor = new Monitor
        {
            Id = "D",
            MonitorNumber = 4,
            Name = "OrientMon",
            GdiDeviceName = @"\\.\DISPLAY4",
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 4, Setting = "orientation", RawValue = "45" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.InvalidDiscreteValue, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_InputSource_ValueNotInSupportedList_ReturnsInvalidDiscreteValue()
    {
        // The monitor advertises input-source value 0x11. 0x99 parses as a valid byte but is NOT in
        // that set, so it must be rejected via the supported-set branch (MakeDiscreteUnsupportedError)
        // before any hardware write — a different path from the hex-parse failure. Use a VcpCodeInfo
        // WITH a discrete value list (VcpCapsWithCodes builds an empty set, which accepts any value).
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source", new List<int> { 0x11 });
        var monitor = new Monitor
        {
            Id = "E",
            MonitorNumber = 5,
            Name = "InputMon",
            VcpCapabilitiesInfo = caps,
            ReadValues = MonitorReadFlags.InputSource,
            CurrentInputSource = 0x11,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 5, Setting = "input-source", RawValue = "0x99" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.InvalidDiscreteValue, error.Error.ExitCode);

        // Pin the supported-set branch specifically (not the hex-parse branch, which shares the code):
        // DiscreteNotInSet is the "value not in the monitor's advertised set" message id.
        Assert.AreEqual(CliMessageIds.DiscreteNotInSet, error.Error.MessageId);
        Assert.IsNotNull(error.Error.Supported);
    }

    // ─── Discrete settings are hex-only: friendly names are rejected ──────────
    [TestMethod]
    public async Task Set_ColorTemperature_ByFriendlyName_ReturnsInvalidDiscreteValue()
    {
        var monitor = new Monitor
        {
            Id = "C",
            MonitorNumber = 3,
            Name = "ColorMon",
            SupportsColorTemperature = true,
            ReadValues = MonitorReadFlags.ColorTemperature,
            CurrentColorTemperature = 0x05,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 3, Setting = "color-temperature", RawValue = "6500K" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Error.Code);
    }

    [TestMethod]
    public async Task Set_InputSource_ByFriendlyName_ReturnsInvalidDiscreteValue()
    {
        var monitor = new Monitor
        {
            Id = "E",
            MonitorNumber = 5,
            Name = "InputMon",
            VcpCapabilitiesInfo = VcpCapsWithCodes(0x60),
            ReadValues = MonitorReadFlags.InputSource,
            CurrentInputSource = 0x11,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 5, Setting = "input-source", RawValue = "HDMI-1" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Error.Code);
    }

    [TestMethod]
    public async Task Set_PowerState_ByFriendlyName_ReturnsInvalidDiscreteValue()
    {
        var monitor = new Monitor
        {
            Id = "I",
            MonitorNumber = 9,
            Name = "PowerMon",
            VcpCapabilitiesInfo = VcpCapsWithCodes(0xD6),
            ReadValues = MonitorReadFlags.PowerState,
            CurrentPowerState = 0x01,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 9, Setting = "power-state", RawValue = "On" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.InvalidDiscreteValue, error!.Error.Code);
    }

    // ─── UnsupportedFeature (exit code 4) ────────────────────────────────────
    [TestMethod]
    public async Task Set_Brightness_NotSupported_ReturnsUnsupportedFeature()
    {
        // Monitor with NO brightness capability flag
        var monitor = new Monitor
        {
            Id = "F",
            MonitorNumber = 6,
            Name = "NoBrightnessMon",
            Capabilities = MonitorCapabilities.None,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 6, Setting = "brightness", RawValue = "50" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.UnsupportedFeature, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.UnsupportedFeature, error.Error.ExitCode);
        Assert.AreEqual(CliMessageIds.Unsupported, error.Error.MessageId);
    }

    [TestMethod]
    public async Task Set_Orientation_NoGdiDevice_ReturnsUnsupportedFeature()
    {
        // Monitor with empty GdiDeviceName — orientation cannot be rotated
        var monitor = new Monitor
        {
            Id = "G",
            MonitorNumber = 7,
            Name = "NoGdiMon",
            GdiDeviceName = string.Empty,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 7, Setting = "orientation", RawValue = "90" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.UnsupportedFeature, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.UnsupportedFeature, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_Contrast_NotSupported_ReturnsUnsupportedFeature()
    {
        var snapshot = new List<Monitor> { BrightnessMon() }; // Brightness only, no contrast
        var req = new SetRequest { MonitorNumber = 1, Setting = "contrast", RawValue = "50" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliErrorCodes.UnsupportedFeature, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.UnsupportedFeature, error.Error.ExitCode);
    }

    // ─── HardwareFailure (exit code 5) ────────────────────────────────────────
    [TestMethod]
    public async Task Set_Brightness_HardwareFailure_ReturnsHardwareFailure()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "50" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new FailingManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.HardwareFailure, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.HardwareFailure, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_Contrast_HardwareFailure_MessageFromManager()
    {
        var snapshot = new List<Monitor> { ContrastMon() };
        var req = new SetRequest { MonitorNumber = 2, Setting = "contrast", RawValue = "60" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new FailingManager("DDC write timed out"), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.AreEqual(CliExitCodes.HardwareFailure, error!.Error.ExitCode);
        Assert.AreEqual(CliMessageIds.HardwareFailure, error.Error.MessageId);
        Assert.AreEqual("DDC write timed out", error.Error.Detail);
    }

    // ─── Success paths (exit code 0) ──────────────────────────────────────────
    [TestMethod]
    public async Task Set_Brightness_Success_ReturnsBeforeAfterValues()
    {
        var monitor = BrightnessMon();
        monitor.CurrentBrightness = 30;
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "70" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual("brightness", result!.Setting);
        Assert.AreEqual("30%", result.BeforeDisplay);
        Assert.AreEqual("70%", result.AfterDisplay);
        Assert.AreEqual(1, result.Monitor.Number);
    }

    [TestMethod]
    public async Task Set_Brightness_BoundaryMin_Success()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "0" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.AreEqual("0%", result!.AfterDisplay);
    }

    [TestMethod]
    public async Task Set_Brightness_BoundaryMax_Success()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "100" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.AreEqual("100%", result!.AfterDisplay);
    }

    [TestMethod]
    public async Task Set_Contrast_Success_BeforeAfterDisplay()
    {
        var snapshot = new List<Monitor> { ContrastMon() };
        var req = new SetRequest { MonitorNumber = 2, Setting = "contrast", RawValue = "80" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual("contrast", result!.Setting);
        Assert.AreEqual("55%", result.BeforeDisplay);
        Assert.AreEqual("80%", result.AfterDisplay);
    }

    [TestMethod]
    public async Task Set_Brightness_BeforeUnknown_OmitsBeforeDisplay()
    {
        var monitor = new Monitor
        {
            Id = "A",
            MonitorNumber = 1,
            Name = "TestMon",
            Capabilities = MonitorCapabilities.Brightness,
            ReadValues = MonitorReadFlags.None, // supported but not read → before is unknown
            CurrentBrightness = 0, // default, should not be reported
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "60" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.IsNull(result!.BeforeDisplay);
    }

    [TestMethod]
    public async Task Set_Orientation_Success_BeforeAfterInDegrees()
    {
        var monitor = new Monitor
        {
            Id = "H",
            MonitorNumber = 8,
            Name = "OrientMon",
            GdiDeviceName = @"\\.\DISPLAY8",
            Orientation = 0, // currently 0°
            ReadValues = MonitorReadFlags.Orientation,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 8, Setting = "orientation", RawValue = "90" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual("orientation", result!.Setting);
        Assert.AreEqual("0°", result.BeforeDisplay);
        Assert.AreEqual("90°", result.AfterDisplay);
    }

    [TestMethod]
    public async Task Set_ByMonitorId_Success()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorId = "A", Setting = "brightness", RawValue = "55" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(error);
        Assert.IsNotNull(result);
        Assert.AreEqual("A", result!.Monitor.Id);
    }

    // ─── PowerState confirmation gate ────────────────────────────────────────
    [TestMethod]
    public async Task Set_PowerState_BlankingWithoutConfirm_ReturnsArgumentError()
    {
        var monitor = new Monitor
        {
            Id = "I",
            MonitorNumber = 9,
            Name = "PowerMon",
            VcpCapabilitiesInfo = VcpCapsWithCodes(0xD6), // makes SupportsPowerState == true
            ReadValues = MonitorReadFlags.PowerState,
            CurrentPowerState = 0x01, // On
        };
        var snapshot = new List<Monitor> { monitor };

        // 0x04 = Off (DPM) — a display-blanking state
        var req = new SetRequest { MonitorNumber = 9, Setting = "power-state", RawValue = "0x04", ConfirmPowerOff = false };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
    }

    [TestMethod]
    public async Task Set_PowerState_BlankingWithConfirm_Proceeds()
    {
        var monitor = new Monitor
        {
            Id = "I",
            MonitorNumber = 9,
            Name = "PowerMon",
            VcpCapabilitiesInfo = VcpCapsWithCodes(0xD6), // makes SupportsPowerState == true
            ReadValues = MonitorReadFlags.PowerState,
            CurrentPowerState = 0x01,
        };
        var snapshot = new List<Monitor> { monitor };
        var req = new SetRequest { MonitorNumber = 9, Setting = "power-state", RawValue = "0x04", ConfirmPowerOff = true };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        // No error — the confirmation flag was provided
        Assert.IsNull(error);
        Assert.IsNotNull(result);

        // Pin the discrete success projection (the only discrete-set success in the suite): before/after
        // are formatted via FormatDiscrete(0xD6, …), not the raw int. Self-pin to the product formatter
        // (BeforeDisplay = "On (0x01)", AfterDisplay = "Off (DPM) (0x04)"). Catches a before/after swap
        // or a dropped FormatDiscrete in ApplyDiscreteAsync.
        Assert.AreEqual("power-state", result!.Setting);
        Assert.AreEqual(MonitorDtoProjector.FormatDiscrete(0xD6, 0x01), result.BeforeDisplay);
        Assert.AreEqual(MonitorDtoProjector.FormatDiscrete(0xD6, 0x04), result.AfterDisplay);
    }

    // ─── Unknown setting name ─────────────────────────────────────────────────
    [TestMethod]
    public async Task Set_UnknownSetting_ReturnsArgumentError()
    {
        var snapshot = new List<Monitor> { BrightnessMon() };
        var req = new SetRequest { MonitorNumber = 1, Setting = "flicker-rate", RawValue = "60" };

        var (result, error) = await SetCommandExecutor.ExecuteAsync(new NoOpManager(), snapshot, EmptyHidden, req, default);

        Assert.IsNull(result);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error!.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
    }
}
