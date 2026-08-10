// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Cli.Commands;
using PowerDisplay.Cli.Ipc;
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
    private static IpcDispatcher MakeDispatcher(string? stubResponse, RecordingCliOutput output)
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
        var output = new RecordingCliOutput();
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
        var output = new RecordingCliOutput();
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
        var output = new RecordingCliOutput();
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

        // An error envelope (isError=true) routes through the error renderer (stderr) only and must
        // never leak to the success path (stdout).
        Assert.AreEqual(0, output.StdoutLines.Count, "error envelope must not render via the success path");
    }

    // ── apply-profile always exits 0 (best-effort) ───────────────────────────
    [TestMethod]
    public async Task ApplyProfile_success_exits_0()
    {
        var output = new RecordingCliOutput();
        var responseJson = SerializeSuccess(
            new CliApplyProfileResult { Profile = "Work" },
            ContractsJsonContext.Default.CliApplyProfileResult);
        var dispatcher = MakeDispatcher(responseJson, output);
        var exit = await dispatcher.SendApplyProfileAsync(CliRequestBuilder.BuildApplyProfile(42), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.Ok, exit, "apply-profile is best-effort and always exits 0 once the profile exists");

        // apply-profile is a success envelope (isError=false): it must route through the success
        // renderer (stdout) and never WriteError.
        Assert.AreEqual(1, output.StdoutLines.Count, "rendered via the success path");
        Assert.AreEqual(0, output.StderrLines.Count, "must not go through WriteError");
    }

    // ── schema-mismatch / undeserializable response → InternalError (9) ────────
    [TestMethod]
    public async Task Malformed_json_response_exits_internal_error()
    {
        var output = new RecordingCliOutput();
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
        var output = new RecordingCliOutput();
        var dispatcher = MakeDispatcher("{\"isError\":false,\"monitors\":\"oops\"}", output);
        var exit = await dispatcher.SendListAsync(CliRequestBuilder.BuildList(), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.InternalError, exit);
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
    public void BuildApplyProfile_Maps_ProfileId()
    {
        var envelope = CliRequestBuilder.BuildApplyProfile(7);
        Assert.AreEqual(CliCommandNames.ApplyProfile, envelope.Command);
        Assert.AreEqual(7, envelope.ApplyProfile!.ProfileId);
    }

    // ── BuildAdjust round-trips ──────────────────────────────────────────────
    [TestMethod]
    public void BuildAdjust_Up_Brightness_MapsCommandSettingAndStep()
    {
        var inputs = new AdjustCommandInputs { Brightness = true, Step = 10, MonitorNumber = 2 };
        var envelope = CliRequestBuilder.BuildAdjust(CliCommandNames.Up, inputs);

        Assert.AreEqual(CliCommandNames.Up, envelope.Command);
        Assert.IsNotNull(envelope.Adjust);
        Assert.AreEqual("brightness", envelope.Adjust!.Setting);
        Assert.AreEqual(10, envelope.Adjust.Step);
        Assert.AreEqual(2, envelope.Adjust.MonitorNumber);
    }

    [TestMethod]
    public void BuildAdjust_Down_Contrast_NullStep()
    {
        var inputs = new AdjustCommandInputs { Contrast = true, Step = null };
        var envelope = CliRequestBuilder.BuildAdjust(CliCommandNames.Down, inputs);

        Assert.AreEqual(CliCommandNames.Down, envelope.Command);
        Assert.AreEqual("contrast", envelope.Adjust!.Setting);
        Assert.IsNull(envelope.Adjust.Step);
    }

    [TestMethod]
    public void BuildAdjust_NoSetting_Throws()
    {
        Assert.ThrowsException<InvalidOperationException>(
            () => CliRequestBuilder.BuildAdjust(CliCommandNames.Up, new AdjustCommandInputs()));
    }

    // ── SendAdjustAsync renders via the set renderer, exits 0 ─────────────────
    [TestMethod]
    public async Task Success_adjust_renders_result_exits_0()
    {
        var output = new RecordingCliOutput();
        var responseJson = SerializeSuccess(
            new CliSetResult { Command = "up", Setting = "brightness", Monitor = new CliMonitorRef { Number = 1, Id = "x", Name = "N" }, AfterDisplay = "60%" },
            ContractsJsonContext.Default.CliSetResult);
        var dispatcher = MakeDispatcher(responseJson, output);
        var inputs = new AdjustCommandInputs { Brightness = true, Step = 10 };
        var exit = await dispatcher.SendAdjustAsync(CliRequestBuilder.BuildAdjust(CliCommandNames.Up, inputs), CancellationToken.None);

        Assert.AreEqual(CliExitCodes.Ok, exit);
        Assert.AreEqual(1, output.StdoutLines.Count);
        StringAssert.Contains(output.StdoutLines[0], "brightness");
    }
}
