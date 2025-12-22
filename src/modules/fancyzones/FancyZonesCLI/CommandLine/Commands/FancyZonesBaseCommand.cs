// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;

using FancyZonesCLI;
using FancyZonesCLI.CommandLine;

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

        if (!FancyZonesCliGuards.IsFancyZonesRunning())
        {
            Logger.LogWarning($"Command '{Name}' blocked: FancyZones is not running");
            context.Console.Error.Write($"Error: FancyZones is not running. Start PowerToys (FancyZones) and retry.{Environment.NewLine}");
            context.ExitCode = 1;
            return;
        }

        try
        {
            string output = Execute(context);
            context.ExitCode = 0;

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
        }
    }
}
