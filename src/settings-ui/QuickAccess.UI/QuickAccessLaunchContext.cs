// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerToys.QuickAccess;

public sealed record QuickAccessLaunchContext(string? ShowEventName, string? ExitEventName, string? PositionMapName, string? RunnerPipeName, string? AppPipeName)
{
    public static QuickAccessLaunchContext Parse(string[] args)
    {
        string? showEvent = null;
        string? exitEvent = null;
        string? positionMap = null;
        string? runnerPipe = null;
        string? appPipe = null;

        foreach (var arg in args)
        {
            if (TryReadValue(arg, "--show-event", out var value))
            {
                showEvent = value;
            }
            else if (TryReadValue(arg, "--exit-event", out value))
            {
                exitEvent = value;
            }
            else if (TryReadValue(arg, "--position-map", out value))
            {
                positionMap = value;
            }
            else if (TryReadValue(arg, "--runner-pipe", out value))
            {
                runnerPipe = value;
            }
            else if (TryReadValue(arg, "--app-pipe", out value))
            {
                appPipe = value;
            }
        }

        return new QuickAccessLaunchContext(showEvent, exitEvent, positionMap, runnerPipe, appPipe);
    }

    private static bool TryReadValue(string candidate, string key, [NotNullWhen(true)] out string? value)
    {
        if (candidate.StartsWith(key, StringComparison.OrdinalIgnoreCase))
        {
            if (candidate.Length == key.Length)
            {
                value = null;
                return false;
            }

            if (candidate[key.Length] == '=')
            {
                value = candidate[(key.Length + 1)..].Trim('"');
                return true;
            }
        }

        value = null;
        return false;
    }
}
