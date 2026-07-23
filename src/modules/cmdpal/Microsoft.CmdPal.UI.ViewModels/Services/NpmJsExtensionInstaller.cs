// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Properties;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Installs and uninstalls gallery jsonrpc extensions as a fail-closed transaction over an
/// immutable, approved artifact. The package, exact version, integrity, and optional registry are
/// validated up front; npm installs into a unique staging directory outside the watched JSExtensions
/// root with lifecycle scripts disabled; the resolved integrity, package identity, and manifest are
/// verified; and only then is the extension atomically promoted into JSExtensions/&lt;name&gt;/ and
/// awaited for host registration. Staging is always cleaned up, and a failed, timed-out, or cancelled
/// install never deletes or corrupts an existing install.
/// </summary>
public sealed class NpmJsExtensionInstaller : IJsExtensionInstaller
{
    // Upper bound on how long to wait for the host to load and register a freshly promoted extension.
    private static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(30);

    private readonly IJsExtensionHost _host;
    private readonly INpmCommandRunner _npmCommandRunner;

    // Per-canonical-directory locks so an install and an uninstall of the same extension are
    // serialized while different extensions install in parallel.
    private readonly Dictionary<string, SemaphoreSlim> _directoryLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _lockReferenceCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _directoryLocksGate = new();

    public NpmJsExtensionInstaller(IJsExtensionHost host, INpmCommandRunner npmCommandRunner)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(npmCommandRunner);

