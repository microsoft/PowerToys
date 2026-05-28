// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Cli.Resolution;

/// <summary>
/// Parses a user-supplied discrete VCP value — either a friendly name (e.g.
/// <c>HDMI-1</c>, <c>6500K</c>, <c>On</c>) or a hex literal (<c>0x11</c>) — into the
/// raw VCP integer for a given VCP code, then verifies that the resolved value
/// appears in the monitor's supported set. Errors are structured so the caller
/// can echo the supported list back to the user.
/// </summary>
public static class DiscreteValueResolver
{
    public static int? TryResolve(
        byte vcpCode,
        string settingName,
        string raw,
        IReadOnlyList<int>? supportedValues,
        out CliError? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = MakeParseError(settingName, raw, supportedValues, vcpCode);
            return null;
        }

        int? parsedValue = TryParseHex(raw) ?? TryParseFriendlyName(vcpCode, raw);

        if (parsedValue is null)
        {
            error = MakeParseError(settingName, raw, supportedValues, vcpCode);
            return null;
        }

        // If the monitor reports a supported-value set, the resolved value must be in it.
        // If it does not (set is null/empty), the VCP code is treated as supporting any
        // value the user can specify (the controller-level write will surface a
        // HARDWARE_FAILURE if the device rejects the value).
        if (supportedValues is { Count: > 0 } && !Contains(supportedValues, parsedValue.Value))
        {
            error = MakeUnsupportedError(settingName, raw, supportedValues, vcpCode);
            return null;
        }

        return parsedValue;
    }

    private static int? TryParseHex(string raw)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("0X", StringComparison.Ordinal))
        {
            if (int.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            {
                return hex;
            }
        }

        return null;
    }

    private static int? TryParseFriendlyName(byte vcpCode, string raw)
    {
        // Reverse-lookup against the static VcpNames table. Comparisons are
        // case-insensitive so users can pass "hdmi-1", "HDMI-1", "Hdmi-1" alike.
        for (int value = 0; value <= 0xFF; value++)
        {
            var name = VcpNames.GetValueName(vcpCode, value);
            if (name is not null && string.Equals(name, raw, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static bool Contains(IReadOnlyList<int> values, int needle)
    {
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] == needle)
            {
                return true;
            }
        }

        return false;
    }

    private static CliError MakeParseError(
        string settingName,
        string raw,
        IReadOnlyList<int>? supportedValues,
        byte vcpCode)
        => new()
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            ExitCode = CliExitCodes.InvalidDiscreteValue,
            Setting = settingName,
            Requested = raw,
            Supported = BuildSupportedList(vcpCode, supportedValues),
            Message = $"--{settingName} value '{raw}' could not be parsed as a name or hex value",
            Hint = "pass a name from the supported list above, or a raw hex value like 0x11",
        };

    private static CliError MakeUnsupportedError(
        string settingName,
        string raw,
        IReadOnlyList<int> supportedValues,
        byte vcpCode)
        => new()
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            ExitCode = CliExitCodes.InvalidDiscreteValue,
            Setting = settingName,
            Requested = raw,
            Supported = BuildSupportedList(vcpCode, supportedValues),
            Message = $"--{settingName} value '{raw}' is not in the monitor's supported set",
            Hint = "pass a name from the supported list above, or a raw hex value like 0x11",
        };

    private static IReadOnlyList<CliSupportedValue>? BuildSupportedList(byte vcpCode, IReadOnlyList<int>? values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var list = new List<CliSupportedValue>(values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            var v = values[i];
            list.Add(new CliSupportedValue
            {
                Name = VcpNames.GetValueName(vcpCode, v) ?? $"0x{v:X2}",
                Vcp = $"0x{v:X2}",
            });
        }

        return list;
    }
}
