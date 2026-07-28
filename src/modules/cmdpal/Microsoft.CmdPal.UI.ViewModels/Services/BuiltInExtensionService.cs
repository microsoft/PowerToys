// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Extension service that manages in-process built-in command providers
/// registered in the DI container as <see cref="ICommandProvider"/>.
/// </summary>
public sealed class BuiltInExtensionService : IExtensionService
{
    private readonly IEnumerable<ICommandProvider> _commandProviders;
    private readonly TaskScheduler _taskScheduler;
    private readonly List<IExtensionWrapper> _wrappers = [];

#pragma warning disable CS0067 // Events are required by the interface but not raised by this implementation
    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderAdded;

    public event TypedEventHandler<IExtensionService, IEnumerable<CommandProviderWrapper>>? OnProviderRemoved;
#pragma warning restore CS0067

    public BuiltInExtensionService(IEnumerable<ICommandProvider> commandProviders, TaskScheduler taskScheduler)
    {
        _commandProviders = commandProviders;
        _taskScheduler = taskScheduler;
    }

    public Task<IEnumerable<CommandProviderWrapper>> LoadProvidersAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return Task.FromResult<IEnumerable<CommandProviderWrapper>>([]);
        }

        var wrappers = new List<CommandProviderWrapper>();
        foreach (var provider in _commandProviders)
        {
            wrappers.Add(new CommandProviderWrapper(provider, _taskScheduler));
        }

        return Task.FromResult<IEnumerable<CommandProviderWrapper>>(wrappers);
    }

    public Task SignalStopAsync()
    {
        // Built-in providers are in-proc and don't need explicit stop signaling.
        return Task.CompletedTask;
    }

    public Task<IEnumerable<IExtensionWrapper>> GetInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        return Task.FromResult<IEnumerable<IExtensionWrapper>>(_wrappers);
    }

    public Task<IEnumerable<IExtensionWrapper>> RefreshInstalledExtensionsAsync(bool includeDisabledExtensions = false)
    {
        // Built-in set is fixed at startup; refresh is a no-op.
        return GetInstalledExtensionsAsync(includeDisabledExtensions);
    }

    public IExtensionWrapper? GetInstalledExtension(string extensionUniqueId)
    {
        return _wrappers.FirstOrDefault(w => w.ExtensionUniqueId == extensionUniqueId);
    }

    public void EnableExtension(string extensionUniqueId)
    {
        // Nothing to do here. We're built-in extensions.
    }

    public void DisableExtension(string extensionUniqueId)
    {
        // Nothing to do here. We're built-in extensions.
    }
}
