// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Contracts;

namespace PowerDisplay.Contracts.UnitTests;

[TestClass]
public class RoundTripTests
{
    [TestMethod]
    public void SetRequest_envelope_round_trips_through_source_gen()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Set,
            Set = new SetRequest { MonitorNumber = 1, Setting = "brightness", RawValue = "50", ConfirmPowerOff = false },
        };

        var json = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliRequestEnvelope);

        Assert.IsNotNull(back);
        Assert.AreEqual(CliCommandNames.Set, back!.Command);
        Assert.AreEqual(1, back.Set!.MonitorNumber);
        Assert.AreEqual("brightness", back.Set.Setting);
        Assert.AreEqual("50", back.Set.RawValue);
    }

    [TestMethod]
    public void GetRequest_envelope_round_trips_inherited_selector_fields()
    {
        // GetRequest/CapabilitiesRequest derive their selector fields from MonitorSelectorRequest;
        // verify source-gen serializes the inherited properties on both payload slots.
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Get,
            Get = new GetRequest { MonitorNumber = 2, MonitorId = "MON2", SettingFilter = "brightness" },
            Capabilities = new CapabilitiesRequest { MonitorNumber = 3, SettingFilter = "input-source" },
        };

        var json = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliRequestEnvelope);

        Assert.IsNotNull(back);
        Assert.AreEqual(2, back!.Get!.MonitorNumber);
        Assert.AreEqual("MON2", back.Get.MonitorId);
        Assert.AreEqual("brightness", back.Get.SettingFilter);
        Assert.AreEqual(3, back.Capabilities!.MonitorNumber);
        Assert.AreEqual("input-source", back.Capabilities.SettingFilter);
    }

    [TestMethod]
    public void ErrorResult_round_trips_and_preserves_exit_code()
    {
        var error = new CliErrorResult
        {
            Command = "set",
            Error = new CliError
            {
                Code = CliErrorCodes.ProviderUnavailable,
                Message = "PowerDisplay is not running.",
                Supported = new List<CliSupportedValue>
                {
                    new CliSupportedValue { Name = "DVI", Vcp = "60" },
                    new CliSupportedValue { Name = "HDMI-1", Vcp = "61" },
                },
            },
            Monitor = new CliMonitorRef { Number = 1, Id = "MON1", Name = "Monitor A", Method = "DDC/CI" },
        };

        var json = JsonSerializer.Serialize(error, ContractsJsonContext.Default.CliErrorResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliErrorResult);

        Assert.IsNotNull(back);
        Assert.AreEqual(CliExitCodes.ProviderUnavailable, back!.Error!.ExitCode);
        Assert.AreEqual("PROVIDER_UNAVAILABLE", back.Error.Code);
        Assert.IsNotNull(back.Error.Supported);
        Assert.AreEqual(2, back.Error.Supported!.Count);
        Assert.AreEqual("DVI", back.Error.Supported[0].Name);
        Assert.AreEqual("60", back.Error.Supported[0].Vcp);
        Assert.AreEqual("HDMI-1", back.Error.Supported[1].Name);

        // Discriminator, schema version, and the optional monitor ref must survive the round trip.
        Assert.IsTrue(back.IsError);
        Assert.AreEqual(CliSchema.Version, back.Version);
        Assert.IsNotNull(back.Monitor);
        Assert.AreEqual("MON1", back.Monitor!.Id);
        Assert.AreEqual("Monitor A", back.Monitor.Name);

        // Wire-format compatibility: ExitCode is now a derived (computed) property, but it MUST
        // still be serialized for external JSON consumers that read error.exitCode.
        StringAssert.Contains(json, "\"exitCode\":10");
    }

    [TestMethod]
    public void ForErrorCode_maps_each_error_code_to_its_matching_exit_code()
    {
        Assert.AreEqual(CliExitCodes.MonitorNotFound, CliExitCodes.ForErrorCode(CliErrorCodes.MonitorNotFound));
        Assert.AreEqual(CliExitCodes.OutOfRange, CliExitCodes.ForErrorCode(CliErrorCodes.OutOfRange));
        Assert.AreEqual(CliExitCodes.InvalidDiscreteValue, CliExitCodes.ForErrorCode(CliErrorCodes.InvalidDiscreteValue));
        Assert.AreEqual(CliExitCodes.UnsupportedFeature, CliExitCodes.ForErrorCode(CliErrorCodes.UnsupportedFeature));
        Assert.AreEqual(CliExitCodes.HardwareFailure, CliExitCodes.ForErrorCode(CliErrorCodes.HardwareFailure));
        Assert.AreEqual(CliExitCodes.SelectorMissing, CliExitCodes.ForErrorCode(CliErrorCodes.SelectorMissing));
        Assert.AreEqual(CliExitCodes.ArgumentError, CliExitCodes.ForErrorCode(CliErrorCodes.ArgumentError));
        Assert.AreEqual(CliExitCodes.Timeout, CliExitCodes.ForErrorCode(CliErrorCodes.Timeout));
        Assert.AreEqual(CliExitCodes.InternalError, CliExitCodes.ForErrorCode(CliErrorCodes.InternalError));
        Assert.AreEqual(CliExitCodes.ProviderUnavailable, CliExitCodes.ForErrorCode(CliErrorCodes.ProviderUnavailable));

        // Unknown code degrades to InternalError; and a CliError's ExitCode tracks its Code.
        Assert.AreEqual(CliExitCodes.InternalError, CliExitCodes.ForErrorCode("NOT_A_REAL_CODE"));
        Assert.AreEqual(CliExitCodes.OutOfRange, new CliError { Code = CliErrorCodes.OutOfRange }.ExitCode);
    }

    [TestMethod]
    public void CliListResult_round_trips_with_nested_monitors()
    {
        var result = new CliListResult
        {
            Monitors = new List<CliMonitorRef>
            {
                new CliMonitorRef
                {
                    Number = 1,
                    Id = "DISPLAY\\DEL0A8C\\4&1a2b3c4d&0&UID12345",
                    Name = "Dell U2722D",
                    Method = "DDC/CI",
                },
            },
        };

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliListResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliListResult);

        Assert.IsNotNull(back);
        Assert.AreEqual("list", back!.Command);
        Assert.AreEqual(1, back.Monitors.Count);
        Assert.AreEqual("Dell U2722D", back.Monitors[0].Name);
        Assert.AreEqual("DDC/CI", back.Monitors[0].Method);
        Assert.IsFalse(back.IsError, "success DTOs carry isError=false");
        Assert.AreEqual(CliSchema.Version, back.Version);
    }

    [TestMethod]
    public void CliGetResult_round_trips_with_nested_settings()
    {
        var result = new CliGetResult
        {
            Monitors = new List<CliGetMonitorEntry>
            {
                new CliGetMonitorEntry
                {
                    Monitor = new CliMonitorRef { Number = 1, Id = "MON1", Name = "Monitor A", Method = "DDC/CI" },
                    Settings = new List<CliSettingValue>
                    {
                        new CliSettingValue { Setting = "brightness", Display = "75%", Supported = true },
                        new CliSettingValue { Setting = "contrast", Display = "50%", Supported = true },
                        new CliSettingValue { Setting = "volume", Supported = false },
                    },
                },
            },
        };

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliGetResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliGetResult);

        Assert.IsNotNull(back);
        Assert.AreEqual("get", back!.Command);
        Assert.AreEqual(1, back.Monitors.Count);
        Assert.AreEqual("MON1", back.Monitors[0].Monitor.Id);
        Assert.AreEqual(3, back.Monitors[0].Settings.Count);
        Assert.AreEqual("75%", back.Monitors[0].Settings[0].Display);
        Assert.IsFalse(back.Monitors[0].Settings[2].Supported);
    }

    [TestMethod]
    public void CliSetResult_round_trips_with_before_after_values()
    {
        var result = new CliSetResult
        {
            Monitor = new CliMonitorRef { Number = 1, Id = "MON1", Name = "Monitor A", Method = "DDC/CI" },
            Setting = "brightness",
            BeforeDisplay = "50%",
            AfterDisplay = "75%",
        };

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliSetResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliSetResult);

        Assert.IsNotNull(back);
        Assert.AreEqual("set", back!.Command);
        Assert.AreEqual("brightness", back.Setting);
        Assert.AreEqual("50%", back.BeforeDisplay);
        Assert.AreEqual("75%", back.AfterDisplay);
        Assert.AreEqual("MON1", back.Monitor.Id);
    }

    [TestMethod]
    public void CliCapabilitiesResult_round_trips_with_vcp_codes()
    {
        var result = new CliCapabilitiesResult
        {
            Monitor = new CliMonitorRef { Number = 1, Id = "MON1", Name = "Monitor A" },
            CommunicationMethod = "DDC/CI",
            RawCapabilities = "(prot(monitor)type(LCD)model(U2722D)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B 0C) 16 18 1A 52 60(01 03 04 0F 11 12) AC AE B6 C0 C6 C8 C9 D6 DF E1 E2 F1 F2 FD)mswhql(1)mccs_ver(2.1))",
            Model = "U2722D",
            MccsVersion = "2.1",
            VcpCodes = new List<CliVcpCodeInfo>
            {
                new CliVcpCodeInfo { Code = "10", Name = "Luminance", Continuous = true },
                new CliVcpCodeInfo { Code = "60", Name = "Input Source", Continuous = false, DiscreteValues = new List<string> { "DP1", "HDMI1" } },
            },
        };

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliCapabilitiesResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliCapabilitiesResult);

        Assert.IsNotNull(back);
        Assert.AreEqual("capabilities", back!.Command);
        Assert.AreEqual("DDC/CI", back.CommunicationMethod);
        Assert.AreEqual(result.RawCapabilities, back.RawCapabilities);
        Assert.AreEqual("U2722D", back.Model);
        Assert.AreEqual("2.1", back.MccsVersion);
        Assert.AreEqual(2, back.VcpCodes.Count);
        Assert.IsTrue(back.VcpCodes[0].Continuous);
        Assert.IsFalse(back.VcpCodes[1].Continuous);
        Assert.IsNotNull(back.VcpCodes[1].DiscreteValues);
        Assert.AreEqual(2, back.VcpCodes[1].DiscreteValues!.Count);
        Assert.AreEqual("DP1", back.VcpCodes[1].DiscreteValues![0]);
    }

    [TestMethod]
    public void CliProfileListResult_round_trips_with_profiles()
    {
        var result = new CliProfileListResult
        {
            Profiles = new List<CliProfileInfo>
            {
                new CliProfileInfo { Name = "Gaming", MonitorCount = 2, LastModified = "2024-01-15T10:30:00Z" },
                new CliProfileInfo { Name = "Work", MonitorCount = 1, LastModified = null },
            },
        };

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliProfileListResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliProfileListResult);

        Assert.IsNotNull(back);
        Assert.AreEqual("profiles", back!.Command);
        Assert.AreEqual(2, back.Profiles.Count);
        Assert.AreEqual("Gaming", back.Profiles[0].Name);
        Assert.AreEqual(2, back.Profiles[0].MonitorCount);
        Assert.AreEqual("2024-01-15T10:30:00Z", back.Profiles[0].LastModified);
        Assert.AreEqual("Work", back.Profiles[1].Name);
        Assert.IsNull(back.Profiles[1].LastModified);
    }

    [TestMethod]
    public void CliApplyProfileResult_round_trips()
    {
        var result = new CliApplyProfileResult { Profile = "Gaming" };

        var json = JsonSerializer.Serialize(result, ContractsJsonContext.Default.CliApplyProfileResult);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliApplyProfileResult);

        Assert.IsNotNull(back);
        Assert.IsFalse(back!.IsError, "apply-profile is a success envelope (isError=false)");
        Assert.AreEqual("apply-profile", back.Command);
        Assert.AreEqual("Gaming", back.Profile);
    }

    [TestMethod]
    public void CapabilitiesRequest_envelope_round_trips_through_source_gen()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Capabilities,
            Capabilities = new CapabilitiesRequest { MonitorNumber = 1, MonitorId = "MON1" },
        };

        var json = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliRequestEnvelope);

        Assert.IsNotNull(back);
        Assert.AreEqual(CliCommandNames.Capabilities, back!.Command);
        Assert.AreEqual(1, back.Capabilities!.MonitorNumber);
        Assert.AreEqual("MON1", back.Capabilities.MonitorId);
    }

    [TestMethod]
    public void ApplyProfileRequest_envelope_round_trips_through_source_gen()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.ApplyProfile,
            ApplyProfile = new ApplyProfileRequest { ProfileId = 7 },
        };

        var json = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliRequestEnvelope);

        Assert.IsNotNull(back);
        Assert.AreEqual(CliCommandNames.ApplyProfile, back!.Command);
        Assert.AreEqual(7, back.ApplyProfile!.ProfileId);
    }

    [TestMethod]
    public void AdjustRequest_envelope_round_trips_through_source_gen()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Up,
            Adjust = new AdjustRequest { MonitorNumber = 2, MonitorId = "MON2", Setting = "brightness", Step = 10 },
        };

        var json = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliRequestEnvelope);

        Assert.IsNotNull(back);
        Assert.AreEqual(CliCommandNames.Up, back!.Command);
        Assert.AreEqual(2, back.Adjust!.MonitorNumber);
        Assert.AreEqual("MON2", back.Adjust.MonitorId);
        Assert.AreEqual("brightness", back.Adjust.Setting);
        Assert.AreEqual(10, back.Adjust.Step);
    }

    [TestMethod]
    public void AdjustRequest_omitted_step_round_trips_as_null()
    {
        var envelope = new CliRequestEnvelope
        {
            Command = CliCommandNames.Down,
            Adjust = new AdjustRequest { MonitorNumber = 1, Setting = "contrast", Step = null },
        };

        var json = JsonSerializer.Serialize(envelope, ContractsJsonContext.Default.CliRequestEnvelope);
        var back = JsonSerializer.Deserialize(json, ContractsJsonContext.Default.CliRequestEnvelope);

        Assert.AreEqual(CliCommandNames.Down, back!.Command);
        Assert.IsNull(back.Adjust!.Step, "omitted --step must serialize/deserialize as null so the app applies the settings default");
    }

    [TestMethod]
    public void ApplyProfileRequest_And_ProfileInfo_And_ApplyResult_RoundTripIds()
    {
        var req = new ApplyProfileRequest { ProfileId = 7 };
        var reqBack = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(req, ContractsJsonContext.Default.ApplyProfileRequest),
            ContractsJsonContext.Default.ApplyProfileRequest);
        Assert.AreEqual(7, reqBack!.ProfileId);

        var info = new CliProfileInfo { Id = 3, Name = "Gaming", MonitorCount = 2 };
        var infoJson = JsonSerializer.Serialize(info, ContractsJsonContext.Default.CliProfileInfo);
        Assert.IsTrue(infoJson.Contains("\"id\":3"));

        var applied = new CliApplyProfileResult { ProfileId = 3, Profile = "Gaming" };
        var appliedBack = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(applied, ContractsJsonContext.Default.CliApplyProfileResult),
            ContractsJsonContext.Default.CliApplyProfileResult);
        Assert.AreEqual(3, appliedBack!.ProfileId);
    }
}
