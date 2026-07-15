// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Narrow hook exposed by the Phase 4 <see cref="JsonRpcExtensionService"/> so the gallery
/// install/uninstall flow can share the JSExtensions root directory and terminate a running
/// extension's Node.js process before its directory is removed.
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
    void StopExtension(string extensionDirectory);
}
