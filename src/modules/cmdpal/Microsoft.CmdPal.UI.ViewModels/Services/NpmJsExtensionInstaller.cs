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
/// Installs and uninstalls gallery jsonrpc extensions as a transaction around an approved artifact.
/// The installer validates the catalog data, stages the package outside JSExtensions, asks the runner
/// to install the frozen dependency tree, verifies identity and integrity, promotes the ready tree,
/// and waits for host registration. Staging is cleaned every time. Failed, timed out, and canceled
/// installs leave any existing install alone.
/// </summary>
public sealed class NpmJsExtensionInstaller : IJsExtensionInstaller
{
    // Upper bound on how long to wait for the host to load and register a freshly promoted extension.
    private static readonly TimeSpan RegistrationTimeout = TimeSpan.FromSeconds(30);

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        "COM1",
        "COM2",
        "COM3",
        "COM4",
        "COM5",
        "COM6",
        "COM7",
        "COM8",
        "COM9",
        "LPT1",
        "LPT2",
        "LPT3",
        "LPT4",
        "LPT5",
        "LPT6",
        "LPT7",
        "LPT8",
        "LPT9",
    };

    private readonly IJsExtensionHost _host;
    private readonly INpmCommandRunner _npmCommandRunner;

    // Per directory locks keep install and uninstall for the same extension serialized while
    // different extensions can still install in parallel.
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

        // Fail closed. An incomplete or malformed catalog entry is never installable.
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
                // Stop the Node.js process before delete so file handles are released. The token lets
                // Cancel stop waiting on a busy lifecycle gate without blocking the UI thread.
                await _host.StopExtensionAsync(targetDirectory, cancellationToken).ConfigureAwait(false);

                // Delete on a worker thread since process handles can take a moment to close.
                if (!await RemoveDirectoryAsync(targetDirectory).ConfigureAwait(false))
                {
                    await _host.RefreshAndAwaitProviderAsync(targetDirectory, RegistrationTimeout, CancellationToken.None).ConfigureAwait(false);
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
        // Upgrade policy: do not install over an existing or loaded extension. The user uninstalls and
        // reinstalls to change versions, so a failed upgrade cannot damage a working install.
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

            var packageDirectory = Path.Combine(stagingDirectory, NpmCommandRunner.PackageRootDirectoryName);
            if (!Directory.Exists(packageDirectory))
            {
                Logger.LogError($"Installed npm package '{artifact.Package}' but its extracted root was not found under {stagingDirectory}.");
                return JsExtensionInstallResult.Fail(Resources.npm_installer_not_an_extension);
            }

            // Validate that this is the approved package and version, with a manifest the host can load.
            // The parser enforces the entry point, extension allowlist, and canonical containment.
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

            File.WriteAllText(Path.Combine(packageDirectory, JsonRpcExtensionService.GalleryInstallMarkerFileName), string.Empty);

            // The extracted package root already has the layout discovery expects: package.json at the
            // root with the frozen dependency closure under its own node_modules. Promote it directly.
            //
            // Keep the promote on the same volume. The target does not exist, so this is a plain rename,
            // and the watched root only sees the fully validated tree.
            Directory.CreateDirectory(_host.ExtensionsRootPath);
            Directory.Move(packageDirectory, targetDirectory);
            promoted = true;

            // Only report success after the host loads and registers the provider.
            var registered = await _host.RefreshAndAwaitProviderAsync(targetDirectory, RegistrationTimeout, cancellationToken).ConfigureAwait(false);
            if (!registered)
            {
                Logger.LogError($"Promoted '{extensionName}' but the host did not register a provider within {RegistrationTimeout.TotalSeconds:0} seconds.");
                if (await RollbackPromotedInstallAsync(targetDirectory).ConfigureAwait(false))
                {
                    promoted = false;
                }

                return JsExtensionInstallResult.Fail(Resources.npm_installer_not_discoverable);
            }

            TryRemoveInstallMarker(targetDirectory);
            Logger.LogInfo($"Installed JS extension '{extensionName}' from npm package '{artifact.InstallSpec}'.");
            return JsExtensionInstallResult.Ok();
        }
        catch (OperationCanceledException)
        {
            // A cancel after promotion must not leave a half registered extension behind. Stop the host
            // process and provider first, then remove the promoted tree.
            if (promoted)
            {
                await RollbackPromotedInstallAsync(targetDirectory).ConfigureAwait(false);
            }

            return JsExtensionInstallResult.Fail(Resources.npm_installer_canceled);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (promoted)
            {
                await RollbackPromotedInstallAsync(targetDirectory).ConfigureAwait(false);
            }

            Logger.LogError($"Install of '{extensionName}' failed: {ex.Message}");
            return JsExtensionInstallResult.Fail(Resources.npm_installer_install_failed);
        }
        finally
        {
            // Clean the staging tree on every path, even after cancel. Do not observe the caller token
            // here, because cleanup still needs to run.
            if (!await RemoveDirectoryAsync(stagingDirectory).ConfigureAwait(false))
            {
                Logger.LogWarning($"Failed to clean up staging directory {stagingDirectory}.");
            }
        }
    }

    /// <summary>
    /// Rolls back a promoted install. Stops any host process and provider for the promoted directory,
    /// then removes the promoted tree, in that order, so a canceled or failed install cannot leave the
    /// extension both installed and running. Uses no cancellation token so cleanup can finish.
    /// </summary>
    /// <returns><see langword="true"/> when the promoted directory was removed; otherwise, <see langword="false"/>.</returns>
    private async Task<bool> RollbackPromotedInstallAsync(string targetDirectory)
    {
        await _host.StopExtensionAsync(targetDirectory, CancellationToken.None).ConfigureAwait(false);
        var removed = await RemoveDirectoryAsync(targetDirectory).ConfigureAwait(false);
        if (!removed)
        {
            TryRemoveInstallMarker(targetDirectory);
        }

        return removed;
    }

    private Task<bool> RemoveDirectoryAsync(string targetDirectory) =>
        Task.Run(() => _npmCommandRunner.RemoveDirectory(targetDirectory, CancellationToken.None));

    private static void TryRemoveInstallMarker(string targetDirectory)
    {
        var markerPath = Path.Combine(targetDirectory, JsonRpcExtensionService.GalleryInstallMarkerFileName);
        try
        {
            File.Delete(markerPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogWarning($"Failed to remove gallery install marker '{markerPath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the staging root, a sibling of JSExtensions that the FileSystemWatcher does not watch.
    /// Keeping it on the same volume makes the final promote a rename instead of a copy.
    /// </summary>
    private string GetStagingRoot()
    {
        var root = Path.GetFullPath(_host.ExtensionsRootPath);
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root));
        var rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(root));

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(rootName))
        {
            // Root of a volume. Fall back to a sibling under the same directory.
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

            // Keep the semaphore until no operation holds it or waits on it.
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
        var separatorIndex = trimmed.IndexOf('.');
        var deviceName = separatorIndex >= 0 ? trimmed[..separatorIndex] : trimmed;
        if (!string.Equals(trimmed, extensionName, StringComparison.Ordinal)
            || trimmed.EndsWith('.')
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || trimmed is "." or ".."
            || ReservedWindowsNames.Contains(deviceName)
            || Path.IsPathRooted(trimmed))
        {
            return false;
        }

        var root = _host.ExtensionsRootPath;
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        // The extensions root must be a real directory, not a reparse point. A junction or symbolic
        // link can pass the text containment check and still resolve outside the intended tree, letting
        // uninstall delete escape containment. Refuse roots that resolve somewhere else.
        if (!RootResolvesToItself(normalizedRoot))
        {
            Logger.LogError($"Refusing to resolve a target under extensions root '{normalizedRoot}' because the root is a reparse point (junction or symbolic link).");
            return false;
        }

        string candidate;
        try
        {
            candidate = Path.GetFullPath(Path.Combine(normalizedRoot, trimmed));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(candidate), trimmed, StringComparison.OrdinalIgnoreCase)
            || (Directory.Exists(candidate) && !DirectoryResolvesToItself(candidate)))
        {
            return false;
        }

        targetDirectory = candidate;
        return true;
    }

    private static bool RootResolvesToItself(string normalizedRoot) => DirectoryResolvesToItself(normalizedRoot);

    private static bool DirectoryResolvesToItself(string directory)
    {
        // A root that does not exist yet cannot redirect anywhere. Install creates it as a real
        // directory before promoting into it.
        if (!Directory.Exists(directory))
        {
            return true;
        }

        try
        {
            // ResolveLinkTarget returns null when the path is not a reparse point. Any reparse point on
            // the root is treated as unsafe.
            return Directory.ResolveLinkTarget(directory, returnFinalTarget: true) is null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // If the root cannot be inspected, play it safe and refuse.
            Logger.LogError($"Failed to inspect extension directory '{directory}' for reparse points: {ex.Message}");
            return false;
        }
    }
}
