// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.Common.Services;

public interface IExtensionService
{
    /// <summary>
    /// Gets the currently cached installed Command Palette extensions.
    /// </summary>
    /// <param name="includeDisabledExtensions">True to include disabled extensions in the result.</param>
    /// <returns>A sequence of installed Command Palette extensions from the current in-memory cache.</returns>
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    /// <summary>
    /// Forces a fresh scan of installed Command Palette extensions and updates the in-memory cache.
    /// </summary>
    /// <param name="includeDisabledExtensions">True to include disabled extensions in the result.</param>
    /// <returns>A sequence of installed Command Palette extensions after the cache has been rebuilt.</returns>
    Task<IEnumerable<IExtensionWrapper>> RefreshInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    // Task<IEnumerable<string>> GetInstalledHomeWidgetPackageFamilyNamesAsync(bool includeDisabledExtensions = false);
    /// <summary>
    /// Gets the installed Command Palette extensions for a specific provider type.
    /// </summary>
    /// <param name="providerType">The provider type to match.</param>
    /// <param name="includeDisabledExtensions">True to include disabled extensions in the result.</param>
    /// <returns>A sequence of installed Command Palette extensions for the requested provider type.</returns>
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(Microsoft.CommandPalette.Extensions.ProviderType providerType, bool includeDisabledExtensions = false);

    /// <summary>
    /// Gets a cached installed extension by its unique id.
    /// </summary>
    /// <param name="extensionUniqueId">The unique id of the extension to look up.</param>
    /// <returns>The cached extension if found; otherwise, null.</returns>
    IExtensionWrapper? GetInstalledExtension(string extensionUniqueId);

    /// <summary>
    /// Signals running extensions to stop.
    /// </summary>
    Task SignalStopExtensionsAsync();

    /// <summary>
    /// Raised when one or more extensions are added to the installed set.
    /// </summary>
    event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionAdded;

    /// <summary>
    /// Raised when one or more extensions are removed from the installed set.
    /// </summary>
    event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionRemoved;

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

    ///// <summary>
    ///// Gets a boolean indicating whether the extension was disabled due to the corresponding Windows optional feature
    ///// being absent from the machine or in an unknown state.
    ///// </summary>
    ///// <param name="extension">The out of proc extension object</param>
    ///// <returns>True only if the extension was disabled. False otherwise.</returns>
    // public Task<bool> DisableExtensionIfWindowsFeatureNotAvailable(IExtensionWrapper extension);
}
