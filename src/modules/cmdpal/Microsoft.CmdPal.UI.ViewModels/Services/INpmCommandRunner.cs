// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Wraps npm and the directory changes the installer relies on. Tests use this seam to cover
/// the install and uninstall flow without running npm or touching a real registry.
/// </summary>
public interface INpmCommandRunner
{
    /// <summary>
    /// Gets a value indicating whether the npm executable can be found on the machine.
    /// </summary>
    /// <returns><see langword="true"/> when npm is available; otherwise, <see langword="false"/>.</returns>
    bool IsNpmAvailable();

    /// <summary>
    /// Installs the approved artifact into <paramref name="stagingDirectory"/> using npm. The
    /// "name@version" spec is passed as one argument, package lifecycle scripts are disabled, and the
    /// result carries npm's resolved integrity so the caller can compare it before promoting.
    /// </summary>
    /// <param name="stagingDirectory">A directory outside the watched extensions root that npm installs into.</param>
    /// <param name="artifact">The validated artifact to install.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of running npm, including the resolved integrity on success.</returns>
    Task<NpmCommandResult> InstallAsync(string stagingDirectory, NpmArtifact artifact, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes <paramref name="targetDirectory"/> and everything under it. Refuses to delete when the
    /// directory itself is a symbolic link or junction, and retries briefly when a file handle is still
    /// being released. Safe to call when the directory does not exist.
    /// </summary>
    /// <param name="targetDirectory">The directory to remove.</param>
    /// <param name="cancellationToken">A token to cancel the retry loop between attempts.</param>
    /// <returns>
    /// <see langword="true"/> when the directory no longer exists after the call (either it was
    /// deleted or it never existed); <see langword="false"/> when deletion failed or was refused and
    /// the directory remains on disk.
    /// </returns>
    bool RemoveDirectory(string targetDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// The result of running an npm command.
/// </summary>
/// <param name="Succeeded">Whether the command exited successfully.</param>
/// <param name="ErrorMessage">A message to show when the command failed; otherwise null.</param>
/// <param name="ResolvedIntegrity">
/// The Subresource Integrity value npm resolved for the installed package, read from the generated
/// lockfile. Null when the command failed or the value could not be found.
/// </param>
public readonly record struct NpmCommandResult(bool Succeeded, string? ErrorMessage, string? ResolvedIntegrity)
{
    public static NpmCommandResult Ok(string? resolvedIntegrity) => new(true, null, resolvedIntegrity);

    public static NpmCommandResult Fail(string errorMessage) => new(false, errorMessage, null);
}
