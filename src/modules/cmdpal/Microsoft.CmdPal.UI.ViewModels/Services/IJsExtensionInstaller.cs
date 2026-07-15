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
    /// Installs the npm package for a jsonrpc extension into JSExtensions/&lt;extensionName&gt;/.
    /// The Phase 4 FileSystemWatcher then loads the extension automatically.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <param name="npmPackage">The npm package identifier to install.</param>
    /// <param name="registry">Optional npm registry URL. When null or empty, the default registry is used.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the install.</returns>
    Task<JsExtensionInstallResult> InstallAsync(string extensionName, string npmPackage, string? registry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Terminates the extension's Node.js process and deletes JSExtensions/&lt;extensionName&gt;/.
    /// The Phase 4 FileSystemWatcher then unloads the extension automatically.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the uninstall.</returns>
    Task<JsExtensionInstallResult> UninstallAsync(string extensionName, CancellationToken cancellationToken = default);
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
