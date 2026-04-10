// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Higher-level extension service that operates on <see cref="CommandProviderWrapper"/>
/// and <see cref="TopLevelViewModel"/> rather than raw <see cref="Common.Services.IExtensionWrapper"/>.
/// </summary>
public interface IExtensionService
{
    event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderAdded;

    event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnCommandProviderRemoved;

    event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsAdded;

    event TypedEventHandler<CommandProviderWrapper, IEnumerable<TopLevelViewModel>>? OnCommandsRemoved;

    Task SignalStartExtensionsAsync(WeakReference<IPageContext> weakPageContext);

    Task SignalStopExtensionsAsync();

    Task EnableProviderAsync(string providerId);

    Task DisableProviderAsync(string providerId);
}
