// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using PowerDisplay.Cli.Errors;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.Resolution;

/// <summary>
/// Picks the target monitor from the discovered set using the CLI selector flags
/// (<c>-n</c> / <c>--monitor-number</c> and <c>-i</c> / <c>--monitor-id</c>). The
/// monitor-id selector wins when both are supplied; the resolver reports the
/// override via <see cref="MonitorResolution.Warning"/> so the caller can echo it
/// to stderr.
/// </summary>
public static class MonitorResolver
{
    public static MonitorResolution Resolve(
        IReadOnlyList<Monitor> monitors,
        int? monitorNumber,
        string? monitorId)
    {
        var hasNumber = monitorNumber.HasValue;
        var hasId = !string.IsNullOrEmpty(monitorId);

        if (!hasNumber && !hasId)
        {
            return new MonitorResolution
            {
                Error = new CliError
                {
                    Code = CliErrorCodes.SelectorMissing,
                    ExitCode = CliExitCodes.SelectorMissing,
                    Message = "one of --monitor-number/-n or --monitor-id/-i is required",
                    Hint = "run 'powerdisplay list' to see available monitors",
                },
            };
        }

        string? warning = null;
        if (hasNumber && hasId)
        {
            warning = $"warning: --monitor-number {monitorNumber.GetValueOrDefault()} ignored because --monitor-id was also provided";
        }

        if (hasId)
        {
            for (int i = 0; i < monitors.Count; i++)
            {
                if (string.Equals(monitors[i].Id, monitorId, StringComparison.OrdinalIgnoreCase))
                {
                    return new MonitorResolution { Monitor = monitors[i], Warning = warning };
                }
            }

            return new MonitorResolution
            {
                Error = new CliError
                {
                    Code = CliErrorCodes.MonitorNotFound,
                    ExitCode = CliExitCodes.MonitorNotFound,
                    Message = $"no monitor found with id '{monitorId}'",
                    Hint = "run 'powerdisplay list' to see available monitors",
                },
            };
        }

        var number = monitorNumber.GetValueOrDefault();
        for (int i = 0; i < monitors.Count; i++)
        {
            if (monitors[i].MonitorNumber == number)
            {
                return new MonitorResolution { Monitor = monitors[i] };
            }
        }

        return new MonitorResolution
        {
            Error = new CliError
            {
                Code = CliErrorCodes.MonitorNotFound,
                ExitCode = CliExitCodes.MonitorNotFound,
                Message = $"no monitor found with number {number}",
                Hint = "run 'powerdisplay list' to see available monitors",
            },
        };
    }
}