        _host = host;
        _npmCommandRunner = npmCommandRunner;
    }

    public async Task<JsExtensionInstallResult> InstallAsync(string extensionName, string npmPackage, string? version, string? integrity, string? registry, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTargetDirectory(extensionName, out var targetDirectory))
        {
            return JsExtensionInstallResult.Fail(Resources.npm_installer_invalid_name);
        }

        // Fail closed: an incomplete or malformed catalog entry is never installable.
        if (!NpmArtifact.TryCreate(npmPackage, version, integrity, registry, out var artifact, out var validationError)
            || artifact is null)
        {
            return JsExtensionInstallResult.Fail(MapValidationError(validationError));
        }

        if (!_npmCommandRunner.IsNpmAvailable())
        {
            return JsExtensionInstallResult.Fail(Resources.npm_runner_npm_not_found);
        }

        var lockKey = CanonicalKey(targetDirectory);
        var gate = AcquireDirectoryLock(lockKey);
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await InstallLockedAsync(extensionName, targetDirectory, artifact, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return JsExtensionInstallResult.Fail(Resources.npm_installer_canceled);
        }
        finally
        {
            ReleaseDirectoryLock(lockKey);
        }
    }

    public async Task<JsExtensionInstallResult> UninstallAsync(string extensionName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTargetDirectory(extensionName, out var targetDirectory))
        {
            return JsExtensionInstallResult.Fail(Resources.npm_installer_invalid_name);
        }

        var lockKey = CanonicalKey(targetDirectory);
        var gate = AcquireDirectoryLock(lockKey);
        try
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Terminate the Node.js process and wait for its handles to be released before
                // deleting; the Phase 4 StopExtension blocks until the process has exited. The token
                // lets Cancel abandon a wait for a contended lifecycle gate.
                _host.StopExtension(targetDirectory, cancellationToken);

                // RemoveDirectory refuses to follow a junction or symbolic link out of the root and
                // retries briefly to tolerate a handle that is still being released. The token stops
                // the retry loop so Cancel is honored between attempts.
                if (!_npmCommandRunner.RemoveDirectory(targetDirectory, cancellationToken))
                {
                    Logger.LogError($"Uninstall of JS extension '{extensionName}' failed: could not delete {targetDirectory}.");
                    return JsExtensionInstallResult.Fail(Resources.npm_installer_remove_failed);
                }

                Logger.LogInfo($"Uninstalled JS extension '{extensionName}'.");
                return JsExtensionInstallResult.Ok();
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return JsExtensionInstallResult.Fail(Resources.npm_installer_canceled);
        }
        finally
        {
            ReleaseDirectoryLock(lockKey);
        }
    }

    public bool IsInstalled(string extensionName) => _host.IsExtensionInstalled(extensionName);

    private async Task<JsExtensionInstallResult> InstallLockedAsync(string extensionName, string targetDirectory, NpmArtifact artifact, CancellationToken cancellationToken)
    {
        // Upgrade policy: block installing over an existing or loaded extension. The user uninstalls
        // and reinstalls to change versions, which guarantees an existing install is never touched by
        // a failed upgrade.
        if (Directory.Exists(targetDirectory) || _host.IsExtensionInstalled(extensionName))
        {
            return JsExtensionInstallResult.Fail(Resources.npm_installer_already_installed);
        }

        var stagingRoot = GetStagingRoot();
        var stagingDirectory = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        var promoted = false;

        try
        {
            var result = await _npmCommandRunner.InstallAsync(stagingDirectory, artifact, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return JsExtensionInstallResult.Fail(result.ErrorMessage ?? Resources.npm_installer_install_failed);
            }

            // Verify the tarball npm resolved is exactly the approved one before anything is promoted.
            if (string.IsNullOrEmpty(result.ResolvedIntegrity)
                || !string.Equals(result.ResolvedIntegrity, artifact.Integrity, StringComparison.Ordinal))
            {
                Logger.LogError($"Integrity mismatch installing '{artifact.InstallSpec}': expected {artifact.Integrity}, npm resolved {result.ResolvedIntegrity ?? "(none)"}.");
                return JsExtensionInstallResult.Fail(Resources.npm_installer_integrity_mismatch);
            }

            var packageDirectory = GetInstalledPackageDirectory(stagingDirectory, artifact.Package);
            if (!Directory.Exists(packageDirectory))
            {
                Logger.LogError($"Installed npm package '{artifact.Package}' but its directory was not found under {stagingDirectory}.");
                return JsExtensionInstallResult.Fail(Resources.npm_installer_not_an_extension);
            }

            // Validate that this is the approved package and version, and that it is a loadable
            // CmdPal manifest (the parser enforces the entry point, the .js/.mjs/.cjs allowlist, and
            // reparse/canonical containment).
            var manifestPath = Path.Combine(packageDirectory, "package.json");
            var parseResult = JSExtensionManifest.TryParseFile(manifestPath);
            if (!parseResult.IsValid || parseResult.Manifest is null)
            {
                Logger.LogError($"Installed package '{artifact.Package}' is not a usable CmdPal extension: {parseResult.FailureReason}");
                return JsExtensionInstallResult.Fail(Resources.npm_installer_not_an_extension);
            }

            var manifest = parseResult.Manifest;
            if (!string.Equals(manifest.Name?.Trim(), artifact.Package, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError($"Manifest identity mismatch: approved package '{artifact.Package}' but manifest declares '{manifest.Name}'.");
                return JsExtensionInstallResult.Fail(Resources.npm_installer_identity_mismatch);
            }

            if (!string.Equals(manifest.Version?.Trim(), artifact.Version, StringComparison.Ordinal))
            {
                Logger.LogError($"Manifest version mismatch: approved version '{artifact.Version}' but manifest declares '{manifest.Version}'.");
                return JsExtensionInstallResult.Fail(Resources.npm_installer_version_mismatch);
            }

            // Assemble the discovery layout inside staging: the package at the root with its (hoisted)
            // dependencies under its own node_modules.
            var assembledDirectory = AssembleDiscoveryLayout(stagingDirectory, packageDirectory);

            // Atomic promote onto the same volume. The target does not exist (blocked above), so this
            // is a plain rename; the watched root only ever sees the fully validated tree.
            Directory.CreateDirectory(_host.ExtensionsRootPath);
            Directory.Move(assembledDirectory, targetDirectory);
            promoted = true;

            // Only report success once the host has actually loaded and registered the provider.
            var registered = await _host.RefreshAndAwaitProviderAsync(targetDirectory, RegistrationTimeout, cancellationToken).ConfigureAwait(false);
            if (!registered)
            {
                Logger.LogError($"Promoted '{extensionName}' but the host did not register a provider within {RegistrationTimeout.TotalSeconds:0} seconds.");
                if (RollbackPromotedInstall(targetDirectory))
                {
                    promoted = false;
                }

                return JsExtensionInstallResult.Fail(Resources.npm_installer_not_discoverable);
            }

            Logger.LogInfo($"Installed JS extension '{extensionName}' from npm package '{artifact.InstallSpec}'.");
            return JsExtensionInstallResult.Ok();
        }
        catch (OperationCanceledException)
        {
            // A cancel after promotion must not leave a half-registered extension behind: stop the
            // host process/provider first, then remove the promoted tree.
            if (promoted)
            {
                RollbackPromotedInstall(targetDirectory);
            }

            return JsExtensionInstallResult.Fail(Resources.npm_installer_canceled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (promoted)
            {
                RollbackPromotedInstall(targetDirectory);
            }

            Logger.LogError($"Install of '{extensionName}' failed: {ex.Message}");
            return JsExtensionInstallResult.Fail(Resources.npm_installer_install_failed);
        }
        finally
        {
            // Clean the staging tree on every path, even when the install was canceled, so the staging
            // cleanup must not observe the caller's token: pass CancellationToken.None explicitly.
            if (!_npmCommandRunner.RemoveDirectory(stagingDirectory, CancellationToken.None))
            {
                Logger.LogWarning($"Failed to clean up staging directory {stagingDirectory}.");
            }
        }
    }

    /// <summary>
    /// Rolls back a promoted install. Stops the host process/provider that may have started for the
    /// promoted directory, then removes the promoted tree, in that order, so a canceled or failed
    /// install can never leave the extension both installed on disk and running. Uses no cancellation
    /// token so cleanup always runs to completion, even on the cancel path.
    /// </summary>
    /// <returns><see langword="true"/> when the promoted directory was removed; otherwise, <see langword="false"/>.</returns>
    private bool RollbackPromotedInstall(string targetDirectory)
    {
        _host.StopExtension(targetDirectory);
        return _npmCommandRunner.RemoveDirectory(targetDirectory);
    }

    /// <summary>
    /// Materializes the discovery layout for a promoted extension inside the staging tree. The
    /// installed package becomes the extension root, and the sibling dependencies npm hoisted to the
    /// top-level node_modules are moved under the package's own node_modules so Node.js can resolve
    /// them after promotion.
    /// </summary>
    private static string AssembleDiscoveryLayout(string stagingDirectory, string packageDirectory)
    {
        var assembledDirectory = Path.Combine(stagingDirectory, "__cmdpal_assembled");
        if (Directory.Exists(assembledDirectory))
        {
            Directory.Delete(assembledDirectory, recursive: true);
        }

        // Move the package itself to the assembled root.
        Directory.Move(packageDirectory, assembledDirectory);

        // Move every remaining hoisted dependency into the package's own node_modules.
        var topLevelNodeModules = Path.Combine(stagingDirectory, "node_modules");
        if (Directory.Exists(topLevelNodeModules))
        {
            var assembledNodeModules = Path.Combine(assembledDirectory, "node_modules");
            Directory.CreateDirectory(assembledNodeModules);

            foreach (var entry in Directory.EnumerateFileSystemEntries(topLevelNodeModules))
            {
                var name = Path.GetFileName(entry);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var destination = Path.Combine(assembledNodeModules, name);
                if (Directory.Exists(entry))
                {
                    if (!Directory.Exists(destination))
                    {
                        Directory.Move(entry, destination);
                    }
                }
                else if (!File.Exists(destination))
                {
                    File.Move(entry, destination);
                }
            }
        }

        return assembledDirectory;
    }

    /// <summary>
    /// Resolves the directory npm installed the top-level package into, handling both scoped
    /// (@scope/name) and unscoped package names.
    /// </summary>
    private static string GetInstalledPackageDirectory(string stagingDirectory, string package)
    {
        var relative = package.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(stagingDirectory, "node_modules", relative);
    }

    /// <summary>
    /// Gets the staging root, a sibling of the JSExtensions root that the FileSystemWatcher does not
    /// watch. Keeping it a sibling (same volume) makes the final promote a rename rather than a copy.
    /// </summary>
    private string GetStagingRoot()
    {
        var root = Path.GetFullPath(_host.ExtensionsRootPath);
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root));
        var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(rootName))
        {
            // Degenerate path (root of a volume); fall back to a sibling under the same directory.
            return Path.Combine(root + ".staging");
        }

        return Path.Combine(parent, rootName + ".staging");
    }

    private static string CanonicalKey(string directory) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)).ToLowerInvariant();

    private SemaphoreSlim AcquireDirectoryLock(string canonicalKey)
    {
        lock (_directoryLocksGate)
        {
            if (!_directoryLocks.TryGetValue(canonicalKey, out var entry))
            {
                entry = new SemaphoreSlim(1, 1);
                _directoryLocks[canonicalKey] = entry;
            }

            // Track a reference count in the semaphore's current use so the dictionary entry is only
            // removed when no operation holds or waits on it.
            _lockReferenceCounts.TryGetValue(canonicalKey, out var count);
            _lockReferenceCounts[canonicalKey] = count + 1;
            return entry;
        }
    }

    private void ReleaseDirectoryLock(string canonicalKey)
    {
        lock (_directoryLocksGate)
        {
            if (!_lockReferenceCounts.TryGetValue(canonicalKey, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _lockReferenceCounts.Remove(canonicalKey);
                if (_directoryLocks.Remove(canonicalKey, out var entry))
                {
                    entry.Dispose();
                }
            }
            else
            {
                _lockReferenceCounts[canonicalKey] = count - 1;
            }
        }
    }

    private static string MapValidationError(NpmArtifactValidationError error) => error switch
    {
        NpmArtifactValidationError.PackageMissing => Resources.npm_installer_package_missing,
        NpmArtifactValidationError.PackageInvalid => Resources.npm_installer_package_invalid,
        NpmArtifactValidationError.VersionMissing => Resources.npm_installer_version_missing,
        NpmArtifactValidationError.VersionInvalid => Resources.npm_installer_version_invalid,
        NpmArtifactValidationError.IntegrityMissing => Resources.npm_installer_integrity_missing,
        NpmArtifactValidationError.IntegrityInvalid => Resources.npm_installer_integrity_invalid,
        NpmArtifactValidationError.RegistryInvalid => Resources.npm_installer_registry_invalid,
        _ => Resources.npm_installer_install_failed,
    };

    private bool TryResolveTargetDirectory(string extensionName, out string targetDirectory)
    {
        targetDirectory = string.Empty;

        if (string.IsNullOrWhiteSpace(extensionName))
        {
            return false;
        }

        // Guard against path traversal or absolute paths escaping the JSExtensions root.
        var trimmed = extensionName.Trim();
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed is "." or ".."
            || Path.IsPathRooted(trimmed))
        {
            return false;
        }

        var root = _host.ExtensionsRootPath;
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        // The extensions root must be a real directory, not a reparse point. If a junction or symbolic
        // link is used as the root, a candidate that passes the textual containment check below can
        // still resolve on disk to a path outside the intended tree, so an uninstall delete could
        // escape containment. Resolve reparse points on the root and refuse when it does not resolve
        // to itself.
        if (!RootResolvesToItself(normalizedRoot))
        {
            Logger.LogError($"Refusing to resolve a target under extensions root '{normalizedRoot}' because the root is a reparse point (junction or symbolic link).");
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, trimmed));
        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targetDirectory = candidate;
        return true;
    }

    private static bool RootResolvesToItself(string normalizedRoot)
    {
        // A root that does not exist yet cannot redirect anywhere; install creates it as a real
        // directory before promoting into it.
        if (!Directory.Exists(normalizedRoot))
        {
            return true;
        }

        try
        {
            // ResolveLinkTarget returns null when the path is not a reparse point, and the final
            // target otherwise. Any reparse point on the root is treated as unsafe.
            return Directory.ResolveLinkTarget(normalizedRoot, returnFinalTarget: true) is null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // If the root cannot be inspected, err on the side of caution and refuse.
            Logger.LogError($"Failed to inspect extensions root '{normalizedRoot}' for reparse points: {ex.Message}");
            return false;
        }
    }
}
