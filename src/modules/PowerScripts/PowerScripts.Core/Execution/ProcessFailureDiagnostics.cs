// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using PowerScripts.Core.Security;

namespace PowerScripts.Core.Execution;

public static class ProcessFailureDiagnostics
{
    public static string? LinuxStatus(ProcessRunResult result)
    {
        if (result.ExitCode == 0)
        {
            return null;
        }

        // Native WSL diagnostics can contain NULs when UTF-16 output was decoded as UTF-8.
        // Normalize only for recognition; keep the captured streams intact in Details.
        var diagnostic = (result.StdErr + "\n" + result.StdOut).Replace("\0", string.Empty, StringComparison.Ordinal);
        if (diagnostic.Contains("WSL_E_WSL1_DISABLED", StringComparison.OrdinalIgnoreCase))
        {
            return "wsl1_policy_blocked";
        }

        if (diagnostic.Contains("HCS_E_HYPERV_NOT_INSTALLED", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("0x80370102", StringComparison.OrdinalIgnoreCase))
        {
            return "virtualization_unavailable";
        }

        if (diagnostic.Contains("WSLC image '", StringComparison.OrdinalIgnoreCase) &&
            diagnostic.Contains("not found locally", StringComparison.OrdinalIgnoreCase))
        {
            return "image_not_cached";
        }

        return null;
    }

    public static string DescribeFailure(ProcessRunResult result, bool linux = false, bool managedLinux = false)
    {
        var summary = linux ? LinuxStatus(result) switch
        {
            "wsl1_policy_blocked" => "WSL1 is blocked by your organization's policy. Python did not run. Contact your administrator or use an approved, working WSL2 distribution.",
            "virtualization_unavailable" => (managedLinux
                ? "The managed Linux environment could not start. Python did not run. "
                : "WSL2 could not start. Python did not run. ") +
                "Check that WSL2 virtualization is supported and enabled. In a virtual machine, ask its administrator whether nested virtualization is supported and enabled." +
                (managedLinux ? " Downloading the Linux image again will not fix this startup failure." : string.Empty),
            "image_not_cached" => "The configured managed Linux image is not cached. Confirm the image reference, then choose Download image in PowerScripts settings and check Python again.",
            _ => null,
        } : null;

        if (summary is null)
        {
            if (string.IsNullOrEmpty(result.StdOut))
            {
                return string.IsNullOrEmpty(result.StdErr)
                    ? $"The process failed without diagnostics (exit code {result.ExitCode})."
                    : result.StdErr;
            }

            return string.IsNullOrEmpty(result.StdErr)
                ? result.StdOut
                : result.StdErr + Environment.NewLine + Environment.NewLine + "Standard output:" + Environment.NewLine + result.StdOut;
        }

        var details = new StringBuilder(summary);
        details.AppendLine().AppendLine().AppendLine("Details:");
        details.Append("Exit code: ").Append(result.ExitCode).AppendLine();
        if (!string.IsNullOrEmpty(result.StdErr))
        {
            details.AppendLine("Standard error:").Append(result.StdErr);
        }

        if (!string.IsNullOrEmpty(result.StdOut))
        {
            details.AppendLine().AppendLine("Standard output:").Append(result.StdOut);
        }

        return details.ToString();
    }
}
