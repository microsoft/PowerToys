// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseWithoutBorders.Core;

internal sealed class SessionPolicy
{
    internal const string AllowNonConsoleEnvironmentVariable = "POWERTOYS_MWB_ALLOW_NONCONSOLE";

    private SessionPolicy(bool allowNonConsole)
    {
        AllowNonConsole = allowNonConsole;
    }

    internal static SessionPolicy Current { get; private set; } = new(false);

    internal bool AllowNonConsole { get; }

    internal static void Initialize(bool serviceMode, bool runningAsSystem, string desktopName)
    {
        Current = FromConfiguration(
            Environment.GetEnvironmentVariable(AllowNonConsoleEnvironmentVariable),
            serviceMode,
            runningAsSystem,
            desktopName);
    }

    internal static SessionPolicy FromConfiguration(string flagValue, bool serviceMode, bool runningAsSystem, string desktopName)
    {
#if DEBUG
        var allowNonConsole = string.Equals(flagValue, "1", StringComparison.Ordinal) &&
            !serviceMode &&
            !runningAsSystem &&
            string.Equals(desktopName, "default", StringComparison.OrdinalIgnoreCase);
#else
        const bool allowNonConsole = false;
#endif

        return new SessionPolicy(allowNonConsole);
    }

    internal bool IsSessionAllowed(int sessionId, uint activeConsoleSessionId)
    {
        return sessionId >= 0 && (sessionId == activeConsoleSessionId || AllowNonConsole);
    }
}
