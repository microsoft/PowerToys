// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using PowerDisplay.Common.Utils;
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// A discrete VCP setting (color-temperature, input-source, power-state). The <c>set</c> value is a
/// hex literal (<c>0x??</c>) that must be present in the monitor's advertised supported set; the
/// display string is the "Name (0xNN)" form.
/// <para>
/// Friendly names are intentionally NOT accepted: the generic VCP name table can disagree with a
/// specific monitor's value mapping, so the CLI requires an unambiguous hex value (use
/// <c>capabilities --setting &lt;name&gt;</c> to discover them).
/// </para>
/// </summary>
internal sealed class DiscreteVcpSetting : CliVcpSetting
{
    private readonly Func<Monitor, IReadOnlyList<int>?> _supportedValues;

    public DiscreteVcpSetting(
        string name,
        byte vcpCode,
        MonitorReadFlags readFlag,
        Func<Monitor, bool> supports,
        Func<Monitor, int> current,
        Func<Monitor, IReadOnlyList<int>?> supportedValues,
        Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        string unsupportedReason,
        bool blanksDisplay = false)
        : base(name, vcpCode, readFlag, supports, current, apply, unsupportedReason, blanksDisplay)
    {
        _supportedValues = supportedValues;
    }

    public override CliSettingKind Kind => CliSettingKind.Discrete;

    public override IReadOnlyList<int>? SupportedValues(Monitor monitor) => _supportedValues(monitor);

    /// <summary>
    /// Resolves a discrete VCP value from a hex literal (<c>0x??</c>), then verifies it against the
    /// monitor's supported set.
    /// </summary>
    public override (int? Value, CliError? Error) ParseSetValue(string rawValue, Monitor monitor)
    {
        var supportedValues = _supportedValues(monitor);

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return (null, MakeParseError(rawValue, supportedValues));
        }

        int? parsedValue = TryParseHex(rawValue);
        if (parsedValue is null)
        {
            return (null, MakeParseError(rawValue, supportedValues));
        }

        // If the monitor reports a supported-value set, the resolved value must be in it.
        if (!CliSettingValidation.IsDiscreteValueSupported(parsedValue.Value, supportedValues))
        {
            return (null, MakeUnsupportedError(rawValue, supportedValues!));
        }

        return (parsedValue, null);
    }

    public override string FormatDisplay(int value, IReadOnlyList<CustomVcpValueMapping>? customMappings = null, string monitorId = "")
        => MonitorDtoProjector.FormatDiscrete(VcpCode, value, customMappings, monitorId);

    /// <summary>
    /// Parses a hex literal of the form "0x??".
    /// </summary>
    private static int? TryParseHex(string raw)
    {
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(raw[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
            && hex is >= 0x00 and <= 0xFF)
        {
            return hex;
        }

        return null;
    }

    private CliError MakeParseError(string raw, IReadOnlyList<int>? supportedValues)
        => new()
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            MessageId = CliMessageIds.InvalidDiscrete,
            Supported = BuildSupportedList(VcpCode, supportedValues),
            Value = raw,
            Setting = Name,
        };

    private CliError MakeUnsupportedError(string raw, IReadOnlyList<int> supportedValues)
        => new()
        {
            Code = CliErrorCodes.InvalidDiscreteValue,
            MessageId = CliMessageIds.DiscreteNotInSet,
            Supported = BuildSupportedList(VcpCode, supportedValues),
            Value = raw,
            Setting = Name,
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
