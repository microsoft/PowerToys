// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PowerDisplay.Cli.Errors;
using PowerDisplay.Cli.Output;
using PowerDisplay.Common.Services;
using Monitor = PowerDisplay.Common.Models.Monitor;

namespace PowerDisplay.Cli.Commands;

public static class ListCommand
{
    public static async Task<int> RunAsync(
        MonitorManager monitorManager,
        ICliOutput output,
        CancellationToken cancellationToken)
    {
        var monitors = await monitorManager.DiscoverMonitorsAsync(cancellationToken);

        var entries = new List<CliListMonitor>(monitors.Count);
        foreach (var m in monitors)
        {
            entries.Add(BuildEntry(m));
        }

        output.WriteListResult(new CliListResult { Monitors = entries });
        return CliExitCodes.Ok;
    }

    internal static CliListMonitor BuildEntry(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
        Method = m.CommunicationMethod,
        SupportsBrightness = m.SupportsBrightness,
        SupportsContrast = m.SupportsContrast,
        SupportsVolume = m.SupportsVolume,
        SupportsColorTemperature = m.SupportsColorTemperature,
        SupportsInputSource = m.SupportsInputSource,
        SupportsPowerState = m.SupportsPowerState,
    };
}
