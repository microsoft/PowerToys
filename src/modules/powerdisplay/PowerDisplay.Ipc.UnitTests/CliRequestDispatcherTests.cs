// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Models;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Unit tests for <see cref="CliRequestDispatcher.BuildResponseAsync"/>.
/// Tests drive the dispatcher directly, without the WinUI host adapter.
/// </summary>
[TestClass]
public class CliRequestDispatcherTests
{
    // ─── Shared fixtures ──────────────────────────────────────────────────────
    private static readonly IReadOnlySet<string> NoHidden =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly PowerDisplayProfiles EmptyProfiles =
        new PowerDisplayProfiles { Profiles = new List<PowerDisplayProfile>() };

    private static Monitor MakeMon(int number = 1, string id = "A", string name = "Mon A")
        => new()
        {
            Id = id,
            MonitorNumber = number,
            Name = name,
            CommunicationMethod = "DDC/CI",
            GdiDeviceName = @"\\.\DISPLAY1",
            Capabilities = MonitorCapabilities.Brightness,
            ReadValues = MonitorReadFlags.Brightness,
            CurrentBrightness = 50,
        };

    /// <summary>
    /// Builds a minimal <see cref="CliRequestEnvelope"/> for the given command.
    /// </summary>
    private static CliRequestEnvelope MakeEnvelope(string command) => new() { Command = command };

    /// <summary>
    /// Calls <c>BuildResponseAsync</c> with the int-based apply-profile delegate signature.
    /// The delegate returns the resolved profile's name (<see langword="null"/> means "not found"),
    /// so the apply-profile handler never needs to fall back to <c>LoadProfiles</c>.
    /// </summary>
    private static Task<string> Dispatch(
        CliRequestEnvelope envelope,
        IReadOnlyList<Monitor>? monitors = null,
        PowerDisplayProfiles? profiles = null,
        Func<int, CancellationToken, Task<string?>>? applyProfile = null,
        int defaultStep = 5,
        Func<CancellationToken, Task<PowerDisplayProfiles>>? loadProfilesAsync = null,
        IReadOnlyList<CustomVcpValueMapping>? customMappings = null,
        CancellationToken ct = default)
    {
        return CliRequestDispatcher.BuildResponseAsync(
            envelope,
            monitors ?? new[] { MakeMon() },
            NoHidden,
            customMappings ?? Array.Empty<CustomVcpValueMapping>(),
            new NoOpManager(),
            defaultStep,
            loadProfilesAsync ?? (_ => Task.FromResult(profiles ?? EmptyProfiles)),
            applyProfile ?? ((_, _) => Task.FromResult<string?>("Default")),
            ct);
    }

