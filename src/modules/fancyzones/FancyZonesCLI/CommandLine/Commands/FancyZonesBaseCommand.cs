// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;

using FancyZonesCLI;
using FancyZonesCLI.CommandLine;
using FancyZonesCLI.Telemetry;
using Microsoft.PowerToys.Telemetry;

namespace FancyZonesCLI.CommandLine.Commands;

internal abstract class FancyZonesBaseCommand : Command
{
    protected FancyZonesBaseCommand(string name, string description)
        : base(name, description)
    {
        this.SetHandler(InvokeInternal);
    }

    protected abstract string Execute(InvocationContext context);

    private void InvokeInternal(InvocationContext context)
    {
        Logger.LogInfo($"Executing command '{Name}'");
        bool successful = false;

        if (!FancyZonesCliGuards.IsFancyZonesRunning())
        {
            Logger.LogWarning($"Command '{Name}' blocked: FancyZones is not running");
            context.Console.Error.Write($"{Properties.Resources.error_fancyzones_not_running}{Environment.NewLine}");
            context.ExitCode = 1;
            LogTelemetry(successful: false);
            return;
        }

        try
        {
            string output = Execute(context);
            context.ExitCode = 0;
            successful = true;

            Logger.LogInfo($"Command '{Name}' completed successfully");
            Logger.LogDebug($"Command '{Name}' output length: {output?.Length ?? 0}");

            if (!string.IsNullOrEmpty(output))
            {
                context.Console.Out.Write(output);
                context.Console.Out.Write(Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Command '{Name}' failed", ex);
            context.Console.Error.Write($"Error: {ex.Message}{Environment.NewLine}");
            context.ExitCode = 1;
            successful = false;
        }
        finally
        {
            LogTelemetry(successful);
        }
    }

    private void LogTelemetry(bool successful)
    {
        try
        {
            PowerToysTelemetry.Log.WriteEvent(new FancyZonesCLICommandEvent
            {
                CommandName = Name,
                Successful = successful,
            });
        }
        catch (Exception ex)
        {
            // Don't fail the command if telemetry logging fails
            Logger.LogError($"Failed to log telemetry for command '{Name}'", ex);
        }
    }
}
