// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.PowerToys.QuickAccess;

public sealed record QuickAccessLaunchContext(string? ShowEventName, string? ExitEventName)
{
    public static QuickAccessLaunchContext Parse(string[] args)
    {
        string? showEvent = null;
        string? exitEvent = null;

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
        }

        return new QuickAccessLaunchContext(showEvent, exitEvent);
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
