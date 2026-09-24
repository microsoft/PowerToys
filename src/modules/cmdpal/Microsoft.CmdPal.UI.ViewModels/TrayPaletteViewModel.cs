// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TrayPaletteViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly TopLevelCommandManager _commandManager;
    private readonly IContextMenuFactory _contextMenuFactory;
    private PinnedCommandSettings[]? _loadedPins;
    private (CommandProviderWrapper Provider, bool IsActive)[] _loadedProviders = [];
    private int _contentVersion;
    private int _loadedContentVersion;
    private int _refreshVersion;
    private bool _disposed;

    public TrayPaletteViewModel(
        ISettingsService settingsService,
        TopLevelCommandManager commandManager,
        IContextMenuFactory contextMenuFactory)
    {
        _settingsService = settingsService;
        _commandManager = commandManager;
        _contextMenuFactory = contextMenuFactory;
        _commandManager.TopLevelCommands.CollectionChanged += CommandsChanged;
    }

    private void CommandsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Interlocked.Increment(ref _contentVersion);

    public ObservableCollection<TrayPaletteItemViewModel> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial bool HasLoadError { get; private set; }

    public bool IsEmpty => !IsLoading && Items.Count == 0;

    public void SaveOrder()
    {
        try
        {
            var order = Items.Select(item => item.Pin).ToArray();
            _settingsService.UpdateSettings(settings => settings with
            {
                TrayPalette = settings.TrayPalette.Reorder(order.Where(settings.TrayPalette.Commands.Contains).ToArray()),
            });
        }
        catch (Exception ex)
        {
            HasLoadError = true;
            Logger.LogError("Failed to save Tray Palette order.", ex);
        }
    }

    public async Task RefreshAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var pins = _settingsService.Settings.TrayPalette.Commands;
        var providers = _commandManager.CommandProviders.Select(provider => (provider, provider.IsActive)).ToArray();
        var contentVersion = Volatile.Read(ref _contentVersion);
        if (_loadedPins is not null &&
            !IsLoading &&
            !HasLoadError &&
            contentVersion == _loadedContentVersion &&
            pins.SequenceEqual(_loadedPins) &&
            providers.SequenceEqual(_loadedProviders))
        {
            return;
        }

        var version = ++_refreshVersion;
        IsLoading = true;
        HasLoadError = false;
        OnPropertyChanged(nameof(IsEmpty));

        var loaded = await Task.Run(() =>
        {
            List<TrayPaletteItemViewModel> items = [];
            var failed = false;
            foreach (var pin in pins.Distinct())
            {
                CommandItemViewModel? item = null;
                try
                {
                    var provider = _commandManager.LookupProvider(pin.ProviderId);
                    var model = provider?.ResolveCommandItem(pin.CommandId);
                    if (provider is null || model?.Command is not (IInvokableCommand or IPage))
                    {
                        continue;
                    }

                    item = new(new(model), new(provider.TopLevelPageContext), _contextMenuFactory);
                    item.InitializeProperties();
                    items.Add(new(pin, item, provider));
                }
                catch (Exception ex)
                {
                    item?.SafeCleanup();
                    failed = true;
                    Logger.LogError($"Failed to load tray command '{pin.ProviderId}/{pin.CommandId}'.", ex);
                }
            }

            return (Items: items, Failed: failed);
        });

        if (_disposed || version != _refreshVersion)
        {
            Cleanup(loaded.Items);
            return;
        }

        Cleanup(Items);
        Items.Clear();
        foreach (var item in loaded.Items)
        {
            Items.Add(item);
        }

        HasLoadError = loaded.Failed;
        _loadedPins = pins.ToArray();
        _loadedProviders = providers;
        _loadedContentVersion = contentVersion;
        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private static void Cleanup(IEnumerable<TrayPaletteItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.Item.SafeCleanup();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _commandManager.TopLevelCommands.CollectionChanged -= CommandsChanged;
        ++_refreshVersion;
        Cleanup(Items);
        Items.Clear();
    }
}
