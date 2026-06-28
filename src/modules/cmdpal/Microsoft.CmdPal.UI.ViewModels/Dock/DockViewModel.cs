// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public sealed partial class DockViewModel : IDisposable
{
    private readonly TopLevelCommandManager _topLevelCommandManager;
    private readonly ISettingsService _settingsService;
    private readonly DockPageContext _pageContext;
    private readonly IContextMenuFactory _contextMenuFactory;
    private readonly string? _monitorDeviceId;

    private DockSettings _settings;
    private bool _isEditing;
    private bool _disposed;

    public string? MonitorDeviceId => _monitorDeviceId;
    public TaskScheduler Scheduler { get; }
    public ObservableCollection<DockBandViewModel> StartItems { get; } = new();
    public ObservableCollection<DockBandViewModel> CenterItems { get; } = new();
    public ObservableCollection<DockBandViewModel> EndItems { get; } = new();
    public IReadOnlyList<TopLevelViewModel> AllItems => _topLevelCommandManager.GetDockBandsSnapshot();

    public DockViewModel(
        TopLevelCommandManager tlcManager,
        IContextMenuFactory contextMenuFactory,
        TaskScheduler scheduler,
        ISettingsService settingsService,
        string? monitorDeviceId = null)
    {
        _topLevelCommandManager = tlcManager;
        _contextMenuFactory = contextMenuFactory;
        _settingsService = settingsService;
        _settings = _settingsService.Settings.DockSettings;
        _monitorDeviceId = monitorDeviceId;
        Scheduler = scheduler;
        _pageContext = new(this);

        _topLevelCommandManager.DockBands.CollectionChanged += DockBands_CollectionChanged;
        EmitDockConfiguration();
    }

    private void DockBands_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_isEditing) return;
        _settings = _settingsService.Settings.DockSettings;
        SetupBands();
    }

    public void UpdateSettings(DockSettings settings)
    {
        if (_isEditing) return;
        _settings = settings;
        SetupBands();
    }

    public void InitializeBands() => SetupBands();

    private void SetupBands()
    {
        var (start, center, end) = GetActiveBands();
        SetupBands(start, StartItems);
        SetupBands(center, CenterItems);
        SetupBands(end, EndItems);
    }

    private void SetupBands(ImmutableList<DockBandSettings> bands, ObservableCollection<DockBandViewModel> target)
    {
        List<DockBandViewModel> newBands = new();
        foreach (var band in bands)
        {
            var commandId = band.CommandId;
            
            // Validation logic to prevent broken bands from breaking the UI
            if (string.IsNullOrEmpty(commandId)) continue;
            var topLevelCommand = _topLevelCommandManager.LookupDockBand(commandId);
            
            if (topLevelCommand is not null)
            {
                var bandVm = CreateBandItem(band, topLevelCommand.ItemViewModel);
                newBands.Add(bandVm);
            }
            else
            {
                Logger.LogWarning($"[Dock] SetupBands: skipping invalid band '{commandId}'");
            }
        }

        DoOnUiThread(() =>
        {
            List<DockBandViewModel> removed = new();
            ListHelpers.InPlaceUpdateList(target, newBands, out removed);
            
            Task.Run(() =>
            {
                foreach (var removedItem in removed) removedItem.SafeCleanup();
            });
        });

        Task.Run(() =>
        {
            foreach (var band in newBands) band.SafeInitializePropertiesSynchronous();
        });
    }

    private DockBandViewModel CreateBandItem(DockBandSettings bandSettings, CommandItemViewModel commandItem)
    {
        return new DockBandViewModel(commandItem, commandItem.PageContext, bandSettings, _settingsService, _contextMenuFactory);
    }

    private (ImmutableList<DockBandSettings> Start, ImmutableList<DockBandSettings> Center, ImmutableList<DockBandSettings> End) GetActiveBands()
    {
        if (_monitorDeviceId is not null)
        {
            var config = FindMonitorConfig(_settings, _monitorDeviceId);
            if (config is not null)
            {
                return (config.ResolveStartBands(_settings.StartBands), config.ResolveCenterBands(_settings.CenterBands), config.ResolveEndBands(_settings.EndBands));
            }
        }
        return (_settings.StartBands, _settings.CenterBands, _settings.EndBands);
    }

    private DockSettings WithActiveBands(ImmutableList<DockBandSettings> start, ImmutableList<DockBandSettings> center, ImmutableList<DockBandSettings> end)
    {
        if (_monitorDeviceId is not null)
        {
            var config = FindMonitorConfig(_settings, _monitorDeviceId);
            if (config is not null && config.IsCustomized)
            {
                return _settings with { MonitorConfigs = ReplaceMonitorConfig(_settings.MonitorConfigs, config with { StartBands = start, CenterBands = center, EndBands = end }) };
            }
        }
        return _settings with { StartBands = start, CenterBands = center, EndBands = end };
    }

    private static DockMonitorConfig? FindMonitorConfig(DockSettings settings, string deviceId)
    {
        return settings.MonitorConfigs?.FirstOrDefault(c => string.Equals(c.MonitorDeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
    }

    private static ImmutableList<DockMonitorConfig> ReplaceMonitorConfig(ImmutableList<DockMonitorConfig> configs, DockMonitorConfig updated)
    {
        int index = configs.FindIndex(c => string.Equals(c.MonitorDeviceId, updated.MonitorDeviceId, StringComparison.OrdinalIgnoreCase));
        return index != -1 ? configs.SetItem(index, updated) : configs.Add(updated);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _topLevelCommandManager.DockBands.CollectionChanged -= DockBands_CollectionChanged;
            _disposed = true;
        }
    }

    private void DoOnUiThread(Action action) => Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, Scheduler);
    private void EmitDockConfiguration() => WeakReferenceMessenger.Default.Send(new TelemetryDockConfigurationMessage(_settingsService.Settings.EnableDock, GetEffectiveSide().ToString().ToLowerInvariant(), string.Empty, string.Empty, string.Empty));
    public DockSide GetEffectiveSide() => _monitorDeviceId != null ? FindMonitorConfig(_settings, _monitorDeviceId)?.ResolveSide(_settings.Side) ?? _settings.Side : _settings.Side;

    // Remaining methods (FindBandById, UnpinBand, etc.) are omitted for brevity but should be kept if they were in your file.
    // If you need the rest of the methods, please let me know.
}
