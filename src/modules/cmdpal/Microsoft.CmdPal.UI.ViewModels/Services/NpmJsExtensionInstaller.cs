// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Installs and uninstalls gallery jsonrpc extensions by driving npm into the Phase 4
/// JSExtensions directory. Load/unload is left to the <see cref="JsonRpcExtensionService"/>
/// FileSystemWatcher; this type only prepares the directory contents and terminates the
/// running process on uninstall.
/// </summary>
public sealed class NpmJsExtensionInstaller : IJsExtensionInstaller
{
    private readonly IJsExtensionHost _host;
    private readonly INpmCommandRunner _npmCommandRunner;

    public NpmJsExtensionInstaller(IJsExtensionHost host, INpmCommandRunner npmCommandRunner)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(npmCommandRunner);

        _host = host;
        _npmCommandRunner = npmCommandRunner;
    }

    public async Task<JsExtensionInstallResult> InstallAsync(string extensionName, string npmPackage, string? registry, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTargetDirectory(extensionName, out var targetDirectory))
        {
            return JsExtensionInstallResult.Fail("The extension name is not valid.");
        }

        if (string.IsNullOrWhiteSpace(npmPackage))
        {
            return JsExtensionInstallResult.Fail("The extension does not specify an npm package.");
        }

        if (!_npmCommandRunner.IsNpmAvailable())
        {
            return JsExtensionInstallResult.Fail("npm was not found. Install Node.js to add JavaScript extensions.");
        }

        var result = await _npmCommandRunner.InstallAsync(targetDirectory, npmPackage, registry, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            // Leave nothing half-installed behind so the watcher does not pick up a broken directory.
            _npmCommandRunner.RemoveDirectory(targetDirectory);
            return JsExtensionInstallResult.Fail(result.ErrorMessage ?? "The installation failed.");
        }

        Logger.LogInfo($"Installed JS extension '{extensionName}' from npm package '{npmPackage}'.");
        return JsExtensionInstallResult.Ok();
    }

    public Task<JsExtensionInstallResult> UninstallAsync(string extensionName, CancellationToken cancellationToken = default)
    {
        if (!TryResolveTargetDirectory(extensionName, out var targetDirectory))
        {
            return Task.FromResult(JsExtensionInstallResult.Fail("The extension name is not valid."));
        }

        // Terminate the Node.js process first so its file handles are released, then delete the
        // directory. The FileSystemWatcher unloads the provider once the directory is gone.
        _host.StopExtension(targetDirectory);
        _npmCommandRunner.RemoveDirectory(targetDirectory);

        Logger.LogInfo($"Uninstalled JS extension '{extensionName}'.");
        return Task.FromResult(JsExtensionInstallResult.Ok());
    }

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
        var candidate = Path.GetFullPath(Path.Combine(root, trimmed));
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targetDirectory = candidate;
        return true;
    }
}
