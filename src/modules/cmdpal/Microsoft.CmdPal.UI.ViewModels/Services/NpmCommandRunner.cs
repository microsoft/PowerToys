// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Properties;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default <see cref="INpmCommandRunner"/>. It finds node.exe on PATH and runs npm through
/// npm-cli.js instead of npm.cmd. The runner uses <c>npm pack</c> to download the exact approved
/// tarball, treats that package as the project root, requires a shipped npm-shrinkwrap.json, then
/// runs <c>npm ci</c> with lifecycle scripts off. Before the installer promotes anything, the runner
/// checks the tarball integrity and the frozen lockfile.
/// </summary>
public sealed class NpmCommandRunner : INpmCommandRunner
{
    // npm on Windows ships as an npm.cmd batch shim. A shell metacharacter passed to a .cmd file can
    // be reinterpreted by cmd.exe, even through ProcessStartInfo.ArgumentList. Keep untrusted
    // arguments off any batch command line by running node.exe with npm-cli.js directly.
    private const string NodeExecutableName = "node.exe";

    // npm-cli.js relative to a directory that contains node.exe, and to a global npm prefix.
    private static readonly string NpmCliRelativePath = Path.Combine("node_modules", "npm", "bin", "npm-cli.js");

    // The directory under staging where the downloaded tarball is unpacked. npm tarballs root their
    // entries under "package/", so the installer can promote this path directly.
    internal const string PackageRootDirectoryName = "package";

    // A private subdirectory of staging that only holds the downloaded .tgz, kept separate from the
    // extracted package root so a single tarball is easy to locate.
    internal const string PackDirectoryName = "__cmdpal_pack";

    // The lockfile that must be present inside the package tarball. It freezes the full dependency
    // closure so npm ci installs an exact set instead of resolving mutable ranges at install time.
    internal const string ShrinkwrapFileName = "npm-shrinkwrap.json";

    // The lockfiles npm consults, in precedence order. npm-shrinkwrap.json is the publishable form
    // and takes precedence over package-lock.json.
    private static readonly string[] LockfileNames = [ShrinkwrapFileName, "package-lock.json"];

    // Upper bound on one npm operation so the gallery cannot stay on "Installing..." forever when
    // npm hangs, such as an unreachable registry with no output.
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(5);

    // A directory with handles still closing can briefly reject a delete. Retry a few times so an
    // uninstall does not fail just because the OS needed another moment.
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

