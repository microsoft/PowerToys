// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Small hook used by the gallery install flow to share the JSExtensions root, stop a running
/// Node.js process before delete, check what is installed, and ask the host to load a promoted
/// directory.
/// </summary>
public interface IJsExtensionHost
{
    /// <summary>
    /// Gets the absolute path of the directory where JavaScript/TypeScript extensions live.
    /// Each extension occupies its own subdirectory under this path.
    /// </summary>
    string ExtensionsRootPath { get; }

    /// <summary>
    /// Stops the extension loaded from <paramref name="extensionDirectory"/> and unloads its
    /// provider. Safe to call when no extension is loaded from that directory. Callers use this before
    /// deleting the directory so file handles are released.
    /// </summary>
    /// <param name="extensionDirectory">The extension's directory under the JSExtensions root.</param>
    /// <param name="cancellationToken">A token to cancel a bounded wait while the provider is stopped.</param>
    void StopExtension(string extensionDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether <paramref name="extensionDirectory"/> contains a CmdPal manifest the host
    /// can load. Used after install so success is only reported when the extension is loadable.
    /// </summary>
    /// <param name="extensionDirectory">The extension's directory under the JSExtensions root.</param>
    /// <returns><see langword="true"/> when a loadable manifest is present; otherwise, <see langword="false"/>.</returns>
    bool IsExtensionDiscoverable(string extensionDirectory);

    /// <summary>
    /// Determines whether an extension named <paramref name="extensionName"/> is currently loaded from
    /// JSExtensions/&lt;extensionName&gt;/. This follows the host state, not the catalog, so the gallery
    /// shows Uninstall only for packages the host knows about.
    /// </summary>
    /// <param name="extensionName">The directory name for the extension under the JSExtensions root.</param>
    /// <returns><see langword="true"/> when the extension is loaded; otherwise, <see langword="false"/>.</returns>
    bool IsExtensionInstalled(string extensionName);

    /// <summary>
    /// Asks the host to discover and load the extension in <paramref name="extensionDirectory"/> and
    /// waits, up to <paramref name="timeout"/>, for provider registration. Called after promotion so
    /// install success means the extension loaded, not just copied to disk.
    /// </summary>
    /// <param name="extensionDirectory">The promoted extension's directory under the JSExtensions root.</param>
    /// <param name="timeout">The maximum time to wait for provider registration.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns><see langword="true"/> when the provider registered within the timeout; otherwise, <see langword="false"/>.</returns>
    Task<bool> RefreshAndAwaitProviderAsync(string extensionDirectory, TimeSpan timeout, CancellationToken cancellationToken);
}
