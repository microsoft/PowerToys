// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels.Services;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class TrayPaletteViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan CommandLoadTimeout = TimeSpan.FromSeconds(10);

    private readonly TrayItemsCollection _items = [];
    private readonly ISettingsService _settingsService;
    private readonly TopLevelCommandManager _commandManager;
    private readonly IContextMenuFactory _contextMenuFactory;
    private PinnedCommandSettings[]? _loadedPins;
    private (CommandProviderWrapper Provider, bool IsActive, bool IsEnabled)[] _loadedProviders = [];
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

    public ObservableCollection<TrayPaletteItemViewModel> Items => _items;

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

    public Task RefreshAsync() => RefreshAsync(CommandLoadTimeout);

    internal async Task RefreshAsync(TimeSpan commandLoadTimeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var pins = _settingsService.Settings.TrayPalette.Commands;
        var providers = _commandManager.CommandProviders
            .Select(provider => (provider, provider.IsActive, _commandManager.IsProviderEnabled(provider.ProviderId)))
            .ToArray();
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

        var loaded = await Task.Run(async () =>
        {
            List<TrayPaletteItemViewModel> items = [];
            var failed = false;
            foreach (var pin in pins.Distinct())
            {
                using var timeout = new CancellationTokenSource(commandLoadTimeout);
                var cancellationToken = timeout.Token;
                var loadTask = Task.Run(() => LoadItemAsync(pin, cancellationToken));
                try
                {
                    if (await loadTask.WaitAsync(timeout.Token) is { } item)
                    {
                        items.Add(item);
                    }
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    failed = true;
                    Logger.LogError($"Timed out loading tray command '{pin.ProviderId}/{pin.CommandId}'.");
                    _ = loadTask.ContinueWith(
                        task =>
                        {
                            if (task.Status == TaskStatus.RanToCompletion)
                            {
                                task.Result?.Dispose();
                            }
                            else if (task.Exception is { } exception)
                            {
                                Logger.LogError($"Failed to finish loading timed-out tray command '{pin.ProviderId}/{pin.CommandId}'.", exception);
                            }
                        },
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
                catch (Exception ex)
                {
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

        var previousItems = Items.ToArray();
        _items.ReplaceWith(loaded.Items);
        Cleanup(previousItems);

        HasLoadError = loaded.Failed;
        _loadedPins = pins.ToArray();
        _loadedProviders = providers;
        _loadedContentVersion = contentVersion;
        IsLoading = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private async Task<TrayPaletteItemViewModel?> LoadItemAsync(PinnedCommandSettings pin, CancellationToken cancellationToken)
    {
        var resolution = await _commandManager.ResolveCommandAsync(pin.ProviderId, pin.CommandId, cancellationToken);
        try
        {
            if (resolution is null ||
                !(resolution.Command.CommandViewModel.IsInvokableCommand || resolution.Command.CommandViewModel.IsPage))
            {
                return null;
            }

            var item = TrayPaletteItemViewModel.CreateAndInitialize(pin, resolution, _contextMenuFactory);
            resolution = null;
            return item;
        }
        finally
        {
            resolution?.Dispose();
        }
    }

    private static void Cleanup(IEnumerable<TrayPaletteItemViewModel> items)
    {
        foreach (var item in items)
        {
            item.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _commandManager.TopLevelCommands.CollectionChanged -= CommandsChanged;
        ++_refreshVersion;
        Cleanup(Items);
        Items.Clear();
    }

    private sealed partial class TrayItemsCollection : ObservableCollection<TrayPaletteItemViewModel>
    {
        internal void ReplaceWith(IList<TrayPaletteItemViewModel> items)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
