// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Abstracts the npm command line and the directory side effects the installer relies on. This
/// is the seam that lets tests exercise the install/uninstall transaction without invoking npm
/// or touching a real registry.
/// </summary>
public interface INpmCommandRunner
{
    /// <summary>
    /// Gets a value indicating whether the npm executable can be found on the machine.
    /// </summary>
    /// <returns><see langword="true"/> when npm is available; otherwise, <see langword="false"/>.</returns>
    bool IsNpmAvailable();

    /// <summary>
    /// Installs the approved artifact into <paramref name="stagingDirectory"/> using npm. The exact
    /// spec "name@version" is passed as a single argument, package lifecycle scripts are disabled, and
    /// the exact version is pinned. On success the result carries the integrity value npm resolved for
    /// the top-level package so the caller can verify it against the approved value before promoting.
    /// </summary>
    /// <param name="stagingDirectory">A directory outside the watched extensions root that npm installs into.</param>
    /// <param name="artifact">The validated artifact to install.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of running npm, including the resolved integrity on success.</returns>
    Task<NpmCommandResult> InstallAsync(string stagingDirectory, NpmArtifact artifact, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes <paramref name="targetDirectory"/> and everything under it. Refuses to delete when the
    /// top-level directory is a symbolic link or junction (a reparse point), and retries briefly to
    /// tolerate a file handle that is still being released. Safe to call when the directory does not
    /// exist.
    /// </summary>
    /// <param name="targetDirectory">The directory to remove.</param>
    /// <returns>
    /// <see langword="true"/> when the directory no longer exists after the call (either it was
    /// deleted or it never existed); <see langword="false"/> when deletion failed or was refused and
    /// the directory remains on disk.
    /// </returns>
    bool RemoveDirectory(string targetDirectory);
}

/// <summary>
/// The outcome of running an npm command.
/// </summary>
/// <param name="Succeeded">Whether the command exited successfully.</param>
/// <param name="ErrorMessage">A user-visible error message when the command failed; otherwise null.</param>
/// <param name="ResolvedIntegrity">
/// The Subresource Integrity value npm resolved for the installed top-level package, read from the
/// generated lockfile. Null when the command failed or the value could not be determined.
/// </param>
public readonly record struct NpmCommandResult(bool Succeeded, string? ErrorMessage, string? ResolvedIntegrity)
{
    public static NpmCommandResult Ok() => new(true, null, null);

    public static NpmCommandResult Ok(string? resolvedIntegrity) => new(true, null, resolvedIntegrity);

    public static NpmCommandResult Fail(string errorMessage) => new(false, errorMessage, null);
}
