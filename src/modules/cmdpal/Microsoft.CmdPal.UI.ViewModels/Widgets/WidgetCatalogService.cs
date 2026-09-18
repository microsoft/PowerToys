// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels.Widgets;

public sealed partial class WidgetCatalogService(TopLevelCommandManager commandManager, IServiceProvider serviceProvider) : IWidgetCatalog, IDisposable
{
    private static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(10);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private WidgetCatalogEntry[] _entries = [];

    public IReadOnlyList<WidgetCatalogEntry> Entries => _entries;

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!commandManager.CommandProviders.Any())
            {
                await commandManager.LoadBuiltInProvidersAsync().WaitAsync(ProviderTimeout, cancellationToken).ConfigureAwait(false);
            }

            if (!commandManager.CommandProviders.Any(provider => provider.IsExtension))
            {
                await commandManager.LoadExternalProvidersAsync().WaitAsync(ProviderTimeout, cancellationToken).ConfigureAwait(false);
            }

            var wrappers = commandManager.CommandProviders.ToArray();
            var loads = wrappers.Select(wrapper => LoadProviderAsync(wrapper, cancellationToken));
            await Task.WhenAll(loads).ConfigureAwait(false);
            _entries = wrappers
                .SelectMany(wrapper => wrapper.WidgetDefinitions.Select(definition => new WidgetCatalogEntry(
                    definition,
                    instanceId => wrapper.GetWidget(definition.WidgetId, instanceId))))
                .OrderBy(entry => entry.Definition.SourceName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(entry => entry.Definition.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public WidgetCatalogEntry? Find(WidgetBinding binding) => _entries.FirstOrDefault(entry =>
        string.Equals(entry.Definition.ExtensionId, binding.ExtensionId, StringComparison.Ordinal) &&
        string.Equals(entry.Definition.ProviderId, binding.ProviderId, StringComparison.Ordinal) &&
        string.Equals(entry.Definition.WidgetId, binding.WidgetId, StringComparison.Ordinal));

    public async Task<IWidgetContent?> CreateAsync(WidgetBinding binding, string instanceId, CancellationToken cancellationToken)
    {
        var entry = Find(binding);
        if (entry is null)
        {
            return null;
        }

        return await Task.Run(() => entry.Create(instanceId), cancellationToken)
            .WaitAsync(ProviderTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task LoadProviderAsync(CommandProviderWrapper wrapper, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Run(() => wrapper.LoadWidgets(serviceProvider), cancellationToken)
                .WaitAsync(ProviderTimeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogError($"Failed to load widgets from {wrapper.ProviderId}", ex);
        }
    }

    public void Dispose() => _refreshLock.Dispose();
}
