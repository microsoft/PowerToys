// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Ipc;
using PowerDisplay.Cli.Output;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.UnitTests;

/// <summary>
/// Tests the IPC dispatch path: provider-unavailable (null response) → exit 10,
/// success response → rendered and exit 0, and error response → rendered and
/// correct exit code.
/// </summary>
[TestClass]
public class IpcDispatchTests
{
    private static readonly TimeSpan AnyTimeout = TimeSpan.FromSeconds(30);

    // ── helpers ──────────────────────────────────────────────────────────────
    private sealed class CaptureOutput : ICliOutput, IDisposable
    {
        private readonly List<string> stdoutLines = new();

        private readonly List<string> stderrLines = new();

        private readonly StringWriter stdout = new();

        private readonly StringWriter stderr = new();

        public IReadOnlyList<string> StdoutLines => this.stdoutLines;

        public IReadOnlyList<string> StderrLines => this.stderrLines;

        public void WriteListResult(CliListResult r) => this.stdoutLines.Add("list:" + r.Command);

        public void WriteSetResult(CliSetResult r) => this.stdoutLines.Add("set:" + r.Setting);

        public void WriteGetResult(CliGetResult r) => this.stdoutLines.Add("get");

        public void WriteCapabilitiesResult(CliCapabilitiesResult r) => this.stdoutLines.Add("capabilities");

        public void WriteProfileListResult(CliProfileListResult r) => this.stdoutLines.Add("profiles");

        public void WriteApplyProfileResult(CliApplyProfileResult r) => this.stdoutLines.Add("apply-profile:" + r.ExitCode);

        public void WriteError(CliErrorResult r) => this.stderrLines.Add("error:" + r.Error.Code + ":" + r.Error.ExitCode);

        public void WriteWarning(string message) => this.stderrLines.Add("warn:" + message);

        public void Dispose()
        {
            this.stdout.Dispose();
            this.stderr.Dispose();
        }
    }

    private static IpcDispatcher MakeDispatcher(string? stubResponse, CaptureOutput output)
    {
        Task<string?> StubSend(string requestJson, TimeSpan timeout, CancellationToken cancellationToken) =>
            Task.FromResult(stubResponse);
        return new IpcDispatcher(StubSend, output, AnyTimeout);
    }

    private static string SerializeSuccess<T>(T obj, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Serialize(obj, typeInfo);

    private static string SerializeError(CliErrorResult err)
        => JsonSerializer.Serialize(err, ContractsJsonContext.Default.CliErrorResult);

    // ── ProviderUnavailable (null) ────────────────────────────────────────────
    [TestMethod]
    public async Task When_provider_unavailable_list_exits_10()
    {
        var output = new CaptureOutput();
        var dispatcher = MakeDispatcher(null, output);
        var exit = await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.ProviderUnavailable, exit);
        Assert.AreEqual(1, output.StderrLines.Count);
        StringAssert.Contains(output.StderrLines[0], CliErrorCodes.ProviderUnavailable);
        StringAssert.Contains(output.StderrLines[0], "10");
    }

    // ── Success responses rendered, exit 0 ───────────────────────────────────
    [TestMethod]
    public async Task Success_set_renders_result_exits_0()
    {
        var output = new CaptureOutput();
        var responseJson = SerializeSuccess(
            new CliSetResult { Setting = "brightness", Monitor = new CliMonitorRef { Number = 1, Id = "x", Name = "N" }, AfterDisplay = "80%" },
            ContractsJsonContext.Default.CliSetResult);
        var dispatcher = MakeDispatcher(responseJson, output);
        var inputs = new SetCommandInputs { Brightness = 80 };
        var exit = await dispatcher.SendSetAsync(CliRequestBuilder.BuildSet(inputs), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.Ok, exit);
        Assert.AreEqual(1, output.StdoutLines.Count);
        StringAssert.Contains(output.StdoutLines[0], "brightness");
    }

