// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TrayPaletteViewModel(
    ISettingsService settingsService,
    TopLevelCommandManager commandManager,
    IContextMenuFactory contextMenuFactory) : ObservableObject, IDisposable
{
    private int _refreshVersion;
    private bool _disposed;

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
            settingsService.UpdateSettings(settings => settings with
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
        var version = ++_refreshVersion;
        var pins = settingsService.Settings.TrayPalette.Commands;
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
                    var provider = commandManager.LookupProvider(pin.ProviderId);
                    var model = provider?.ResolveCommandItem(pin.CommandId);
                    if (provider is null || model?.Command is not (IInvokableCommand or IPage))
                    {
                        continue;
                    }

                    item = new(new(model), new(provider.TopLevelPageContext), contextMenuFactory);
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
        ++_refreshVersion;
        Cleanup(Items);
        Items.Clear();
    }
}
