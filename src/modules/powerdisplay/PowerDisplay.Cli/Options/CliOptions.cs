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

    // --- up/down: no-value setting flags (exactly one) ---
    // These intentionally reuse the same alias strings (--brightness/--contrast/--volume) as the
    // set-command Option<int?> instances above. There is no conflict: each Option instance is added
    // only to its own subcommand (set gets the int? options; up/down get these bool flags), and
    // System.CommandLine scopes alias resolution per command. Do NOT add both variants to one command.
    public static readonly Option<bool> BrightnessFlag = new(
        ["--brightness"],
        "Adjust brightness (no value; the amount comes from --step or the mouse_wheel_increment setting).")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<bool> ContrastFlag = new(
        ["--contrast"],
        "Adjust contrast (no value; the amount comes from --step or the mouse_wheel_increment setting).")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<bool> VolumeFlag = new(
        ["--volume"],
        "Adjust volume (no value; the amount comes from --step or the mouse_wheel_increment setting).")
    {
        Arity = ArgumentArity.ZeroOrOne,
    };

    public static readonly Option<int?> Step = new(
        ["--step"],
        "Amount to raise/lower by. Defaults to the PowerDisplay mouse_wheel_increment setting. Must be >= 0.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    // --- set: discrete ---
    public static readonly Option<string?> ColorTemperature = new(
        ["--color-temperature"],
        "Hex VCP value (e.g. 0x05). Run 'powerdisplay capabilities --setting color-temperature' to list supported values.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> InputSource = new(
        ["--input-source"],
        "Hex VCP value (e.g. 0x11). Run 'powerdisplay capabilities --setting input-source' to list supported values.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> PowerState = new(
        ["--power-state"],
        "Hex VCP value (e.g. 0x01=On, 0x04=Off (DPM)). Run 'powerdisplay capabilities --setting power-state' to list supported values.")
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

        // Reject a negative --step at parse time so it flows through the single ArgumentError
        // envelope (mirrors the --timeout validator). 0 is allowed (a no-op adjust).
        Step.AddValidator(result =>
        {
            if (result.Tokens.Count != 0
                && int.TryParse(result.Tokens[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step)
                && step < 0)
            {
                result.ErrorMessage = Resources.Error_NegativeStep;
            }
        });
    }
}
