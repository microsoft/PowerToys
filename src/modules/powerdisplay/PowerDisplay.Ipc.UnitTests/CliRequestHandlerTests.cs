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
using PowerDisplay.Common.Services;
using PowerDisplay.Contracts;
using PowerDisplay.Ipc;
using PowerDisplay.Models;
using PowerDisplay.ViewModels;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc.UnitTests;

/// <summary>
/// Unit tests for the testable core of <see cref="CliRequestHandler.BuildResponseAsync"/>.
/// Tests drive the internal static <c>BuildResponseAsync</c> directly so that no WinUI
/// <c>DispatcherQueue</c> or real <see cref="MainViewModel"/> is required.
/// </summary>
[TestClass]
public class CliRequestHandlerTests
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
    /// Fake no-op <see cref="IMonitorManager"/> that records set calls without performing hardware writes.
    /// </summary>
    private sealed class FakeManager : IMonitorManager
    {
        public bool FailWrites { get; set; }

        public string FailureMessage { get; set; } = "hardware error";

        public void SetMaxCompatibilityMode(bool enabled)
        {
        }

        public Task<IReadOnlyList<Monitor>> DiscoverMonitorsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Monitor>>(Array.Empty<Monitor>());

        public Task<MonitorOperationResult> SetBrightnessAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        public Task<MonitorOperationResult> SetContrastAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        public Task<MonitorOperationResult> SetVolumeAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        public Task<MonitorOperationResult> SetColorTemperatureAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        public Task<MonitorOperationResult> SetInputSourceAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        public Task<MonitorOperationResult> SetPowerStateAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        public Task<MonitorOperationResult> SetRotationAsync(string id, int v, CancellationToken ct = default)
            => Respond();

        private Task<MonitorOperationResult> Respond()
            => Task.FromResult(FailWrites
                ? MonitorOperationResult.Failure(FailureMessage)
                : MonitorOperationResult.Success());
    }

    /// <summary>
    /// Builds a minimal <see cref="CliRequestEnvelope"/> for the given command.
    /// </summary>
    private static CliRequestEnvelope MakeEnvelope(string command) => new() { Command = command };

    /// <summary>
    /// Calls <c>BuildResponseAsync</c> with a single monitor snapshot and no hidden IDs.
    /// </summary>
    private static Task<string> Dispatch(
        CliRequestEnvelope envelope,
        IReadOnlyList<Monitor>? monitors = null,
        PowerDisplayProfiles? profiles = null,
        Func<string, CancellationToken, Task<IReadOnlyList<ProfileApplyOutcome>?>>? applyProfile = null)
    {
        return CliRequestHandler.BuildResponseAsync(
            envelope,
            monitors ?? new[] { MakeMon() },
            NoHidden,
            new FakeManager(),
            profiles ?? EmptyProfiles,
            applyProfile ?? ((_, _) => Task.FromResult<IReadOnlyList<ProfileApplyOutcome>?>(Array.Empty<ProfileApplyOutcome>())),
            CancellationToken.None);
    }

    // ─── list command ─────────────────────────────────────────────────────────
    [TestMethod]
    public async Task List_ReturnsCliListResult()
    {
        var envelope = MakeEnvelope(CliCommandNames.List);

        var json = await Dispatch(envelope, monitors: new[] { MakeMon(1, "A") });

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliListResult);
        Assert.IsNotNull(result, "should deserialize to CliListResult");
        Assert.IsTrue(result!.Ok);
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
        Assert.IsTrue(result!.Ok);
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
        Assert.IsFalse(error!.Ok);
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
        Assert.IsTrue(result!.Ok);
        Assert.AreEqual("set", result.Command);
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
        Assert.IsFalse(error!.Ok);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
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
        Assert.IsFalse(error!.Ok);
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
        Assert.IsTrue(result!.Ok);
        Assert.AreEqual("capabilities", result.Command);
    }

    // ─── profiles command ─────────────────────────────────────────────────────
    [TestMethod]
    public async Task Profiles_ReturnsCliProfileListResult()
    {
        var profiles = new PowerDisplayProfiles
        {
            Profiles = new List<PowerDisplayProfile>
            {
                new PowerDisplayProfile { Name = "Night", MonitorSettings = new List<ProfileMonitorSetting>() },
            },
        };
        var envelope = MakeEnvelope(CliCommandNames.Profiles);

        var json = await Dispatch(envelope, profiles: profiles);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliProfileListResult);
        Assert.IsNotNull(result, "should deserialize to CliProfileListResult");
        Assert.IsTrue(result!.Ok);
        Assert.AreEqual("profiles", result.Command);
        Assert.AreEqual(1, result.Profiles.Count);
        Assert.AreEqual("Night", result.Profiles[0].Name);
    }

    // ─── apply-profile command ────────────────────────────────────────────────
    [TestMethod]
    public async Task ApplyProfile_FoundProfile_ReturnsCliApplyProfileResult()
    {
        var outcomes = new ProfileApplyOutcome[]
        {
            new ProfileApplyOutcome("A", Connected: true, Changes: Array.Empty<ProfileChangeOutcome>()),
        };
        Func<string, CancellationToken, Task<IReadOnlyList<ProfileApplyOutcome>?>> applyFn =
            (_, _) => Task.FromResult<IReadOnlyList<ProfileApplyOutcome>?>(outcomes);

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileName = "Night" },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var result = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliApplyProfileResult);
        Assert.IsNotNull(result, "should deserialize to CliApplyProfileResult");
        Assert.IsTrue(result!.Ok);
        Assert.AreEqual("apply-profile", result.Command);
        Assert.AreEqual("Night", result.Profile);
    }

    [TestMethod]
    public async Task ApplyProfile_ProfileNotFound_ReturnsArgumentError()
    {
        // null outcomes = profile not found
        Func<string, CancellationToken, Task<IReadOnlyList<ProfileApplyOutcome>?>> applyFn =
            (_, _) => Task.FromResult<IReadOnlyList<ProfileApplyOutcome>?>(null);

        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileName = "NoSuchProfile" },
        };

        var json = await Dispatch(envelope, applyProfile: applyFn);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.IsFalse(error!.Ok);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
        Assert.AreEqual(CliExitCodes.ArgumentError, error.Error.ExitCode);
        Assert.AreEqual("apply-profile", error.Command);
        StringAssert.Contains(error.Error.Message, "NoSuchProfile");
    }

    [TestMethod]
    public async Task ApplyProfile_EmptyProfileName_ReturnsArgumentError()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileName = string.Empty },
        };

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.IsFalse(error!.Ok);
        Assert.AreEqual(CliErrorCodes.ArgumentError, error.Error.Code);
    }

    // ─── unknown command ──────────────────────────────────────────────────────
    [TestMethod]
    public async Task UnknownCommand_ReturnsInternalError()
    {
        var envelope = MakeEnvelope("does-not-exist");

        var json = await Dispatch(envelope);

        var error = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);
        Assert.IsNotNull(error, "should deserialize to CliErrorResult");
        Assert.IsFalse(error!.Ok);
        Assert.AreEqual(CliErrorCodes.InternalError, error.Error.Code);
        Assert.AreEqual(CliExitCodes.InternalError, error.Error.ExitCode);
        Assert.AreEqual("unknown", error.Command);
    }
}
