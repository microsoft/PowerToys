// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.CmdPal.Common.Services;

public interface IExtensionService
{
    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false);

    // Task<IEnumerable<string>> GetInstalledHomeWidgetPackageFamilyNamesAsync(bool includeDisabledExtensions = false);

    Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(Microsoft.Windows.CommandPalette.Extensions.ProviderType providerType, bool includeDisabledExtensions = false);

    IExtensionWrapper? GetInstalledExtension(string extensionUniqueId);

    Task SignalStopExtensionsAsync();

    public event EventHandler OnExtensionsChanged;

    public void EnableExtension(string extensionUniqueId);

    public void DisableExtension(string extensionUniqueId);

    ///// <summary>
    ///// Gets a boolean indicating whether the extension was disabled due to the corresponding Windows optional feature
    ///// being absent from the machine or in an unknown state.
    ///// </summary>
    ///// <param name="extension">The out of proc extension object</param>
    ///// <returns>True only if the extension was disabled. False otherwise.</returns>
    //public Task<bool> DisableExtensionIfWindowsFeatureNotAvailable(IExtensionWrapper extension);
}
