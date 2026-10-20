// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dashboard;

/// <summary>
/// Backs the dashboard's settings page.
/// </summary>
public sealed partial class DashboardSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly TopLevelCommandManager? _topLevelCommandManager;
    private readonly TaskScheduler _uiScheduler;
    private bool _isDisposed;

    public ObservableCollection<DashboardPinnedTileViewModel> PinnedTiles { get; } = [];

    public bool HasPinnedTiles => PinnedTiles.Count > 0;

    public DashboardSettingsViewModel(
        ISettingsService settingsService,
        TopLevelCommandManager? topLevelCommandManager,
        TaskScheduler uiScheduler)
    {
        _settingsService = settingsService;
        _topLevelCommandManager = topLevelCommandManager;
        _uiScheduler = uiScheduler;
        _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        PinnedTiles.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPinnedTiles));
        RefreshPinnedTiles();
    }

    private DashboardSettings Current => _settingsService.Settings.DashboardSettings;

    public bool IsEnabled
    {
        get => _settingsService.Settings.EnableDashboard;
        set
        {
            if (value == IsEnabled)
            {
                return;
            }

            _settingsService.UpdateSettings(s => s with { EnableDashboard = value });
            OnPropertyChanged();
        }
    }

    public HotkeySettings? Hotkey
    {
        get => _settingsService.Settings.DashboardHotkey;
        set
        {
            _settingsService.UpdateSettings(s => s with { DashboardHotkey = value ?? SettingsModel.DefaultDashboardShortcut });
            OnPropertyChanged();
        }
    }

    public bool IgnoreHotkeyWhenFullscreen
    {
        get => Current.IgnoreHotkeyWhenFullscreen;
        set => UpdateDashboard(d => d with { IgnoreHotkeyWhenFullscreen = value });
    }

    public bool DismissOnFocusLost
    {
        get => Current.DismissOnFocusLost;
        set => UpdateDashboard(d => d with { DismissOnFocusLost = value });
    }

    public int BackdropIndex
    {
        get => (int)Current.Backdrop;
        set
        {
            if (value < 0 || !Enum.IsDefined(typeof(DashboardBackdrop), value))
            {
                return;
            }

            UpdateDashboard(d => d with { Backdrop = (DashboardBackdrop)value });
        }
    }

    public double BackgroundOpacity
    {
        get => Current.BackgroundOpacity;
        set
        {
            var opacity = (int)Math.Round(Math.Clamp(value, DashboardSettings.MinBackgroundOpacity, DashboardSettings.MaxBackgroundOpacity));
            UpdateDashboard(d => d with { BackgroundOpacity = opacity });
        }
    }

    [RelayCommand]
    private void ShowDashboard() => WeakReferenceMessenger.Default.Send(new ToggleDashboardMessage());

    [RelayCommand]
    private void ResetLayout() => UpdateDashboard(d => d.WithLayoutReset(), notify: false);

    [RelayCommand]
    private void Unpin(DashboardPinnedTileViewModel? tile)
    {
        if (tile is null)
        {
            return;
        }

        UpdateDashboard(d => d.WithTileUnpinned(tile.ProviderId, tile.CommandId), notify: false);
    }

    private void UpdateDashboard(Func<DashboardSettings, DashboardSettings> update, bool notify = true, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        var before = Current;
        var after = update(before);
        if (before == after)
        {
            return;
        }

        _settingsService.UpdateSettings(s => s with { DashboardSettings = update(s.DashboardSettings) });
        if (notify)
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void SettingsService_SettingsChanged(ISettingsService sender, SettingsModel args)
    {
        if (_isDisposed)
        {
            return;
        }

        _ = Task.Factory.StartNew(RefreshPinnedTiles, CancellationToken.None, TaskCreationOptions.None, _uiScheduler);
    }

    private void RefreshPinnedTiles()
    {
        if (_isDisposed)
        {
            return;
        }

        var tiles = Current.Tiles;
        var unchanged = tiles.Count == PinnedTiles.Count &&
            tiles.Select((tile, i) => PinnedTiles[i].Matches(tile)).All(match => match);
        if (unchanged)
        {
            return;
        }

        PinnedTiles.Clear();
        foreach (var tile in tiles)
        {
            var command = _topLevelCommandManager?.LookupCommand(tile.ProviderId, tile.CommandId)
                ?? _topLevelCommandManager?.LookupDockBand(tile.CommandId);
            PinnedTiles.Add(new DashboardPinnedTileViewModel(
                tile.ProviderId,
                tile.CommandId,
                string.IsNullOrEmpty(command?.Title) ? tile.CommandId : command.Title,
                command?.ExtensionName ?? tile.ProviderId,
                command?.IconViewModel));
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
    }
}
