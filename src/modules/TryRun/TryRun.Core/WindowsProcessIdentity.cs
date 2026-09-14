// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

// A PID alone is not an identity: Windows can reuse it after a process exits.
public sealed record WindowsProcessIdentity(uint ProcessId, long CreationTime)
{
    public static WindowsProcessIdentity? Capture(uint processId)
    {
        using var process = WindowsProcessTree.Open(processId);
        return WindowsProcessTree.TryGetTimes(process, out var created, out _) && WindowsProcessTree.IsAlive(process)
            ? new WindowsProcessIdentity(processId, created)
            : null;
    }
}
