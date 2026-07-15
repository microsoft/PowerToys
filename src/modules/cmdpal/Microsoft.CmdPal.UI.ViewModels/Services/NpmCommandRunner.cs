// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default <see cref="INpmCommandRunner"/> that shells out to the npm executable found on PATH
/// and performs the directory side effects for install/uninstall.
/// </summary>
public sealed class NpmCommandRunner : INpmCommandRunner
{
    // npm on Windows is a batch shim; probe the common executable names on PATH.
    private static readonly string[] NpmExecutableNames = ["npm.cmd", "npm.exe", "npm"];

    // Upper bound on a single npm install so the gallery cannot stay on "Installing..."
    // forever when npm hangs (for example, an unreachable registry with no output).
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    public bool IsNpmAvailable() => ResolveNpmExecutable() is not null;

    public async Task<NpmCommandResult> InstallAsync(string targetDirectory, string package, string? registry, CancellationToken cancellationToken)
    {
        var npmExecutable = ResolveNpmExecutable();
        if (npmExecutable is null)
        {
            return NpmCommandResult.Fail("npm was not found on this machine.");
        }

        try
        {
            Directory.CreateDirectory(targetDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to create JS extension directory {targetDirectory}: {ex.Message}");
            return NpmCommandResult.Fail($"Could not create the extension directory: {ex.Message}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = npmExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = targetDirectory,
        };

        psi.ArgumentList.Add("install");
        psi.ArgumentList.Add(package);
        psi.ArgumentList.Add("--no-fund");
        psi.ArgumentList.Add("--no-audit");
        psi.ArgumentList.Add("--loglevel=error");
        if (!string.IsNullOrWhiteSpace(registry))
        {
            psi.ArgumentList.Add("--registry");
            psi.ArgumentList.Add(registry);
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return NpmCommandResult.Fail("Failed to start npm.");
            }

            var stderrBuilder = new StringBuilder();
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
            };
            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            // Bound the wait so a hung npm does not leave the UI stuck. A caller-driven cancel and
            // the timeout share one linked source; the catch below tells the two apart.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(InstallTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKillProcess(process);
                Logger.LogError($"npm install {package} timed out after {InstallTimeout.TotalMinutes:0} minutes.");
                return NpmCommandResult.Fail($"npm install timed out after {InstallTimeout.TotalMinutes:0} minutes.");
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw;
            }

            if (process.ExitCode == 0)
            {
                // npm installs the package under node_modules/<package>; hoist it to the
                // extension directory root so the discovery scan can find its manifest.
                return JsExtensionPackageLayout.Materialize(targetDirectory);
            }

            var error = stderrBuilder.ToString().Trim();
            Logger.LogError($"npm install {package} failed (exit {process.ExitCode}): {error}");
            return NpmCommandResult.Fail(string.IsNullOrEmpty(error)
                ? $"npm install failed with exit code {process.ExitCode}."
                : error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError($"npm install {package} threw: {ex.Message}");
            return NpmCommandResult.Fail(ex.Message);
        }
    }

    public bool RemoveDirectory(string targetDirectory)
    {
        if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return true;
        }

        try
        {
            Directory.Delete(targetDirectory, recursive: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to delete JS extension directory {targetDirectory}: {ex.Message}");
            return false;
        }
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            Logger.LogError($"Failed to terminate npm process: {ex.Message}");
        }
    }

    private static string? ResolveNpmExecutable()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var executableName in NpmExecutableNames)
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory, executableName);
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry; skip it.
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }
}
