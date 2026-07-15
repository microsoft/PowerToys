// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Abstracts the npm command line and the directory side effects the installer relies on. This
/// is the seam that lets tests exercise install/uninstall path resolution without invoking npm
/// or touching disk.
/// </summary>
public interface INpmCommandRunner
{
    /// <summary>
    /// Gets a value indicating whether the npm executable can be found on the machine.
    /// </summary>
    /// <returns><see langword="true"/> when npm is available; otherwise, <see langword="false"/>.</returns>
    bool IsNpmAvailable();

    /// <summary>
    /// Runs "npm install &lt;package&gt;" in <paramref name="targetDirectory"/>, creating the
    /// directory if needed.
    /// </summary>
    /// <param name="targetDirectory">The extension directory the package is installed into.</param>
    /// <param name="package">The npm package identifier to install.</param>
    /// <param name="registry">Optional npm registry URL. When null or empty, the default registry is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of running npm.</returns>
    Task<NpmCommandResult> InstallAsync(string targetDirectory, string package, string? registry, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes <paramref name="targetDirectory"/> and everything under it. Safe to call when the
    /// directory does not exist.
    /// </summary>
    /// <param name="targetDirectory">The extension directory to remove.</param>
    void RemoveDirectory(string targetDirectory);
}

/// <summary>
/// The outcome of running an npm command.
/// </summary>
/// <param name="Succeeded">Whether the command exited successfully.</param>
/// <param name="ErrorMessage">A user-visible error message when the command failed; otherwise null.</param>
public readonly record struct NpmCommandResult(bool Succeeded, string? ErrorMessage)
{
    public static NpmCommandResult Ok() => new(true, null);

    public static NpmCommandResult Fail(string errorMessage) => new(false, errorMessage);
}
