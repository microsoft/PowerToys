// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;

namespace PowerDisplay.Cli.Options;

/// <summary>
/// Shared option instances. Same <see cref="Option{T}"/> instance is reused across
/// subcommands so <c>parseResult.GetValueForOption</c> in dispatch code can rely on
/// reference identity.
/// </summary>
public static class CliOptions
{
    public static readonly Option<int?> MonitorNumber = new(
        ["--monitor-number", "-n"],
        "Index of the monitor (1-based). Run 'powerdisplay list' to discover.")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string?> MonitorId = new(
        ["--monitor-id", "-i"],
        "Stable monitor ID (DevicePath-derived). Wins if --monitor-number is also provided.")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<bool> Json = new(
        ["--json"],
        "Emit machine-readable JSON instead of human-readable text.")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<string?> SettingFilter = new(
        ["--setting"],
        "Restrict 'get' to a single setting name (e.g. brightness, input-source).")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    // --- set: continuous ---
    public static readonly Option<int?> Brightness = new(
        ["--brightness"],
        "Brightness percentage in [0, 100].")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<int?> Contrast = new(
        ["--contrast"],
        "Contrast percentage in [0, 100].")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<int?> Volume = new(
        ["--volume"],
        "Volume percentage in [0, 100].")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    // --- set: discrete ---
    public static readonly Option<string?> ColorTemperature = new(
        ["--color-temperature"],
        "Color preset name (e.g. 6500K, sRGB) or hex VCP value (e.g. 0x05).")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> InputSource = new(
        ["--input-source"],
        "Input source name (e.g. HDMI-1, USB-C) or hex VCP value (e.g. 0x11).")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> PowerState = new(
        ["--power-state"],
        "Power state name (On, Standby, Suspend, Off-DPM, Off-Hard) or hex VCP value.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> Orientation = new(
        ["--orientation"],
        "Rotation in degrees: 0, 90, 180, or 270.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };
}
