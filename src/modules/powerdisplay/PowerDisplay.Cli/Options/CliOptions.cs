// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
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
    public static readonly Option<int[]> MonitorNumber = new(
        new[] { "--monitor-number", "-n" },
        ParseMonitorNumbers,
        isDefault: false,
        description: "Index(es) of the monitor(s) (1-based), comma-separated for multiple (e.g. 1,2,3). Multiple monitors are applied together by set/up/down. Run 'PowerToys.PowerDisplay.Cli.exe list' to discover.")
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
    //
    // Arity is Zero (a pure presence flag), not ZeroOrOne: ZeroOrOne lets the option greedily swallow
    // a following bareword, so `up --brightness false` would bind "false" as the flag value and then
    // report "no setting specified" — contradicting the documented "no value" contract. Zero rejects
    // any attached value while `up --brightness` still resolves to true.
    public static readonly Option<bool> BrightnessFlag = new(
        ["--brightness"],
        "Adjust brightness (no value; the amount comes from --step or the mouse_wheel_increment setting).")
    {
        Arity = ArgumentArity.Zero,
    };

    public static readonly Option<bool> ContrastFlag = new(
        ["--contrast"],
        "Adjust contrast (no value; the amount comes from --step or the mouse_wheel_increment setting).")
    {
        Arity = ArgumentArity.Zero,
    };

    public static readonly Option<bool> VolumeFlag = new(
        ["--volume"],
        "Adjust volume (no value; the amount comes from --step or the mouse_wheel_increment setting).")
    {
        Arity = ArgumentArity.Zero,
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
        "Hex VCP value (e.g. 0x05). Run 'PowerToys.PowerDisplay.Cli.exe capabilities --setting color-temperature' to list supported values.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> InputSource = new(
        ["--input-source"],
        "Hex VCP value (e.g. 0x11). Run 'PowerToys.PowerDisplay.Cli.exe capabilities --setting input-source' to list supported values.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> PowerState = new(
        ["--power-state"],
        "Hex VCP value (e.g. 0x01=On, 0x04=Off (DPM)). Run 'PowerToys.PowerDisplay.Cli.exe capabilities --setting power-state' to list supported values.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    public static readonly Option<string?> Orientation = new(
        ["--orientation"],
        "Rotation in degrees: 0, 90, 180, or 270.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    // Arity is Zero (a pure presence flag), not ZeroOrOne: a ZeroOrOne bool greedily swallows a
    // following bareword that parses as a bool. Since --quiet is a global option, `apply-profile
    // --quiet true` would otherwise bind "true" as the flag value and leave apply-profile with no
    // name (a misleading "Required argument missing"), so a profile literally named "true"/"false"
    // could not be applied. Zero rejects any attached value while a bare --quiet still resolves to
    // true. Mirrors the up/down setting flags above.
    public static readonly Option<bool> Quiet = new(
        ["--quiet"],
        "Suppress warning messages on stderr.")
    {
        Arity = ArgumentArity.Zero,
    };

    // Arity is Zero (a pure presence flag), not ZeroOrOne: same greedy-swallow reasoning as --quiet
    // and the up/down setting flags. A bare --confirm-power-off resolves to true.
    public static readonly Option<bool> ConfirmPowerOff = new(
        ["--confirm-power-off"],
        "Required to apply a power-state that powers the display off or puts it to sleep (Standby/Suspend/Off).")
    {
        Arity = ArgumentArity.Zero,
    };

    // --- apply-profile ---
    public static readonly Argument<int> ProfileId = new(
        "id",
        "Numeric id of the profile to apply. Run 'PowerToys.PowerDisplay.Cli.exe profiles' to list them.")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    static CliOptions()
    {
        // Reject a negative --step at parse time so it flows through the single ArgumentError
        // envelope instead of an unfriendly framework message. 0 is allowed (a no-op adjust).
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

    // Parses the single --monitor-number/-n token as a comma-separated list of 1-based indices
    // (e.g. "1,2,3"). Duplicates are collapsed preserving first-seen order; any empty, non-integer,
    // or non-positive element flows through the single ArgumentError envelope like other parse failures.
    // Returns an empty array only when the option is absent.
    private static int[] ParseMonitorNumbers(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return Array.Empty<int>();
        }

        var raw = result.Tokens[0].Value;
        var parts = raw.Split(',');
        var numbers = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0
                || !int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                || number < 1)
            {
                result.ErrorMessage = Resources.Error_InvalidMonitorNumber(raw);
                return Array.Empty<int>();
            }

            if (!numbers.Contains(number))
            {
                numbers.Add(number);
            }
        }

        return numbers.ToArray();
    }
}
