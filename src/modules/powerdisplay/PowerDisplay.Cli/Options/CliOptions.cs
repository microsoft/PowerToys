// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.CommandLine;
using System.Globalization;
using PowerDisplay.Cli.Properties;

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
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> MonitorId = new(
        ["--monitor-id", "-i"],
        "Stable monitor ID (DevicePath-derived). Wins if --monitor-number is also provided.")
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
        "Power state name (On, Standby, Suspend, \"Off (DPM)\", \"Off (Hard)\") or hex VCP value.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> Orientation = new(
        ["--orientation"],
        "Rotation in degrees: 0, 90, 180, or 270.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<int?> TimeoutSeconds = new(
        ["--timeout"],
        "Abort the operation after this many seconds (default 30). 0 disables the timeout.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<bool> Quiet = new(
        ["--quiet"],
        "Suppress warning messages on stderr.")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<bool> ConfirmPowerOff = new(
        ["--confirm-power-off"],
        "Required to apply a power-state that powers the display off or puts it to sleep (Standby/Suspend/Off).")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    // --- apply-profile ---
    public static readonly Argument<string> ProfileName = new(
        "name",
        "Name of the profile to apply (case-insensitive). Run 'powerdisplay profiles' to list them.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    static CliOptions()
    {
        // Reject a negative --timeout at parse time so it flows through the single ArgumentError
        // envelope instead of silently disabling the timeout (the `timeoutSeconds > 0` guard in
        // Program would otherwise treat a fat-fingered "-5" like the documented "0 = disabled").
        TimeoutSeconds.AddValidator(result =>
        {
            if (result.Tokens.Count != 0
                && int.TryParse(result.Tokens[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                && seconds < 0)
            {
                result.ErrorMessage = Resources.Error_NegativeTimeout;
            }
        });
    }
}