    // ─── list command ─────────────────────────────────────────────────────────
    [TestMethod]
    public async Task List_ReturnsCliListResult()
    {
        var envelope = MakeEnvelope(CliCommandNames.List);

        var json = await Dispatch(envelope, monitors: new[] { MakeMon(1, "A") });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliListResult);
        Assert.IsNotNull(result, "should deserialize to CliListResult");
        Assert.AreEqual("list", result.Command);
        Assert.AreEqual(1, result.Monitors.Count);
    }

    // ─── get command ──────────────────────────────────────────────────────────
    [TestMethod]
    public async Task Get_NoSelector_ReturnsAllMonitors()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Get,
            Get = new GetRequest { MonitorNumber = null, MonitorId = null },
        };

        var json = await Dispatch(envelope, monitors: new[] { MakeMon(1, "A"), MakeMon(2, "B") });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliGetResult);
        Assert.IsNotNull(result, "should deserialize to CliGetResult");
        Assert.AreEqual("get", result.Command);
        Assert.AreEqual(2, result.Monitors.Count);
    }

    [TestMethod]
    public async Task Get_UnknownMonitorNumber_ReturnsErrorResult()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Get,
            Get = new GetRequest { MonitorNumber = 99 },
        };

        var json = await Dispatch(envelope, monitors: new[] { MakeMon(1, "A") });

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.MonitorNotFound, error.Error.Code);
        Assert.AreEqual(CliExitCodes.MonitorNotFound, error.Error.ExitCode);
    }

    // ─── set command ──────────────────────────────────────────────────────────
    [TestMethod]
    public async Task Set_ValidBrightness_ReturnsCliSetResult()
    {
        var mon = MakeMon(1, "A");
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Set,
            Set = new SetRequest
            {
                MonitorNumber = 1,
                Setting = "brightness",
                RawValue = "75",
            },
        };

        var json = await Dispatch(envelope, monitors: new[] { mon });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliSetResult);
        Assert.IsNotNull(result, "should deserialize to CliSetResult");
        Assert.AreEqual("set", result.Command);

        // Pin the dispatch wiring end-to-end: MakeMon's CurrentBrightness=50 → BeforeDisplay; the
        // requested 75 → AfterDisplay. (Command alone is satisfied by the DTO default and carries no
        // signal.) A handler that passed a wrong value to the executor, or swapped before/after, fails here.
        Assert.AreEqual("50%", result.BeforeDisplay);
        Assert.AreEqual("75%", result.AfterDisplay);
    }

    [TestMethod]
    public async Task Set_DiscreteValue_UsesMonitorSpecificCustomName()
    {
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source", new List<int> { 0x11, 0x12 });
        var mon = MakeMon(1, "A");
        mon.VcpCapabilitiesInfo = caps;
        mon.ReadValues = MonitorReadFlags.InputSource;
        mon.CurrentInputSource = 0x11;

        var mappings = new List<CustomVcpValueMapping>
        {
            new() { VcpCode = 0x60, Value = 0x11, CustomName = "Work laptop", ApplyToAll = false, TargetMonitorId = "A" },
            new() { VcpCode = 0x60, Value = 0x12, CustomName = "Game console", ApplyToAll = false, TargetMonitorId = "A" },
            new() { VcpCode = 0x60, Value = 0x12, CustomName = "Wrong monitor", ApplyToAll = false, TargetMonitorId = "B" },
        };
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Set,
            Set = new SetRequest { MonitorNumber = 1, Setting = CliSettingNames.InputSource, RawValue = "0x12" },
        };

        var json = await Dispatch(envelope, monitors: new[] { mon }, customMappings: mappings);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliSetResult);
        Assert.IsNotNull(result);
        Assert.AreEqual("Work laptop (0x11)", result.BeforeDisplay);
        Assert.AreEqual("Game console (0x12)", result.AfterDisplay);
    }

    [TestMethod]
    public async Task Set_MissingPayload_ReturnsErrorResult()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Set,
            Set = null,
        };

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
    }

    [TestMethod]
    public async Task Set_ServerTokenCancelled_ReturnsTimeoutError()
    {
        // This test injects a cancelled server-side token directly into BuildResponseAsync so the
        // handler observes server-lifetime cancellation and maps it to TIMEOUT (exit 8).
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Set,
            Set = new SetRequest
            {
                MonitorNumber = 1,
                Setting = "brightness",
                RawValue = "75",
            },
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var json = await Dispatch(envelope, monitors: new[] { MakeMon() }, ct: cts.Token);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.Timeout, error.Error.Code);
        Assert.AreEqual(CliExitCodes.Timeout, error.Error.ExitCode);
    }

    // ─── capabilities command ─────────────────────────────────────────────────
    [TestMethod]
    public async Task Capabilities_SelectorMissing_ReturnsErrorResult()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Capabilities,
            Capabilities = new CapabilitiesRequest { MonitorNumber = null, MonitorId = null },
        };

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.SelectorMissing, error.Error.Code);
    }

    [TestMethod]
    public async Task Capabilities_ValidSelector_ReturnsCliCapabilitiesResult()
    {
        var mon = MakeMon(1, "A");
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Capabilities,
            Capabilities = new CapabilitiesRequest { MonitorNumber = 1 },
        };

        var json = await Dispatch(envelope, monitors: new[] { mon });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliCapabilitiesResult);
        Assert.IsNotNull(result, "should deserialize to CliCapabilitiesResult");
        Assert.AreEqual("capabilities", result.Command);

        // Confirm the dispatch path resolved the right monitor and carried the top-level transport,
        // not just a typed-but-empty envelope: MakeMon(1, "A") is monitor #1 on DDC/CI.
        Assert.AreEqual(1, result.Monitor.Number);
        Assert.AreEqual("DDC/CI", result.CommunicationMethod);
    }

    [TestMethod]
    public async Task Capabilities_WithSettingFilter_ReturnsOnlyMatchingCode()
    {
        var mon = MakeMon(1, "A");
        var caps = new VcpCapabilities();
        caps.SupportedVcpCodes[0x14] = new VcpCodeInfo(0x14, "Color Preset", new List<int> { 0x05 });
        caps.SupportedVcpCodes[0x60] = new VcpCodeInfo(0x60, "Input Source", new List<int> { 0x11 });
        mon.VcpCapabilitiesInfo = caps;
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Capabilities,
            Capabilities = new CapabilitiesRequest { MonitorNumber = 1, SettingFilter = "input-source" },
        };

        var json = await Dispatch(envelope, monitors: new[] { mon });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliCapabilitiesResult);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result!.VcpCodes.Count);
        Assert.AreEqual("0x60", result.VcpCodes[0].Code);
    }

    // ─── profiles command ─────────────────────────────────────────────────────
    [TestMethod]
    public async Task Profiles_ReturnsCliProfileListResult()
    {
        var profiles = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile { Name = "Night", MonitorSettings = new List<ProfileMonitorSetting>(), Id = 1 },
            },
        };
        var envelope = MakeEnvelope(CliCommandNames.Profiles);

        var json = await Dispatch(envelope, profiles: profiles);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliProfileListResult);
        Assert.IsNotNull(result, "should deserialize to CliProfileListResult");
        Assert.AreEqual("profiles", result.Command);
        Assert.AreEqual(1, result.Profiles.Count);
        Assert.AreEqual("Night", result.Profiles[0].Name);
    }

    [TestMethod]
    public async Task Profiles_HidesProfilesWithoutAssignedId()
    {
        var profiles = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile { Name = "Legacy", MonitorSettings = new List<ProfileMonitorSetting>() },
                new PowerDisplayProfile { Name = "Assigned", MonitorSettings = new List<ProfileMonitorSetting>(), Id = 2 },
            },
        };

        var json = await Dispatch(MakeEnvelope(CliCommandNames.Profiles), profiles: profiles);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliProfileListResult);
        Assert.IsNotNull(result);
        Assert.AreEqual(1, result.Profiles.Count);
        Assert.AreEqual(2, result.Profiles[0].Id);
    }

    // ─── apply-profile command ────────────────────────────────────────────────
    [TestMethod]
    public async Task ApplyProfile_FoundProfile_ReturnsCliApplyProfileResult()
    {
        // The apply delegate returns the resolved name directly; the handler must use it as-is.
        Func<int, CancellationToken, Task<string?>> applyFn = (id, ct) => Task.FromResult<string?>("Night");

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 1 },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliApplyProfileResult);
        Assert.IsNotNull(result, "should deserialize to CliApplyProfileResult");
        Assert.AreEqual("apply-profile", result.Command);
        Assert.AreEqual(1, result.ProfileId);
        Assert.AreEqual("Night", result.Profile);
    }

    [TestMethod]
    public async Task ApplyProfile_FoundProfile_NeverCallsLoadProfiles()
    {
        // Regression test for the duplicate-load bug: once the apply delegate has resolved and
        // applied the profile, the handler must use the returned name directly and must never call
        // LoadProfilesAsync again to "recover" the name (extra disk I/O, and can report a
        // stale/renamed/deleted name if the profile changed between the two loads).
        var loadProfilesCallCount = 0;
        Func<CancellationToken, Task<PowerDisplayProfiles>> loadProfilesAsync = _ =>
        {
            loadProfilesCallCount++;
            throw new InvalidOperationException("LoadProfilesAsync must not be called by apply-profile");
        };

        Func<int, CancellationToken, Task<string?>> applyFn = (id, ct) => Task.FromResult<string?>("Night");

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 1 },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn, loadProfilesAsync: loadProfilesAsync);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliApplyProfileResult);
        Assert.IsNotNull(result, "should deserialize to CliApplyProfileResult");
        Assert.AreEqual("Night", result.Profile);
        Assert.AreEqual(0, loadProfilesCallCount, "apply-profile must not call LoadProfilesAsync");
    }

    [TestMethod]
    public async Task ApplyProfile_ProfileNotFound_ReturnsArgumentError()
    {
        // null = profile not found (unknown/invalid id)
        Func<int, CancellationToken, Task<string?>> applyFn = (id, ct) => Task.FromResult<string?>(null);

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 99 },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
        Assert.AreEqual("apply-profile", error.Command);
        Assert.AreEqual(CliMessageIds.ProfileNotFound, error.Error.MessageId);
        Assert.AreEqual("99", error.Error.Value);
    }

    [TestMethod]
    public async Task ApplyProfile_EmptyProfileName_ReturnsArgumentError()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 0 },
        };

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
    }

    // ─── apply-profile by id ──────────────────────────────────────────────────
    [TestMethod]
    public async Task ApplyProfile_UnknownId_ReturnsArgumentError()
    {
        Func<int, CancellationToken, Task<string?>> applyFn = (id, ct) => Task.FromResult<string?>(null);

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 99 },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
        Assert.AreEqual(CliMessageIds.ProfileNotFound, error.Error.MessageId);
        Assert.AreEqual("99", error.Error.Value);
    }

    [TestMethod]
    public async Task ApplyProfile_NonPositiveId_ReturnsArgumentError()
    {
        Func<int, CancellationToken, Task<string?>> applyFn = (id, ct) => Task.FromResult<string?>("Should not be called");

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 0 },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
    }

    [TestMethod]
    public async Task ApplyProfile_FoundId_ReturnsSuccessWithId()
    {
        Func<int, CancellationToken, Task<string?>> applyFn = (id, ct) => Task.FromResult<string?>("Gaming");

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 3 },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliApplyProfileResult);
        Assert.IsNotNull(result, "should deserialize to CliApplyProfileResult");
        Assert.AreEqual("apply-profile", result.Command);
        Assert.AreEqual(3, result.ProfileId);
        Assert.AreEqual("Gaming", result.Profile);
    }

    // ─── up / down commands ───────────────────────────────────────────────────
    [TestMethod]
    public async Task Up_Brightness_ReturnsCliSetResult_WithIncrementedValue()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Up,
            Adjust = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = 10 },
        };

        var json = await Dispatch(envelope, monitors: new[] { MakeMon() });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliSetResult);
        Assert.IsNotNull(result, "should deserialize to CliSetResult");
        Assert.AreEqual("up", result!.Command);
        Assert.AreEqual("60%", result.AfterDisplay);
    }

    [TestMethod]
    public async Task Down_NullStep_UsesDefaultStep()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Down,
            Adjust = new AdjustRequest { MonitorNumber = 1, Setting = "brightness", Step = null },
        };

        var json = await Dispatch(envelope, monitors: new[] { MakeMon() }, defaultStep: 5);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliSetResult);
        Assert.IsNotNull(result);
        Assert.AreEqual("45%", result!.AfterDisplay, "50 - default step 5 = 45");
    }

    [TestMethod]
    public async Task Up_MissingAdjustPayload_ReturnsArgumentError()
    {
        var envelope = new CliRequestEnvelope { Command = CliCommandNames.Up, Adjust = null };

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error!.Error.Code);
    }

    // ─── unknown command ──────────────────────────────────────────────────────
    [TestMethod]
    public async Task UnknownCommand_ReturnsArgumentError()
    {
        // A command name the app does not recognize (e.g. a newer CLI talking to an older app) is a
        // bad argument, not an internal fault: it maps to ARGUMENT_ERROR (exit 7), not INTERNAL_ERROR
        // (exit 9). The offending command name is echoed back in the Command field.
        var envelope = MakeEnvelope("does-not-exist");

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
        Assert.AreEqual("does-not-exist", error.Command);
    }
}
