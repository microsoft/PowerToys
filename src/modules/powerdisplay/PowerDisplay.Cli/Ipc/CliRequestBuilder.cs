// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using PowerDisplay.Cli.Commands;
using PowerDisplay.Contracts;

namespace PowerDisplay.Cli.Ipc;

/// <summary>
/// Maps parsed CLI arguments into a <see cref="CliRequestEnvelope"/> ready for IPC serialization.
/// One static factory method per command. Syntactic validation (exactly one setting, valid setting
/// name) is intentionally NOT performed here — it lives in <see cref="Program"/> before this
/// builder is called.
/// </summary>
public static class CliRequestBuilder
{
    /// <summary>Builds a <c>list</c> request envelope.</summary>
    public static CliRequestEnvelope BuildList() => new()
    {
        Command = CliCommandNames.List,
    };

    /// <summary>Builds a <c>get</c> request envelope.</summary>
    public static CliRequestEnvelope BuildGet(int? monitorNumber, string? monitorId, string? settingFilter) => new()
    {
        Command = CliCommandNames.Get,
        Get = new GetRequest
        {
            MonitorNumber = monitorNumber,
            MonitorId = monitorId,
            SettingFilter = settingFilter,
        },
    };

    /// <summary>Builds a <c>set</c> request envelope from the already-validated inputs.
    /// Exactly one setting field in <paramref name="inputs"/> must be non-null.</summary>
    public static CliRequestEnvelope BuildSet(SetCommandInputs inputs)
    {
        // Derive the canonical setting name and raw value from the first non-null field.
        var (settingName, rawValue) = inputs switch
        {
            { Brightness: { } v } => (CliSettingNames.Brightness, v.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            { Contrast: { } v } => (CliSettingNames.Contrast, v.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            { Volume: { } v } => (CliSettingNames.Volume, v.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            { ColorTemperature: { } v } => (CliSettingNames.ColorTemperature, v),
            { InputSource: { } v } => (CliSettingNames.InputSource, v),
            { PowerState: { } v } => (CliSettingNames.PowerState, v),
            { Orientation: { } v } => (CliSettingNames.Orientation, v),
            _ => throw new System.InvalidOperationException(
                "BuildSet called without any setting; callers must validate CountSelectedSettings == 1 first."),
        };

        return new CliRequestEnvelope
        {
            Command = CliCommandNames.Set,
            Set = new SetRequest
            {
                MonitorNumber = inputs.MonitorNumber,
                MonitorId = inputs.MonitorId,
                Setting = settingName,
                RawValue = rawValue,
                ConfirmPowerOff = inputs.ConfirmPowerOff,
            },
        };
    }

    /// <summary>Builds an <c>up</c>/<c>down</c> request envelope from the already-validated inputs.
    /// Exactly one continuous-setting flag in <paramref name="inputs"/> must be true.
    /// <paramref name="command"/> is the subcommand name (<c>up</c> or <c>down</c>).</summary>
    public static CliRequestEnvelope BuildAdjust(string command, AdjustCommandInputs inputs)
    {
        var settingName = inputs switch
        {
            { Brightness: true } => CliSettingNames.Brightness,
            { Contrast: true } => CliSettingNames.Contrast,
            { Volume: true } => CliSettingNames.Volume,
            _ => throw new System.InvalidOperationException(
                "BuildAdjust called without any setting; callers must validate CountSelectedSettings == 1 first."),
        };

        return new CliRequestEnvelope
        {
            Command = command,
            Adjust = new AdjustRequest
            {
                MonitorNumber = inputs.MonitorNumber,
                MonitorId = inputs.MonitorId,
                Setting = settingName,
                Step = inputs.Step,
            },
        };
    }

    /// <summary>Builds a <c>capabilities</c> request envelope.</summary>
    public static CliRequestEnvelope BuildCapabilities(int? monitorNumber, string? monitorId, string? settingFilter) => new()
    {
        Command = CliCommandNames.Capabilities,
        Capabilities = new CapabilitiesRequest
        {
            MonitorNumber = monitorNumber,
            MonitorId = monitorId,
            SettingFilter = settingFilter,
        },
    };

    /// <summary>Builds a <c>profiles</c> request envelope.</summary>
    public static CliRequestEnvelope BuildProfiles() => new()
    {
        Command = CliCommandNames.Profiles,
    };

    /// <summary>Builds an <c>apply-profile</c> request envelope.</summary>
    public static CliRequestEnvelope BuildApplyProfile(int profileId) => new()
    {
        Command = CliCommandNames.ApplyProfile,
        ApplyProfile = new ApplyProfileRequest { ProfileId = profileId },
    };
}
