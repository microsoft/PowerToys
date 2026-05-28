// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Output;
using PowerDisplay.Cli.Resolution;
using PowerDisplay.Common.Models;
using PowerDisplay.Common.Services;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.Commands;

public static class GetCommand
{
    /// <summary>
    /// Canonical setting names accepted by <c>--setting</c>. The same identifiers
    /// are used in <see cref="CliSettingValue.Setting"/> so JSON consumers can
    /// switch on them.
    /// </summary>
    public static readonly string[] AllSettingNames =
    [
        "brightness",
        "contrast",
        "volume",
        "color-temperature",
        "input-source",
        "power-state",
        "orientation",
    ];

    public static async Task<int> RunAsync(
        MonitorManager monitorManager,
        int? monitorNumber,
        string? monitorId,
        string? settingFilter,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        var monitors = await monitorManager.DiscoverMonitorsAsync(cancellationToken);
        var resolution = MonitorResolver.Resolve(monitors, monitorNumber, monitorId);

        if (resolution.Warning is not null)
        {
            output.WriteWarning(resolution.Warning);
        }

        if (resolution.Error is not null)
        {
            output.WriteError(new CliErrorResult { Command = "get", Error = resolution.Error });
            return resolution.Error.ExitCode;
        }

        var monitor = resolution.Monitor!;
        var monitorRef = ToRef(monitor);

        IEnumerable<string> settingNames = settingFilter is null
            ? AllSettingNames
            : new[] { settingFilter.ToLowerInvariant() };

        var results = new List<CliSettingValue>();
        foreach (var name in settingNames)
        {
            var value = BuildSettingValue(monitor, name);
            if (value is null)
            {
                output.WriteError(new CliErrorResult
                {
                    Command = "get",
                    Monitor = monitorRef,
                    Error = new CliError
                    {
                        Code = CliErrorCodes.ArgumentError,
                        ExitCode = CliExitCodes.ArgumentError,
                        Setting = name,
                        Message = $"unknown setting '{name}'",
                        Hint = $"valid settings: {string.Join(", ", AllSettingNames)}",
                    },
                });
                return CliExitCodes.ArgumentError;
            }

            results.Add(value);
        }

        output.WriteGetResult(new CliGetResult { Monitor = monitorRef, Settings = results });
        return CliExitCodes.Ok;
    }

    private static CliSettingValue? BuildSettingValue(Monitor monitor, string settingName) => settingName switch
    {
        "brightness" => new CliSettingValue
        {
            Setting = "brightness",
            Raw = monitor.CurrentBrightness,
            Display = monitor.CurrentBrightness + "%",
            Supported = monitor.SupportsBrightness,
        },
        "contrast" => new CliSettingValue
        {
            Setting = "contrast",
            Raw = monitor.CurrentContrast,
            Display = monitor.CurrentContrast + "%",
            Supported = monitor.SupportsContrast,
        },
        "volume" => new CliSettingValue
        {
            Setting = "volume",
            Raw = monitor.CurrentVolume,
            Display = monitor.CurrentVolume + "%",
            Supported = monitor.SupportsVolume,
        },
        "color-temperature" => new CliSettingValue
        {
            Setting = "color-temperature",
            Raw = monitor.CurrentColorTemperature,
            Display = SetCommand.FormatDiscrete(0x14, monitor.CurrentColorTemperature),
            Supported = monitor.SupportsColorTemperature,
        },
        "input-source" => new CliSettingValue
        {
            Setting = "input-source",
            Raw = monitor.CurrentInputSource,
            Display = SetCommand.FormatDiscrete(0x60, monitor.CurrentInputSource),
            Supported = monitor.SupportsInputSource,
        },
        "power-state" => new CliSettingValue
        {
            Setting = "power-state",
            Raw = monitor.CurrentPowerState,
            Display = SetCommand.FormatDiscrete(0xD6, monitor.CurrentPowerState),
            Supported = monitor.SupportsPowerState,
        },
        "orientation" => new CliSettingValue
        {
            Setting = "orientation",
            Raw = monitor.Orientation,
            Display = SetCommand.OrientationDegrees(monitor.Orientation),
            Supported = !string.IsNullOrEmpty(monitor.GdiDeviceName),
        },
        _ => null,
    };

    private static CliMonitorRef ToRef(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
    };
}
