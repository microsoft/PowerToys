// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Narrow hook exposed by the Phase 4 <see cref="JsonRpcExtensionService"/> so the gallery
/// install/uninstall flow can share the JSExtensions root directory, terminate a running
/// extension's Node.js process before its directory is removed, learn which extensions are
/// actually loaded, and ask the host to load a freshly promoted directory.
/// </summary>
public interface IJsExtensionHost
{
    /// <summary>
    /// Gets the absolute path of the directory where JavaScript/TypeScript extensions live.
    /// Each extension occupies its own subdirectory under this path.
    /// </summary>
    string ExtensionsRootPath { get; }

    /// <summary>
    /// Stops the extension loaded from <paramref name="extensionDirectory"/>, terminating its
    /// Node.js process and unloading its provider. Safe to call when no extension is loaded from
    /// that directory. Callers use this before deleting the directory so file handles are
    /// released.
    /// </summary>
    /// <param name="extensionDirectory">The extension's directory under the JSExtensions root.</param>
    /// <param name="cancellationToken">A token to cancel a bounded wait while the provider is stopped.</param>
    void StopExtension(string extensionDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether <paramref name="extensionDirectory"/> contains a valid CmdPal manifest
    /// that the Phase 4 discovery scan can load (a package.json with a cmdpal section at the
    /// directory root). Used after an install so success is only reported when the extension is
    /// actually loadable.
    /// </summary>
    /// <param name="extensionDirectory">The extension's directory under the JSExtensions root.</param>
    /// <returns><see langword="true"/> when a loadable manifest is present; otherwise, <see langword="false"/>.</returns>
    bool IsExtensionDiscoverable(string extensionDirectory);

    /// <summary>
    /// Determines whether an extension named <paramref name="extensionName"/> is currently loaded by
    /// the host from JSExtensions/&lt;extensionName&gt;/. This reflects the live host state (a validated,
    /// loadable manifest that was actually registered) rather than any catalog status, so the gallery
    /// can show Uninstall for packages that are really installed.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <returns><see langword="true"/> when the extension is loaded; otherwise, <see langword="false"/>.</returns>
    bool IsExtensionInstalled(string extensionName);

    /// <summary>
    /// Asks the host to discover and load the extension in <paramref name="extensionDirectory"/> and
    /// waits, up to <paramref name="timeout"/>, until its provider has been registered. Called after a
    /// freshly staged extension is promoted into the JSExtensions root so success is only reported once
    /// the extension is actually loadable and registered.
    /// </summary>
    /// <param name="extensionDirectory">The promoted extension's directory under the JSExtensions root.</param>
    /// <param name="timeout">The maximum time to wait for provider registration.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns><see langword="true"/> when the provider registered within the timeout; otherwise, <see langword="false"/>.</returns>
    Task<bool> RefreshAndAwaitProviderAsync(string extensionDirectory, TimeSpan timeout, CancellationToken cancellationToken);
}
