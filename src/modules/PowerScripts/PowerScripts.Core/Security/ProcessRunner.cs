// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace PowerScripts.Core.Security;

/// <summary>The exit code, standard output and standard error captured from a launched process.</summary>
public readonly record struct ProcessRunResult(int ExitCode, string StdOut, string StdErr);

/// <summary>
/// The single process-launch chokepoint for <b>every</b> PowerScript execution path (PowerShell,
/// Python, WSL, and descriptor-driven). It resolves MXC policy in one place and, whether launching
/// the MXC executor, an explicitly risk-accepted direct process, or a platform-default direct WSL
/// process that ProcessContainer cannot host, ensures that launch uses the interactive user's
/// rights rather than the host's administrator token.
///
/// <list type="bullet">
///   <item><description>When the host is <b>not</b> elevated, the selected executor is launched
///   directly under the current standard-user token.</description></item>
///   <item><description>When the host <b>is</b> elevated, the process is dropped to the shell user's
///   token (see <see cref="ShellTokenProcessLauncher"/>). If that de-elevation cannot be done, the
///   run <b>fails closed</b> — PowerScripts never falls back to launching the script elevated.</description></item>
/// </list>
/// </summary>
public static class ProcessRunner
{
    /// <summary>Exit code returned when a script is refused because it could not be de-elevated.</summary>
    public const int RefusedElevatedExitCode = 126;

    public const int UnsupportedIsolationExitCode = 125;

    internal const string RefusedElevatedMessage =
        "PowerScripts refused to run this script: PowerToys is running as administrator and the script " +
        "could not be dropped to your standard-user rights. For security, PowerScripts never runs a " +
        "script elevated. Restart PowerToys without administrator rights to run this script.";

    /// <summary>
    /// Launches <paramref name="startInfo"/>, writing <paramref name="standardInput"/> to its stdin
    /// (when provided) and returning its exit code and captured streams. <paramref name="timeoutMs"/>
    /// of 0 waits indefinitely.
    /// </summary>
    public static ProcessRunResult Run(
        ProcessStartInfo startInfo,
        ProcessExecutionContext context,
        string? standardInput = null,
        int timeoutMs = 0,
        MxcSettings? mxcSettings = null)
    {
        var settings = mxcSettings ?? MxcSettings.Load();
        var policy = MxcPolicyResolver.Resolve(settings, context.ScriptId, context.RecommendedPolicies);

        if (policy.Enabled && IsWslTarget(startInfo))
        {
            if (policy.UsesPlatformDefault)
            {
                return SecureLauncher.Run(startInfo, standardInput, timeoutMs);
            }

            return new ProcessRunResult(
                UnsupportedIsolationExitCode,
                string.Empty,
                "MXC's Windows processcontainer cannot host wsl.exe. Use Windows execution, or explicitly disable MXC for this script.");
        }

        return policy.Enabled
            ? MxcProcessRunner.Run(startInfo, context, policy, settings, standardInput, timeoutMs)
            : SecureLauncher.Run(startInfo, standardInput, timeoutMs);
    }

    internal static bool IsWslTarget(ProcessStartInfo startInfo) =>
        string.Equals(Path.GetFileName(startInfo.FileName), "wsl.exe", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Path.GetFileName(startInfo.FileName), "wsl", StringComparison.OrdinalIgnoreCase);

    internal static IProcessLauncher SecureLauncher { get; } = new SecureProcessLauncher();

    private sealed class SecureProcessLauncher : IProcessLauncher
    {
        public ProcessRunResult Run(ProcessStartInfo startInfo, string? standardInput, int timeoutMs)
        {
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            if (standardInput is not null)
            {
                startInfo.RedirectStandardInput = true;
            }

            if (!ProcessElevation.IsElevated)
            {
                return RunDirect(startInfo, standardInput, timeoutMs);
            }

            // This launches wxc-exec itself when MXC is enabled. Therefore an elevated PowerToys host
            // never bypasses the existing shell-token de-elevation boundary on the way into MXC.
            var deElevated = ShellTokenProcessLauncher.TryRun(startInfo, standardInput, timeoutMs);
            return deElevated ?? new ProcessRunResult(RefusedElevatedExitCode, string.Empty, RefusedElevatedMessage);
        }

        private static ProcessRunResult RunDirect(ProcessStartInfo startInfo, string? standardInput, int timeoutMs)
        {
            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Read both streams concurrently to avoid a pipe deadlock on large output.
            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();

            if (standardInput is not null)
            {
                process.StandardInput.Write(standardInput);
                process.StandardInput.Close();
            }

            if (timeoutMs > 0)
            {
                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception)
                    {
                        // Best effort.
                    }

                    return new ProcessRunResult(124, stdOutTask.GetAwaiter().GetResult(), "Script timed out.");
                }
            }
            else
            {
                process.WaitForExit();
            }

            return new ProcessRunResult(
                process.ExitCode,
                stdOutTask.GetAwaiter().GetResult(),
                stdErrTask.GetAwaiter().GetResult());
        }
    }
}
