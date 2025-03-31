// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Microsoft.CmdPal.Common.Services;

public interface IExtensionService
{
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    // Task<IEnumerable<string>> GetInstalledHomeWidgetPackageFamilyNamesAsync(bool includeDisabledExtensions = false);
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(Microsoft.CommandPalette.Extensions.ProviderType providerType, bool includeDisabledExtensions = false);

    IExtensionWrapper? GetInstalledExtension(string extensionUniqueId);

    Task SignalStopExtensionsAsync();

    public event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<IExtensionWrapper>>? OnExtensionRemoved;

    public void EnableExtension(string extensionUniqueId);

    public void DisableExtension(string extensionUniqueId);

    ///// <summary>
    ///// Gets a boolean indicating whether the extension was disabled due to the corresponding Windows optional feature
    ///// being absent from the machine or in an unknown state.
    ///// </summary>
    ///// <param name="extension">The out of proc extension object</param>
    ///// <returns>True only if the extension was disabled. False otherwise.</returns>
    // public Task<bool> DisableExtensionIfWindowsFeatureNotAvailable(IExtensionWrapper extension);
}
