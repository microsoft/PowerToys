// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Properties;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default <see cref="INpmCommandRunner"/> that shells out to the npm executable found on PATH.
/// npm is always invoked with lifecycle scripts disabled and an exact version pinned, into a
/// staging directory the caller supplies. The resolved package integrity is read back from the
/// lockfile npm produces so the caller can verify it before the package is promoted.
/// </summary>
public sealed class NpmCommandRunner : INpmCommandRunner
{
    // npm on Windows is a batch shim; probe the common executable names on PATH.
    private static readonly string[] NpmExecutableNames = ["npm.cmd", "npm.exe", "npm"];

    // Upper bound on a single npm install so the gallery cannot stay on "Installing..."
    // forever when npm hangs (for example, an unreachable registry with no output).
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    // A directory whose handles are still being released briefly rejects a delete; retry a few
    // times with a short backoff before giving up so an uninstall does not fail spuriously.
    private const int DeleteAttempts = 5;
    private static readonly TimeSpan DeleteRetryDelay = TimeSpan.FromMilliseconds(100);

    // Cached composite formats for the localized runner messages that take arguments (CA1863).
    private static readonly CompositeFormat CreateStagingFailedFormat = CompositeFormat.Parse(Resources.npm_runner_create_staging_failed);
    private static readonly CompositeFormat TimedOutFormat = CompositeFormat.Parse(Resources.npm_runner_timed_out);
    private static readonly CompositeFormat FailedExitFormat = CompositeFormat.Parse(Resources.npm_runner_failed_exit);

    public bool IsNpmAvailable() => ResolveNpmExecutable() is not null;

    public async Task<NpmCommandResult> InstallAsync(string stagingDirectory, NpmArtifact artifact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var npmExecutable = ResolveNpmExecutable();
        if (npmExecutable is null)
        {
            return NpmCommandResult.Fail(Resources.npm_runner_npm_not_found);
        }

        try
        {
            Directory.CreateDirectory(stagingDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to create npm staging directory {stagingDirectory}: {ex.Message}");
            return NpmCommandResult.Fail(string.Format(CultureInfo.CurrentCulture, CreateStagingFailedFormat, ex.Message));
        }

        var psi = new ProcessStartInfo
        {
            FileName = npmExecutable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = stagingDirectory,
        };

        // The spec is the single validated "name@version" token. Passing it through ArgumentList
        // (never string concatenation) keeps npm from ever reading it as a flag or a second argument.
        foreach (var argument in BuildInstallArguments(artifact))
        {
            psi.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return NpmCommandResult.Fail(Resources.npm_runner_start_failed);
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
                await TerminateAndWaitAsync(process).ConfigureAwait(false);
                Logger.LogError($"npm install {artifact.InstallSpec} timed out after {InstallTimeout.TotalMinutes:0} minutes.");
                return NpmCommandResult.Fail(string.Format(CultureInfo.CurrentCulture, TimedOutFormat, InstallTimeout.TotalMinutes.ToString("0", CultureInfo.CurrentCulture)));
            }
            catch (OperationCanceledException)
            {
                await TerminateAndWaitAsync(process).ConfigureAwait(false);
                throw;
            }

            if (process.ExitCode != 0)
            {
                var error = stderrBuilder.ToString().Trim();
                Logger.LogError($"npm install {artifact.InstallSpec} failed (exit {process.ExitCode}): {error}");
                return NpmCommandResult.Fail(string.IsNullOrEmpty(error)
                    ? string.Format(CultureInfo.CurrentCulture, FailedExitFormat, process.ExitCode)
                    : error);
            }

            var resolvedIntegrity = ReadResolvedIntegrity(stagingDirectory, artifact.Package);
            return NpmCommandResult.Ok(resolvedIntegrity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError($"npm install {artifact.InstallSpec} threw: {ex.Message}");
            return NpmCommandResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Builds the immutable npm argument list for an approved artifact. The first real token is the
    /// validated "name@version" spec; lifecycle scripts are always disabled and the exact version is
    /// saved. A registry, when present, is passed only through its own flag pair.
    /// </summary>
    internal static IReadOnlyList<string> BuildInstallArguments(NpmArtifact artifact)
    {
        var arguments = new List<string>
        {
            "install",
            artifact.InstallSpec,
            "--ignore-scripts",
            "--save-exact",
            "--no-audit",
            "--no-fund",
            "--loglevel=error",
        };

        if (!string.IsNullOrWhiteSpace(artifact.Registry))
        {
            arguments.Add("--registry");
            arguments.Add(artifact.Registry);
        }

        return arguments;
    }

    public bool RemoveDirectory(string targetDirectory)
    {
        if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return true;
        }

        // Never recurse through a junction or symbolic link: deleting recursively could reach files
        // outside the extensions tree. If the directory itself is a reparse point, refuse.
        if (IsReparsePoint(targetDirectory))
        {
            Logger.LogError($"Refusing to delete '{targetDirectory}' because it is a reparse point (junction or symbolic link).");
            return false;
        }

        for (var attempt = 1; attempt <= DeleteAttempts; attempt++)
        {
            try
            {
                Directory.Delete(targetDirectory, recursive: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    Logger.LogError($"Failed to delete directory {targetDirectory} after {DeleteAttempts} attempts: {ex.Message}");
                    return false;
                }

                Thread.Sleep(DeleteRetryDelay);
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the Subresource Integrity npm resolved for <paramref name="package"/> from the lockfile
    /// generated in <paramref name="stagingDirectory"/>. Returns null when the lockfile is missing or
    /// does not contain an entry for the package.
    /// </summary>
    private static string? ReadResolvedIntegrity(string stagingDirectory, string package)
    {
        var lockfilePath = Path.Combine(stagingDirectory, "package-lock.json");
        if (!File.Exists(lockfilePath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(lockfilePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            // lockfileVersion 2/3: packages["node_modules/<package>"].integrity
            if (root.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Object
                && packages.TryGetProperty($"node_modules/{package}", out var packageEntry)
                && packageEntry.TryGetProperty("integrity", out var integrity)
                && integrity.ValueKind == JsonValueKind.String)
            {
                return integrity.GetString();
            }

            // lockfileVersion 1: dependencies[<package>].integrity
            if (root.TryGetProperty("dependencies", out var dependencies) && dependencies.ValueKind == JsonValueKind.Object
                && dependencies.TryGetProperty(package, out var dependencyEntry)
                && dependencyEntry.TryGetProperty("integrity", out var legacyIntegrity)
                && legacyIntegrity.ValueKind == JsonValueKind.String)
            {
                return legacyIntegrity.GetString();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.LogError($"Failed to read resolved integrity from {lockfilePath}: {ex.Message}");
        }

        return null;
    }

    private static async Task TerminateAndWaitAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);

                // Give the OS a bounded moment to tear the tree down so file handles are released
                // before the staging directory is cleaned up.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException or OperationCanceledException)
        {
            Logger.LogError($"Failed to terminate npm process: {ex.Message}");
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
        {
            // If the attributes cannot be read, err on the side of caution and treat it as unsafe.
            return true;
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