        try
        {
            // 1) Download the exact "name@version" tarball with lifecycle scripts off. npm pack fetches
            //    the published artifact without resolving dependencies.
            var packDirectory = Path.Combine(stagingDirectory, PackDirectoryName);
            Directory.CreateDirectory(packDirectory);

            var packResult = await RunNpmAsync(invocation.Value, BuildPackArguments(artifact, packDirectory), stagingDirectory, artifact.InstallSpec, cancellationToken).ConfigureAwait(false);
            if (!packResult.Succeeded)
            {
                return NpmCommandResult.Fail(packResult.ErrorMessage ?? Resources.npm_runner_extract_failed);
            }

            var tarballPath = FindPackedTarball(packDirectory);
            if (tarballPath is null)
            {
                Logger.LogError($"npm pack {artifact.InstallSpec} produced no tarball in {packDirectory}.");
                return NpmCommandResult.Fail(Resources.npm_runner_extract_failed);
            }

            // 2) Verify the downloaded tarball before extracting or executing npm against anything it
            //    contains. A mismatched artifact must not get a chance to choose dependency URLs.
            var resolvedIntegrity = ComputeTarballIntegrity(tarballPath);
            if (resolvedIntegrity is null)
            {
                return NpmCommandResult.Fail(Resources.npm_runner_extract_failed);
            }

            if (!string.Equals(resolvedIntegrity, artifact.Integrity, StringComparison.Ordinal))
            {
                Logger.LogError($"Integrity mismatch installing '{artifact.InstallSpec}': expected {artifact.Integrity}, npm resolved {resolvedIntegrity}.");
                return NpmCommandResult.Fail(Resources.npm_installer_integrity_mismatch);
            }

            // 3) Make the published package the root project before npm ci runs. npm only honors an
            //    embedded npm-shrinkwrap.json at the project root, not inside a dependency.
            var packageRoot = Path.Combine(stagingDirectory, PackageRootDirectoryName);
            var extractError = TryExtractPackage(tarballPath, packageRoot);
            if (extractError is not null)
            {
                return NpmCommandResult.Fail(extractError);
            }

            // 4) Fail closed unless the publisher shipped a shrinkwrap that freezes the full dependency
            //    closure. Without it, npm ci has no fixed tree to install.
            var shrinkwrapError = RequirePublisherShrinkwrap(packageRoot);
            if (shrinkwrapError is not null)
            {
                Logger.LogError($"npm package {artifact.InstallSpec} does not include a published {ShrinkwrapFileName}; refusing to install.");
                return NpmCommandResult.Fail(shrinkwrapError);
            }

            // 5) Validate every dependency URL and integrity hash before npm can fetch the closure.
            var lockfileError = VerifyLockfileIntegrity(packageRoot);
            if (lockfileError is not null)
            {
                Logger.LogError($"npm package {artifact.InstallSpec} contains an untrusted lockfile: {lockfileError}");
                return NpmCommandResult.Fail(lockfileError);
            }

            // 6) npm ci installs the exact closure named in the shrinkwrap. It fails when the lockfile
            //    is missing or out of sync with package.json.
            var ciResult = await RunNpmAsync(invocation.Value, BuildCiArguments(artifact), packageRoot, artifact.InstallSpec, cancellationToken).ConfigureAwait(false);
            if (!ciResult.Succeeded)
            {
                return NpmCommandResult.Fail(ciResult.ErrorMessage ?? Resources.npm_runner_lockfile_untrusted);
            }

            // 7) Check the lockfile again after npm exits so promotion only uses the closure we approved.
            lockfileError = VerifyLockfileIntegrity(packageRoot);
            if (lockfileError is not null)
            {
                Logger.LogError($"npm ci {artifact.InstallSpec} produced an untrusted lockfile: {lockfileError}");
                return NpmCommandResult.Fail(lockfileError);
            }

            return NpmCommandResult.Ok(resolvedIntegrity);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError($"npm operation for {artifact.InstallSpec} threw: {ex.Message}");
            return NpmCommandResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// Builds the <c>npm pack</c> arguments for an approved artifact. The validated "name@version"
    /// spec goes through ArgumentList as one token so npm cannot read it as a flag. Lifecycle scripts
    /// are disabled, and any registry is passed as its own flag value pair.
    /// </summary>
    internal static IReadOnlyList<string> BuildPackArguments(NpmArtifact artifact, string packDestination)
    {
        var arguments = new List<string>
        {
            "pack",
            artifact.InstallSpec,
            "--ignore-scripts",
            "--no-audit",
            "--no-fund",
            "--loglevel=error",
            "--pack-destination",
            packDestination,
        };

        if (!string.IsNullOrWhiteSpace(artifact.Registry))
        {
            arguments.Add("--registry");
            arguments.Add(artifact.Registry);
        }

        return arguments;
    }

    /// <summary>
    /// Builds the <c>npm ci</c> arguments. npm ci installs the exact closure named in the lockfile and
    /// does not take a package spec, so it cannot resolve a range again. Lifecycle scripts are disabled,
    /// and any registry is passed as its own flag value pair.
    /// </summary>
    internal static IReadOnlyList<string> BuildCiArguments(NpmArtifact artifact)
    {
        var arguments = new List<string>
        {
            "ci",
            "--ignore-scripts",
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

    /// <summary>
    /// Returns null when the extracted package root contains the <c>npm-shrinkwrap.json</c> that
    /// freezes the dependency closure; otherwise returns a localized error. This gate rejects packages
    /// that did not freeze their transitive dependencies.
    /// </summary>
    internal static string? RequirePublisherShrinkwrap(string packageRoot)
    {
        var shrinkwrapPath = Path.Combine(packageRoot, ShrinkwrapFileName);
        return File.Exists(shrinkwrapPath) ? null : Resources.npm_runner_shrinkwrap_required;
    }

    /// <summary>
    /// Computes the sha512 Subresource Integrity value ("sha512-" + base64 digest) for the tarball at
    /// <paramref name="tarballPath"/>. This matches the value npm publishes for a registry tarball, so
    /// the caller can compare it to the catalog. Returns null when the file cannot be read.
    /// </summary>
    internal static string? ComputeTarballIntegrity(string tarballPath)
    {
        try
        {
            using var stream = File.OpenRead(tarballPath);
            var hash = SHA512.HashData(stream);
            return "sha512-" + Convert.ToBase64String(hash);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError($"Failed to read tarball {tarballPath} for integrity: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Locates the tarball <c>npm pack</c> wrote into <paramref name="packDirectory"/>. The directory
    /// is new for each install, so one .tgz is expected. Returns null when none is found.
    /// </summary>
    internal static string? FindPackedTarball(string packDirectory)
    {
        if (!Directory.Exists(packDirectory))
        {
            return null;
        }

        foreach (var candidate in Directory.EnumerateFiles(packDirectory, "*.tgz"))
        {
            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Extracts the gzip compressed npm tarball at <paramref name="tarballPath"/> so the published
    /// package becomes the root project at <paramref name="packageRoot"/>. npm tarballs place entries
    /// under "package/", so the archive is unpacked aside and that inner folder is moved into place.
    /// Returns null on success, or a localized error when the tarball cannot be read or lacks a package
    /// root.
    /// </summary>
    internal static string? TryExtractPackage(string tarballPath, string packageRoot)
    {
        var extractRoot = packageRoot + ".extract";
        try
        {
            if (Directory.Exists(extractRoot))
            {
                Directory.Delete(extractRoot, recursive: true);
            }

            Directory.CreateDirectory(extractRoot);

            using (var fileStream = File.OpenRead(tarballPath))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            {
                // ExtractToDirectory refuses to write outside extractRoot, so a malicious "../" path in
                // the tarball cannot escape staging.
                TarFile.ExtractToDirectory(gzipStream, extractRoot, overwriteFiles: true);
            }

            var innerPackage = Path.Combine(extractRoot, PackageRootDirectoryName);
            if (!Directory.Exists(innerPackage))
            {
                // Some publishers root the tarball under a different folder name. Use the single top
                // level directory when there is exactly one.
                innerPackage = ResolveSingleTopLevelDirectory(extractRoot) ?? string.Empty;
            }

            if (string.IsNullOrEmpty(innerPackage) || !Directory.Exists(innerPackage))
            {
                Logger.LogError($"Tarball {tarballPath} did not contain a package root.");
                return Resources.npm_runner_extract_failed;
            }

            if (Directory.Exists(packageRoot))
            {
                Directory.Delete(packageRoot, recursive: true);
            }

            Directory.Move(innerPackage, packageRoot);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Logger.LogError($"Failed to extract tarball {tarballPath}: {ex.Message}");
            return Resources.npm_runner_extract_failed;
        }
        finally
        {
            try
            {
                if (Directory.Exists(extractRoot))
                {
                    Directory.Delete(extractRoot, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.LogWarning($"Failed to clean tarball extract directory {extractRoot}: {ex.Message}");
            }
        }
    }

    private static string? ResolveSingleTopLevelDirectory(string root)
    {
        string? only = null;
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (only is not null)
            {
                return null;
            }

            only = directory;
        }

        return only;
    }

    /// <summary>
    /// Runs npm through node.exe and npm-cli.js so the npm.cmd batch shim stays out of the path.
    /// Bounds the wait so a hung npm cannot stall the UI. Returns success, a timeout message, or the
    /// captured stderr on a nonzero exit. Rethrows <see cref="OperationCanceledException"/> only for a
    /// caller cancel, not the timeout.
    /// </summary>
    private static async Task<NpmProcessResult> RunNpmAsync(NpmInvocation invocation, IReadOnlyList<string> arguments, string workingDirectory, string installSpec, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = invocation.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };

        // Launcher arguments first so node.exe runs npm itself. These paths were resolved by the
        // runner. Every untrusted argument goes through ArgumentList, not string concatenation.
        foreach (var launcherArgument in invocation.LauncherArguments)
        {
            psi.ArgumentList.Add(launcherArgument);
        }

        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        using var process = Process.Start(psi);
        if (process is null)
        {
            return NpmProcessResult.Fail(Resources.npm_runner_start_failed);
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

        // Bound the wait so a hung npm does not leave the UI stuck. The caller token and timeout share
        // one linked source; the catch below tells them apart.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(InstallTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await TerminateAndWaitAsync(process).ConfigureAwait(false);
            Logger.LogError($"npm {installSpec} timed out after {InstallTimeout.TotalMinutes:0} minutes.");
            return NpmProcessResult.Fail(string.Format(CultureInfo.CurrentCulture, TimedOutFormat, InstallTimeout.TotalMinutes.ToString("0", CultureInfo.CurrentCulture)));
        }
        catch (OperationCanceledException)
        {
            await TerminateAndWaitAsync(process).ConfigureAwait(false);
            throw;
        }

        if (process.ExitCode != 0)
        {
            var error = stderrBuilder.ToString().Trim();
            Logger.LogError($"npm {installSpec} failed (exit {process.ExitCode}): {error}");
            return NpmProcessResult.Fail(string.IsNullOrEmpty(error)
                ? string.Format(CultureInfo.CurrentCulture, FailedExitFormat, process.ExitCode)
                : error);
        }

        return NpmProcessResult.Ok();
    }

    /// <summary>
    /// The result of one npm invocation: success, or a localized or captured error message.
    /// </summary>
    private readonly record struct NpmProcessResult(bool Succeeded, string? ErrorMessage)
    {
        public static NpmProcessResult Ok() => new(true, null);

        public static NpmProcessResult Fail(string errorMessage) => new(false, errorMessage);
    }

    public bool RemoveDirectory(string targetDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
        {
            return true;
        }

        // Never recurse through a junction or symbolic link. Recursive delete could reach files outside
        // the extensions tree, so refuse when the directory itself is a reparse point.
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
    /// Resolves the lockfile npm consults inside <paramref name="projectRoot"/>, preferring the
    /// publishable npm-shrinkwrap.json over package-lock.json. Returns null when neither file exists.
    /// </summary>
    private static string? ResolveLockfilePath(string projectRoot)
    {
        foreach (var lockfileName in LockfileNames)
        {
            var candidate = Path.Combine(projectRoot, lockfileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
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

                // Give the OS a bounded moment to tear the tree down before staging cleanup tries to
                // delete files that still have handles open.
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
            // If the attributes cannot be read, play it safe and treat the path as unsafe.
            return true;
        }
    }

    /// <summary>
    /// A resolved npm launcher: node.exe plus the leading arguments that make it run npm. The install
    /// spec and flags are appended after these.
    /// </summary>
    internal readonly record struct NpmInvocation(string FileName, IReadOnlyList<string> LauncherArguments);

    /// <summary>
    /// Verifies that every dependency in the frozen lockfile inside <paramref name="projectRoot"/>
    /// came from an approved registry over HTTPS and carries a supported Subresource Integrity hash.
    /// The publisher's npm-shrinkwrap.json is preferred over package-lock.json. Returns null when the
    /// tree is trusted, or a localized error for the first untrusted resolution. A missing or unreadable
    /// lockfile is treated as untrusted.
    /// </summary>
    internal static string? VerifyLockfileIntegrity(string projectRoot)
    {
        var lockfilePath = ResolveLockfilePath(projectRoot);
        if (lockfilePath is null)
        {
            return Resources.npm_runner_lockfile_untrusted;
        }

        try
        {
            using var stream = File.OpenRead(lockfilePath);
            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            // lockfileVersion 2 and 3 use a "packages" map keyed by install path. The root package has
            // an empty key and no resolution of its own. A "link": true entry points at a local
            // workspace and is skipped. Every other entry must carry a trusted resolved URL and hash.
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

            // lockfileVersion 1 uses a nested "dependencies" tree. Walk it recursively.
            if (root.TryGetProperty("dependencies", out var dependencies) && dependencies.ValueKind == JsonValueKind.Object)
            {
                return VerifyLegacyDependencies(dependencies) ? null : Resources.npm_runner_lockfile_untrusted;
            }

            // Neither shape is present. Nothing was pinned, so the tree cannot be trusted.
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

            // A "bundled" dependency ships inside its parent's tarball and has no resolution of its own.
            // The parent's integrity already covers it. Anything else must be trusted.
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
    /// Resolves node.exe and npm-cli.js so npm can run without the npm.cmd batch shim. Probes PATH for
    /// node.exe, then looks for npm-cli.js next to node.exe and under the global npm prefix reported by
    /// the environment. Returns null when either piece cannot be found.
    /// </summary>
    internal static NpmInvocation? ResolveNpmInvocation() =>
        ResolveNpmInvocation(GetPathDirectories(), includeUserPrefix: true);

    internal static NpmInvocation? ResolveNpmInvocation(IReadOnlyList<string> pathDirectories) =>
        ResolveNpmInvocation(pathDirectories, includeUserPrefix: false);

    private static NpmInvocation? ResolveNpmInvocation(IReadOnlyList<string> pathDirectories, bool includeUserPrefix)
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
                // Malformed PATH entry. Skip it.
                continue;
            }

            if (!File.Exists(nodeCandidate))
            {
                continue;
            }

            var npmCli = FindNpmCli(directory, includeUserPrefix);
            if (npmCli is not null)
            {
                return new NpmInvocation(nodeCandidate, new[] { npmCli });
            }
        }

        return null;
    }

    private static string? FindNpmCli(string nodeDirectory, bool includeUserPrefix)
    {
        // Standard Windows Node.js layout: npm-cli.js sits under the same directory as node.exe.
        foreach (var candidateRoot in EnumerateNpmPrefixCandidates(nodeDirectory, includeUserPrefix))
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

    private static IEnumerable<string> EnumerateNpmPrefixCandidates(string nodeDirectory, bool includeUserPrefix)
    {
        // node.exe's own directory (Program Files\nodejs) is the usual prefix on Windows.
        yield return nodeDirectory;

        if (!includeUserPrefix)
        {
            yield break;
        }

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
