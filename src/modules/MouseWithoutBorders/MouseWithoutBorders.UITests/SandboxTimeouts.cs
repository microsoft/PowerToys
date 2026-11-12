// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.MouseWithoutBorders.UITests;

internal static class SandboxTimeouts
{
    internal const int EndpointStartupMinutes = 15;
    internal const int EndpointStartRequestSeconds = (EndpointStartupMinutes + 5) * 60;
    internal const int EndpointEvidenceRequestSeconds = 180;
    internal const int GuestStopRequestSeconds = 300;

    internal static readonly TimeSpan LegacyBootstrap = TimeSpan.FromMinutes(20);
    internal static readonly TimeSpan ModernBootstrap = TimeSpan.FromMinutes(35);
    internal static readonly TimeSpan LegacyRun = TimeSpan.FromMinutes(65);
    internal static readonly TimeSpan ModernRun = TimeSpan.FromMinutes(90);
    internal static readonly TimeSpan ModernStart = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan LegacyViewerStart = TimeSpan.FromMinutes(3);
    internal static readonly TimeSpan RecoveryRun = TimeSpan.FromSeconds(EndpointStartRequestSeconds) + TimeSpan.FromMinutes(3);

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
