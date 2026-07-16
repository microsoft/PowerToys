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
using PowerDisplay.Contracts;
using PowerDisplay.Models;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Ipc;

/// <summary>
/// A continuous percentage VCP setting (brightness, contrast, volume). The <c>set</c> value is a
/// plain integer that must fall in [0, 100]; the display string is a bare percentage.
/// </summary>
internal sealed class ContinuousVcpSetting : CliVcpSetting
{
    private const int Min = 0;
    private const int Max = 100;

    public ContinuousVcpSetting(
        string name,
        byte vcpCode,
        MonitorReadFlags readFlag,
        Func<Monitor, bool> supports,
        Func<Monitor, int> current,
        Func<IMonitorManager, string, int, CancellationToken, Task<MonitorOperationResult>> apply,
        string unsupportedReason)
        : base(name, vcpCode, readFlag, supports, current, apply, unsupportedReason)
    {
    }

    public override CliSettingKind Kind => CliSettingKind.Continuous;

    /// <summary>
    /// Parses the raw value as an integer (the app receives it as a string from the JSON request),
    /// then validates it is within [0, 100].
    /// </summary>
    public override (int? Value, CliError? Error) ParseSetValue(string rawValue, Monitor monitor)
    {
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requested))
        {
            return (null, new CliError
            {
                Code = CliErrorCodes.ArgumentError,
                MessageId = CliMessageIds.InvalidInteger,
                Value = rawValue,
                Setting = Name,
            });
        }

        if (requested < Min || requested > Max)
        {
            return (null, new CliError
            {
                Code = CliErrorCodes.OutOfRange,
                MessageId = CliMessageIds.OutOfRange,
                ExpectedRange = $"[{Min}, {Max}]",
                Value = requested.ToString(CultureInfo.InvariantCulture),
                Setting = Name,
            });
        }

        return (requested, null);
    }

    public override string FormatDisplay(int value, IReadOnlyList<CustomVcpValueMapping>? customMappings = null, string monitorId = "")
        => value + "%";
}
