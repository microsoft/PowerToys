// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class SandboxTimeouts
{
    internal static readonly TimeSpan LegacyBootstrap = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan ModernBootstrap = TimeSpan.FromMinutes(35);
    internal static readonly TimeSpan LegacyRun = TimeSpan.FromMinutes(40);
    internal static readonly TimeSpan ModernRun = TimeSpan.FromMinutes(70);
    internal static readonly TimeSpan ModernStart = TimeSpan.FromMinutes(10);

    internal static DateTime BootstrapDeadline(DateTime now, DateTime hardDeadline, bool modern)
    {
        if (hardDeadline <= now)
        {
            throw new WinAppSandboxException("run_deadline_expired");
        }

        var deadline = now + (modern ? ModernBootstrap : LegacyBootstrap);
        return deadline < hardDeadline ? deadline : hardDeadline;
    }
}
