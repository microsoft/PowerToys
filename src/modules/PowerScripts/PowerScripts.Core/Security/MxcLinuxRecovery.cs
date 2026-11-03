// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerScripts.Core.Security;

public static class MxcLinuxRecovery
{
    internal const string RecoveryRequired = "Restart the PowerScripts Linux runtime to check this image. The previous session is interrupted, uses another image, or cannot be safely checked. Cached images and settings are kept.";
    internal const string LegacySharedRuntime = "This older Linux session uses a runtime shared with other MXC apps. PowerToys cannot safely restart it automatically. Save your work and restart Windows, then check Python again. Cached images and settings are kept.";
    internal const string RestartWindowsRequired = "The WSL backend could not safely release the PowerScripts Linux session. Save your work and restart Windows, then check Python again. Cached images, settings, and script approvals are kept. No processes or WSL distributions were forcibly stopped.";

    public static bool IsRpcUnavailable(ProcessRunResult result) =>
        result.ExitCode != 0 && (result.StdOut.Contains("0x800706BA", StringComparison.OrdinalIgnoreCase) ||
            result.StdErr.Contains("0x800706BA", StringComparison.OrdinalIgnoreCase));

    public static string Status(ProcessRunResult result)
    {
        if (result.ExitCode == 0)
        {
            return "ready";
        }

        if (result.StdErr.StartsWith(LegacySharedRuntime, StringComparison.Ordinal) ||
            result.StdErr.StartsWith(RestartWindowsRequired, StringComparison.Ordinal))
        {
            return "restart_windows_required";
        }

        if (result.StdErr.StartsWith(RecoveryRequired, StringComparison.Ordinal))
        {
            return "recovery_required";
        }

        if (IsRpcUnavailable(result))
        {
            return "runtime_unavailable";
        }

        if (result.StdErr.Contains("0x80070020", StringComparison.OrdinalIgnoreCase) ||
            result.StdOut.Contains("0x80070020", StringComparison.OrdinalIgnoreCase))
        {
            return "cache_busy";
        }

        if (result.StdErr.Contains("operation is running", StringComparison.Ordinal) ||
            result.StdErr.Contains("\"code\":\"busy\"", StringComparison.Ordinal))
        {
            return "runtime_busy";
        }

        return "check_failed";
    }
}
