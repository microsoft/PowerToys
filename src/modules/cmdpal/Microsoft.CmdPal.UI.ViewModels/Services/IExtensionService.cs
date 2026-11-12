// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public interface IExtensionService
{
    /// <summary>
    /// Loads command providers managed by this service. Returns providers that
    /// are immediately ready. Slow or late providers arrive via <see cref="OnProviderAdded"/>.
    /// </summary>
    /// <param name="ct">Cancellation token owned by the caller to cancel in-flight loading.</param>
    /// <returns>Command provider wrappers that are started and ready for command loading.</returns>
    Task<IEnumerable<CommandProviderWrapper>> LoadProvidersAsync(CancellationToken ct);

    /// <summary>
    /// Signals running providers managed by this service to stop/dispose.
    /// </summary>
    Task SignalStopAsync();

    /// <summary>
    /// Gets the currently cached installed extensions managed by this service.
    /// </summary>
    /// <param name="includeDisabledExtensions">True to include disabled extensions in the result.</param>
    /// <returns>A sequence of installed extensions from the current in-memory cache.</returns>
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    /// <summary>
    /// Forces a fresh scan of installed extensions and updates the in-memory cache.
    /// </summary>
    /// <param name="includeDisabledExtensions">True to include disabled extensions in the result.</param>
    /// <returns>A sequence of installed extensions after the cache has been rebuilt.</returns>
    Task<IEnumerable<IExtensionWrapper>> RefreshInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    /// <summary>
    /// Gets a cached installed extension by its unique id.
    /// </summary>
    /// <param name="extensionUniqueId">The unique id of the extension to look up.</param>
    /// <returns>The cached extension if found; otherwise, null.</returns>
    IExtensionWrapper? GetInstalledExtension(string extensionUniqueId);

    /// <summary>
    /// Enables an installed extension by unique id.
    /// </summary>
    /// <param name="extensionUniqueId">The unique id of the extension to enable.</param>
    void EnableExtension(string extensionUniqueId);

    /// <summary>
    /// Disables an installed extension by unique id.
    /// </summary>
    /// <param name="extensionUniqueId">The unique id of the extension to disable.</param>
    void DisableExtension(string extensionUniqueId);

    /// <summary>
    /// Raised when one or more providers become available (late start, new package install, etc.).
    /// </summary>
    event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderAdded;

    /// <summary>
    /// Raised when one or more providers are removed (package uninstall, etc.).
    /// </summary>
    event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderRemoved;
}
