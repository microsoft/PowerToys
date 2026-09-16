// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.TryRun.Core;

public static class WindowsApplicationFailure
{
    public static string? Describe(int? exitCode, bool timedOut, IsolationReport report)
    {
        if (timedOut || exitCode is null or 0)
        {
            return null;
        }

        var message = $"Application exit code: {exitCode.Value} (0x{unchecked((uint)exitCode.Value):X8}).";
        var activationDenied = report.Events.Any(item => item.Outcome == "Blocked" && item.NativeDenial is { } denial &&
            (denial.Resource.Contains("\\AppRepository\\Packages\\", StringComparison.OrdinalIgnoreCase) ||
             denial.Resource.Contains("\\AppModel\\SystemAppData\\", StringComparison.OrdinalIgnoreCase)));
        if (activationDenied)
        {
            message += " MXC recorded denied access to Windows app activation state. This packaged application may not be compatible with the current isolated environment. Review the denied-access report; Try Run has not granted additional system access.";
        }

        return message;
    }
}
