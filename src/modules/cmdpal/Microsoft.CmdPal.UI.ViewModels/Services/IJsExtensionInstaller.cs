// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Installs and uninstalls gallery JavaScript/TypeScript ("jsonrpc") extensions into the Phase 4
/// JSExtensions directory. Implementations delegate the actual npm invocation and filesystem
/// side effects to injected abstractions so the flow can be tested without running npm.
/// </summary>
public interface IJsExtensionInstaller
{
    /// <summary>
    /// Installs the approved npm artifact for a jsonrpc extension into JSExtensions/&lt;extensionName&gt;/.
    /// The install is a fail-closed transaction: the package, exact version, integrity, and optional
    /// registry are validated up front, the package is installed into a staging directory outside the
    /// watched root, verified, and only then atomically promoted and awaited for registration. A failed
    /// or cancelled install never corrupts an existing install of the same extension.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <param name="npmPackage">The npm package identifier to install.</param>
    /// <param name="version">The exact version to install.</param>
    /// <param name="integrity">The sha512 Subresource Integrity value of the approved tarball.</param>
    /// <param name="registry">Optional npm registry URL. When null or empty, the default registry is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the install.</returns>
    Task<JsExtensionInstallResult> InstallAsync(string extensionName, string npmPackage, string? version, string? integrity, string? registry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the extension's Node.js process and deletes JSExtensions/&lt;extensionName&gt;/.
    /// The Phase 4 FileSystemWatcher then unloads the extension automatically.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the uninstall.</returns>
    Task<JsExtensionInstallResult> UninstallAsync(string extensionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether an extension named <paramref name="extensionName"/> is currently installed
    /// and loaded by the host. Used to seed the gallery item's installed state from the host truth
    /// rather than the catalog status.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <returns><see langword="true"/> when the extension is installed; otherwise, <see langword="false"/>.</returns>
    bool IsInstalled(string extensionName);
}

/// <summary>
/// The outcome of a JavaScript/TypeScript extension install or uninstall operation.
/// </summary>
/// <param name="Succeeded">Whether the operation completed successfully.</param>
/// <param name="ErrorMessage">A user-visible error message when the operation failed; otherwise null.</param>
public readonly record struct JsExtensionInstallResult(bool Succeeded, string? ErrorMessage)
{
    public static JsExtensionInstallResult Ok() => new(true, null);

    public static JsExtensionInstallResult Fail(string errorMessage) => new(false, errorMessage);
}