    // ── Error responses rendered, correct exit code ───────────────────────────
    [TestMethod]
    public async Task Error_response_renders_error_and_returns_its_exit_code()
    {
        var output = new CaptureOutput();
        var errorResponse = new CliErrorResult
        {
            Command = "list",
            Error = new CliError
            {
                Code = CliErrorCodes.MonitorNotFound,
                Message = "Monitor not found.",
            },
        };
        var responseJson = SerializeError(errorResponse);
        var dispatcher = MakeDispatcher(responseJson, output);
        var exit = await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.MonitorNotFound, exit);
        Assert.AreEqual(1, output.StderrLines.Count);
        StringAssert.Contains(output.StderrLines[0], CliErrorCodes.MonitorNotFound);
    }

    // ── apply-profile exit-code carried through IPC ───────────────────────────

    /// <summary>
    /// Verifies that when the app returns a canned CliApplyProfileResult with
    /// ExitCode=2 (OutOfRange), the CLI dispatcher returns exit 2, NOT the old hardcoded 5
    /// (HardwareFailure). This is the regression test for the apply-profile exit-code bug.
    /// </summary>
    [TestMethod]
    public async Task ApplyProfile_OutOfRange_partial_failure_exits_2()
    {
        var output = new CaptureOutput();
        var responseJson = SerializeSuccess(
            new CliApplyProfileResult
            {
                ExitCode = CliExitCodes.OutOfRange,
                Profile = "Night",
                Monitors = new List<CliProfileMonitorOutcome>
                {
                    new CliProfileMonitorOutcome
                    {
                        Monitor = new CliMonitorRef { Number = 1, Id = "MON1", Name = "Monitor A" },
                        Connected = true,
                        Changes = new List<CliProfileChange>
                        {
                            new CliProfileChange { Setting = "brightness", Value = 110, Status = CliProfileChange.StatusOutOfRange },
                        },
                    },
                },
            },
            ContractsJsonContext.Default.CliApplyProfileResult);
        var dispatcher = MakeDispatcher(responseJson, output);
        var exit = await dispatcher.SendApplyProfileAsync(CliRequestBuilder.BuildApplyProfile("Night"), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.OutOfRange, exit, "OutOfRange partial failure must return exit 2, not hardcoded HardwareFailure(5)");
        Assert.AreEqual(1, output.StdoutLines.Count);
    }

    [TestMethod]
    public async Task ApplyProfile_full_success_exits_0()
    {
        var output = new CaptureOutput();
        var responseJson = SerializeSuccess(
            new CliApplyProfileResult
            {
                ExitCode = CliExitCodes.Ok,
                Profile = "Work",
                Monitors = new List<CliProfileMonitorOutcome>(),
            },
            ContractsJsonContext.Default.CliApplyProfileResult);
        var dispatcher = MakeDispatcher(responseJson, output);
        var exit = await dispatcher.SendApplyProfileAsync(CliRequestBuilder.BuildApplyProfile("Work"), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.Ok, exit);
    }

    // ── schema-mismatch / undeserializable response → InternalError (9) ────────
    [TestMethod]
    public async Task Malformed_json_response_exits_internal_error()
    {
        var output = new CaptureOutput();
        var dispatcher = MakeDispatcher("{ this is not valid json", output);
        var exit = await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.InternalError, exit);
        Assert.AreEqual(1, output.StderrLines.Count);
        StringAssert.Contains(output.StderrLines[0], CliErrorCodes.InternalError);
    }

    [TestMethod]
    public async Task Wrong_shape_response_exits_internal_error()
    {
        // Valid JSON with isError:false, but the success payload cannot deserialize as the expected
        // type (monitors is a string, not an array) — the version-skew fallback path.
        var output = new CaptureOutput();
        var dispatcher = MakeDispatcher("{\"isError\":false,\"monitors\":\"oops\"}", output);
        var exit = await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.InternalError, exit);
    }

    // ── IsError discriminator routing ─────────────────────────────────────────
    [TestMethod]
    public async Task ApplyProfile_partial_failure_isError_false_routes_to_success_path()
    {
        // A partial-failure apply-profile result is a SUCCESS envelope (isError=false) carrying a
        // non-zero ExitCode. The dispatcher must route it through the success renderer and return
        // ExitCode — never the error path — purely on the explicit discriminator.
        var result = new CliApplyProfileResult { ExitCode = CliExitCodes.OutOfRange, Profile = "Night", Monitors = [] };
        Assert.IsFalse(result.IsError, "apply-profile result must not be flagged as an error envelope");

        var output = new CaptureOutput();
        var responseJson = SerializeSuccess(result, ContractsJsonContext.Default.CliApplyProfileResult);
        var dispatcher = MakeDispatcher(responseJson, output);
        var exit = await dispatcher.SendApplyProfileAsync(CliRequestBuilder.BuildApplyProfile("Night"), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.OutOfRange, exit);
        Assert.AreEqual(1, output.StdoutLines.Count, "rendered via the success path");
        Assert.AreEqual(0, output.StderrLines.Count, "must not go through WriteError");
    }

    [TestMethod]
    public async Task Error_envelope_isError_true_routes_to_error_path()
    {
        var err = new CliErrorResult
        {
            Command = "list",
            Error = new CliError { Code = CliErrorCodes.InternalError, Message = "boom" },
        };
        Assert.IsTrue(err.IsError, "an error envelope must be flagged with isError=true");

        var output = new CaptureOutput();
        var dispatcher = MakeDispatcher(SerializeError(err), output);
        var exit = await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.InternalError, exit);
        Assert.AreEqual(1, output.StderrLines.Count);
        Assert.AreEqual(0, output.StdoutLines.Count);
    }

    // ── CliRequestBuilder round-trips ────────────────────────────────────────
    [TestMethod]
    public void BuildSet_Brightness_MapsCorrectly()
    {
        var inputs = new SetCommandInputs { Brightness = 75, MonitorNumber = 2 };
        var envelope = CliRequestBuilder.BuildSet(inputs);

        Assert.AreEqual(CliCommandNames.Set, envelope.Command);
        Assert.IsNotNull(envelope.Set);
        Assert.AreEqual("brightness", envelope.Set!.Setting);
        Assert.AreEqual("75", envelope.Set.RawValue);
        Assert.AreEqual(2, envelope.Set.MonitorNumber);
    }

    [TestMethod]
    public void BuildSet_PowerState_MapsCorrectly()
    {
        var inputs = new SetCommandInputs { PowerState = "Standby", ConfirmPowerOff = true };
        var envelope = CliRequestBuilder.BuildSet(inputs);

        Assert.AreEqual("power-state", envelope.Set!.Setting);
        Assert.AreEqual("Standby", envelope.Set.RawValue);
        Assert.IsTrue(envelope.Set.ConfirmPowerOff);
    }

    [TestMethod]
    public void BuildSet_NoSetting_Throws()
    {
        var inputs = new SetCommandInputs();
        Assert.ThrowsException<InvalidOperationException>(() => CliRequestBuilder.BuildSet(inputs));
    }

    [TestMethod]
    public void BuildGet_Maps_MonitorSelectors_And_Filter()
    {
        var envelope = CliRequestBuilder.BuildGet(3, "myId", "brightness");
        Assert.AreEqual(CliCommandNames.Get, envelope.Command);
        Assert.AreEqual(3, envelope.Get!.MonitorNumber);
        Assert.AreEqual("myId", envelope.Get.MonitorId);
        Assert.AreEqual("brightness", envelope.Get.SettingFilter);
    }

    [TestMethod]
    public void BuildApplyProfile_Maps_ProfileName()
    {
        var envelope = CliRequestBuilder.BuildApplyProfile("Night");
        Assert.AreEqual(CliCommandNames.ApplyProfile, envelope.Command);
        Assert.AreEqual("Night", envelope.ApplyProfile!.ProfileName);
    }
}
