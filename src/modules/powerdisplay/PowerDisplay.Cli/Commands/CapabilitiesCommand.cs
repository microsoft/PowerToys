// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

public static class CapabilitiesCommand
{
    public static async Task<int> RunAsync(
        MonitorManager monitorManager,
        int? monitorNumber,
        string? monitorId,
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
            output.WriteError(new CliErrorResult { Command = "capabilities", Error = resolution.Error });
            return resolution.Error.ExitCode;
        }

        var monitor = resolution.Monitor!;
        var caps = monitor.VcpCapabilitiesInfo;
        var vcpCodes = new List<CliVcpCodeInfo>();

        if (caps is not null)
        {
            foreach (var code in caps.GetSortedVcpCodes())
            {
                List<string>? discreteValues = null;
                if (code.HasDiscreteValues)
                {
                    discreteValues = new List<string>(code.SupportedValues.Count);
                    foreach (var v in code.SupportedValues)
                    {
                        var name = Common.Utils.VcpNames.GetValueName(code.Code, v);
                        discreteValues.Add(name is null ? $"0x{v:X2}" : $"{name} (0x{v:X2})");
                    }
                }

                vcpCodes.Add(new CliVcpCodeInfo
                {
                    Code = code.FormattedCode,
                    Name = code.Name,
                    Continuous = code.IsContinuous,
                    DiscreteValues = discreteValues,
                });
            }
        }

        output.WriteCapabilitiesResult(new CliCapabilitiesResult
        {
            Monitor = ToRef(monitor),
            CommunicationMethod = monitor.CommunicationMethod,
            RawCapabilities = monitor.CapabilitiesRaw,
            Model = caps?.Model,
            MccsVersion = caps?.MccsVersion,
            VcpCodes = vcpCodes,
        });

        return CliExitCodes.Ok;
    }

    private static CliMonitorRef ToRef(Monitor m) => new()
    {
        Number = m.MonitorNumber,
        Id = m.Id,
        Name = m.Name,
        Method = m.CommunicationMethod,
    };
}
