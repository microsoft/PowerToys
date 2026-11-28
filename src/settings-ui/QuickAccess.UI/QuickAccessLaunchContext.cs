// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerToys.QuickAccess;

public sealed record QuickAccessLaunchContext(
    string? VisibilityStateEventName,
    string? VisibilityChangedEventName,
    string? ExitEventName,
    string? RunnerPipeName,
    string? AppPipeName)
{
    public static QuickAccessLaunchContext Parse(string[] args)
    {
        string? visibilityStateEvent = null;
        string? visibilityChangedEvent = null;
        string? exitEvent = null;
        string? runnerPipe = null;
        string? appPipe = null;

        foreach (var arg in args)
        {
            if (TryReadValue(arg, "--visibility-state-event", out var value))
            {
                visibilityStateEvent = value;
            }
            else if (TryReadValue(arg, "--visibility-changed-event", out value))
            {
                visibilityChangedEvent = value;
            }
            else if (TryReadValue(arg, "--exit-event", out value))
            {
                exitEvent = value;
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

        return new QuickAccessLaunchContext(visibilityStateEvent, visibilityChangedEvent, exitEvent, runnerPipe, appPipe);
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
