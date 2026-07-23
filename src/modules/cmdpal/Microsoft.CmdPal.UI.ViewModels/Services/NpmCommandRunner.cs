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
    // npm on Windows ships as an npm.cmd batch shim. Passing an argument that contains a shell
    // metacharacter to a .cmd file can let cmd.exe reinterpret it, even through ProcessStartInfo's
    // ArgumentList. To keep untrusted arguments off any batch/cmd command line, the runner instead
    // resolves node.exe and npm's JavaScript entry point (npm-cli.js) and launches
    // "node.exe npm-cli.js install ...". node.exe is a real executable, so its ArgumentList is passed
    // verbatim with no shell in the middle.
    private const string NodeExecutableName = "node.exe";

    // npm-cli.js relative to a directory that contains node.exe, and to a global npm prefix.
    private static readonly string NpmCliRelativePath = Path.Combine("node_modules", "npm", "bin", "npm-cli.js");

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

    public bool IsNpmAvailable() => ResolveNpmInvocation() is not null;

    public async Task<NpmCommandResult> InstallAsync(string stagingDirectory, NpmArtifact artifact, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var invocation = ResolveNpmInvocation();
        if (invocation is null)
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
            FileName = invocation.Value.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = stagingDirectory,
        };

        // Launcher arguments first (npm-cli.js), so node.exe runs npm itself. These are trusted,
        // runner-resolved paths.
        foreach (var launcherArgument in invocation.Value.LauncherArguments)
        {
            psi.ArgumentList.Add(launcherArgument);
        }

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

            // The top-level artifact is pinned by exact version, but npm resolves transitive
            // dependency ranges at install time. Before the package is trusted, require that every
            // resolved dependency in the lockfile npm just produced came from an approved registry
            // over HTTPS and carries a Subresource Integrity hash. This fails closed: a lockfile that
            // is missing, malformed, or contains a file:/git:/http:/integrity-less resolution is
            // rejected, and the committed lockfile then pins the verified set.
            var lockfileError = VerifyLockfileIntegrity(stagingDirectory);
            if (lockfileError is not null)
            {
                Logger.LogError($"npm install {artifact.InstallSpec} produced an untrusted lockfile: {lockfileError}");
                return NpmCommandResult.Fail(lockfileError);
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

    public bool RemoveDirectory(string targetDirectory, CancellationToken cancellationToken = default)
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
            cancellationToken.ThrowIfCancellationRequested();

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

    /// <summary>
    /// A resolved npm launcher: the executable to start (node.exe) and the leading arguments that
    /// make it run npm (the path to npm-cli.js). The install spec and flags are appended after these.
    /// </summary>
    internal readonly record struct NpmInvocation(string FileName, IReadOnlyList<string> LauncherArguments);

    /// <summary>
    /// Verifies that every resolved dependency in the lockfile npm generated in
    /// <paramref name="stagingDirectory"/> was fetched from an approved registry over HTTPS and
    /// carries a supported Subresource Integrity hash. Returns null when the whole tree is trusted, or
    /// a localized error message describing the first untrusted resolution. Fails closed: a missing or
    /// unreadable lockfile is treated as untrusted.
    /// </summary>
    internal static string? VerifyLockfileIntegrity(string stagingDirectory)
    {
        var lockfilePath = Path.Combine(stagingDirectory, "package-lock.json");
        if (!File.Exists(lockfilePath))
        {
            return Resources.npm_runner_lockfile_untrusted;
        }

        try
        {
            using var stream = File.OpenRead(lockfilePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            // lockfileVersion 2/3: a "packages" map keyed by install path. The root package has an
            // empty key and no resolution of its own; a "link": true entry points at a local
            // workspace and is skipped. Every other entry must carry a trusted resolved URL + hash.
            if (root.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Object)
            {
                foreach (var package in packages.EnumerateObject())
                {
                    if (package.Name.Length == 0)
                    {
                        continue;
                    }

                    if (package.Value.TryGetProperty("link", out var link)
                        && link.ValueKind == JsonValueKind.True)
                    {
                        continue;
                    }

                    if (!IsTrustedResolution(package.Value))
                    {
                        return Resources.npm_runner_lockfile_untrusted;
                    }
                }

                return null;
            }

            // lockfileVersion 1: a nested "dependencies" tree. Walk it recursively.
            if (root.TryGetProperty("dependencies", out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
            {
                return VerifyLegacyDependencies(dependencies) ? null : Resources.npm_runner_lockfile_untrusted;
            }

            // Neither shape present: nothing was pinned, so the tree cannot be trusted.
            return Resources.npm_runner_lockfile_untrusted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.LogError($"Failed to verify lockfile integrity at {lockfilePath}: {ex.Message}");
            return Resources.npm_runner_lockfile_untrusted;
        }
    }

    private static bool VerifyLegacyDependencies(JsonElement dependencies)
    {
        foreach (var dependency in dependencies.EnumerateObject())
        {
            var entry = dependency.Value;
            if (entry.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // A "bundled" dependency ships inside its parent's tarball and has no resolution of its
            // own; it was already covered by the parent's integrity. Anything else must be trusted.
            var isBundled = entry.TryGetProperty("bundled", out var bundled) && bundled.ValueKind == JsonValueKind.True;
            if (!isBundled && !IsTrustedResolution(entry))
            {
                return false;
            }

            if (entry.TryGetProperty("dependencies", out var nested) && nested.ValueKind == JsonValueKind.Object
                && !VerifyLegacyDependencies(nested))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsTrustedResolution(JsonElement entry)
    {
        var resolved = entry.TryGetProperty("resolved", out var resolvedElement) && resolvedElement.ValueKind == JsonValueKind.String
            ? resolvedElement.GetString()
            : null;
        var integrity = entry.TryGetProperty("integrity", out var integrityElement) && integrityElement.ValueKind == JsonValueKind.String
            ? integrityElement.GetString()
            : null;

        return NpmArtifact.IsRegistrySourcedHttps(resolved) && NpmArtifact.IsSupportedIntegrity(integrity);
    }

    /// <summary>
    /// Resolves node.exe and npm's npm-cli.js so npm can be launched without the npm.cmd batch shim.
    /// Probes PATH for node.exe, then looks for npm-cli.js next to node.exe (a standard Node.js
    /// install) and under the global npm prefix reported by the environment. Returns null when either
    /// piece cannot be located.
    /// </summary>
    internal static NpmInvocation? ResolveNpmInvocation() =>
        ResolveNpmInvocation(GetPathDirectories());

    internal static NpmInvocation? ResolveNpmInvocation(IReadOnlyList<string> pathDirectories)
    {
        ArgumentNullException.ThrowIfNull(pathDirectories);

        foreach (var directory in pathDirectories)
        {
            string nodeCandidate;
            try
            {
                nodeCandidate = Path.Combine(directory, NodeExecutableName);
            }
            catch (ArgumentException)
            {
                // Malformed PATH entry; skip it.
                continue;
            }

            if (!File.Exists(nodeCandidate))
            {
                continue;
            }

            var npmCli = FindNpmCli(directory);
            if (npmCli is not null)
            {
                return new NpmInvocation(nodeCandidate, new[] { npmCli });
            }
        }

        return null;
    }

    private static string? FindNpmCli(string nodeDirectory)
    {
        // Standard Windows Node.js layout: npm-cli.js sits under the same directory as node.exe.
        foreach (var candidateRoot in EnumerateNpmPrefixCandidates(nodeDirectory))
        {
            string npmCli;
            try
            {
                npmCli = Path.Combine(candidateRoot, NpmCliRelativePath);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (File.Exists(npmCli))
            {
                return npmCli;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateNpmPrefixCandidates(string nodeDirectory)
    {
        // node.exe's own directory (Program Files\nodejs) is the usual prefix on Windows.
        yield return nodeDirectory;

        // A user-level npm prefix (npm config's default on Windows) lives under APPDATA\npm.
        var appData = Environment.GetEnvironmentVariable("APPDATA");
        if (!string.IsNullOrEmpty(appData))
        {
            yield return Path.Combine(appData, "npm");
        }
    }

    private static IReadOnlyList<string> GetPathDirectories()
    {
        var pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return [];
        }

        return pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
