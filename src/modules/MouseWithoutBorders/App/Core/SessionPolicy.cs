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

    internal static void Initialize(bool serviceMode, bool runningAsSystem, string desktopName, bool allowNonConsoleSessions = false)
    {
        string flagValue = null;
#if DEBUG
        flagValue = Environment.GetEnvironmentVariable(AllowNonConsoleEnvironmentVariable);
#endif

        Current = FromConfiguration(
            flagValue,
            serviceMode,
            runningAsSystem,
            desktopName,
            allowNonConsoleSessions);
    }

    internal static SessionPolicy FromConfiguration(string flagValue, bool serviceMode, bool runningAsSystem, string desktopName, bool allowNonConsoleSessions = false)
    {
        var optedIn = allowNonConsoleSessions;
#if DEBUG
        optedIn |= string.Equals(flagValue, "1", StringComparison.Ordinal);
#endif

        var allowNonConsole = optedIn &&
            !serviceMode &&
            !runningAsSystem &&
            string.Equals(desktopName, "default", StringComparison.OrdinalIgnoreCase);

        return new SessionPolicy(allowNonConsole);
    }

    internal bool IsSessionAllowed(int sessionId, uint activeConsoleSessionId)
    {
        return sessionId >= 0 && (sessionId == activeConsoleSessionId || AllowNonConsole);
    }
}
