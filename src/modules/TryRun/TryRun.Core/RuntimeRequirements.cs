// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Principal;

namespace PowerToys.TryRun.Core;

public static class RuntimeRequirements
{
    public static string? GetUnavailableReason()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100))
        {
            return "Try Run requires Windows 11 24H2 or later.";
        }

        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            return "Open Try Run without administrator privileges.";
        }

        return null;
    }
}
