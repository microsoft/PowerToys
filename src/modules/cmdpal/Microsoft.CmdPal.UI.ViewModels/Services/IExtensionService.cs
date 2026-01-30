// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public interface IExtensionService
{
    event TypedEventHandler<CommandProviderWrapper, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    event TypedEventHandler<CommandProviderWrapper, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    Task<IEnumerable<CommandProviderWrapper>> GetCommandProviderWrappersAsync(WeakReference<IPageContext> weakPageContext, bool includeDisabledExtensions = false);

    Task<IEnumerable<TopLevelViewModel>> GetTopLevelCommandsAsync();

    Task SignalStopExtensionsAsync();

    Task EnableProviderAsync(string providerId);

    Task DisableProviderAsync(string providerId);
}
