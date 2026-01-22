// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Common.Abstractions;

public interface IExtensionService
{
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(CommandPalette.Extensions.ProviderType providerType, bool includeDisabledExtensions = false);

    IExtensionWrapper? GetInstalledExtension(string extensionUniqueId);

    Task SignalStopExtensionsAsync();

    event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionAdded;

    event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionRemoved;

    void EnableExtension(string extensionUniqueId);

    void DisableExtension(string extensionUniqueId);
}
