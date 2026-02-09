// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Services;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Models;

public partial class WinRTExtensionService : IExtensionService, IDisposable
{
    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    public event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    public Task DisableProviderAsync(string providerId)
    {

    }

    public Task EnableProviderAsync(string providerId)
    {

    }

    public Task<IEnumerable<CommandProviderWrapper>> GetCommandProviderWrappersAsync(WeakReference<IPageContext> weakPageContext, bool includeDisabledExtensions = false)
    {
    }

    public Task<IEnumerable<TopLevelViewModel>> GetTopLevelCommandsAsync()
    {
    }

    public Task SignalStopExtensionsAsync()
    {
    }

    public void Dispose()
    {

    }
}
