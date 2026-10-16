// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Windows.Win32;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Dock;

/// <summary>
/// Manages multiple <see cref="DockWindow"/> instances, one per enabled monitor.
/// Replaces the single <c>_dockWindow</c> field previously held by ShellPage.
/// </summary>
public sealed partial class DockWindowManager : IDisposable
{
    private readonly IMonitorService _monitorService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Dictionary<string, (DockWindow Window, DockViewModel ViewModel)> _docks = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private int _syncing;

    /// <summary>
    /// Windows can briefly report a monitor as absent while the graphics stack re-enumerates
    /// it during a display switch or Modern Standby resume. Waiting for the topology to settle
    /// keeps us from disposing a dock that is about to come back.
    /// </summary>
    private static readonly TimeSpan MonitorsChangedDebounceInterval = TimeSpan.FromSeconds(3);
    private readonly DispatcherQueueTimer _monitorsChangedDebounceTimer;

    private bool? _lastSyncedEnableDock;
    private DockSettings? _lastSyncedDockSettings;

    public DockWindowManager(
        IMonitorService monitorService,
        ISettingsService settingsService,
        DispatcherQueue dispatcherQueue)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _dispatcherQueue = dispatcherQueue;
        _monitorsChangedDebounceTimer = _dispatcherQueue.CreateTimer();

        _monitorService.MonitorsChanged += OnMonitorsChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Creates dock windows for all enabled monitors according to current settings.
    /// Runs reconciliation to ensure configs match currently connected monitors.
    /// </summary>
    public void ShowDocks()
    {
        var settings = _settingsService.Settings;
        if (!settings.EnableDock)
        {
            return;
        }

        SyncDocksToSettings();
    }

    /// <summary>
    /// Destroys all dock windows.
    /// </summary>
    public void HideDocks()
    {
        foreach (var (_, (window, viewModel)) in _docks)
        {
            window.Close();
            viewModel.Dispose();
        }

        _docks.Clear();
    }

    /// <summary>
    /// Synchronizes running dock windows to match the current settings and connected monitors.
    /// </summary>
    public void SyncDocksToSettings(bool refreshDockWindows = false)
    {
        if (Interlocked.CompareExchange(ref _syncing, 1, 0) != 0)
        {
            return;
        }

        try
        {
            SyncDocksToSettingsCore(refreshDockWindows);
        }
        finally
        {
            Interlocked.Exchange(ref _syncing, 0);
        }
    }

    private void SyncDocksToSettingsCore(bool refreshDockWindows)
    {
        var settings = _settingsService.Settings;
        if (!settings.EnableDock)
        {
            HideDocks();
            return;
        }

        var dockSettings = settings.DockSettings;

        // Discard snapshots read before the display topology settled.
        var monitors = _monitorService.GetMonitors(forceRefresh: refreshDockWindows);
        var currentConfigs = dockSettings.MonitorConfigs ?? System.Collections.Immutable.ImmutableList<DockMonitorConfig>.Empty;
        var reconciled = MonitorConfigReconciler.Reconcile(currentConfigs, monitors);
        if (reconciled != currentConfigs)
        {
            _settingsService.UpdateSettings(s => s with
            {
                DockSettings = s.DockSettings with { MonitorConfigs = reconciled },
            });

            // Re-read settings after update
            dockSettings = _settingsService.Settings.DockSettings;
        }

        var configs = GetEffectiveConfigs(dockSettings);
        var desiredMonitorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Refresh settings on existing ViewModels so they pick up new pins/changes
        foreach (var (_, (_, viewModel)) in _docks)
        {
            viewModel.UpdateSettings(dockSettings);
        }

        for (var i = 0; i < configs.Count; i++)
        {
            var config = configs[i];
            if (!config.Enabled)
            {
                continue;
            }

            var monitor = _monitorService.GetMonitorByStableId(config.MonitorDeviceId);
            if (monitor is null)
            {
                continue;
            }

            desiredMonitorIds.Add(config.MonitorDeviceId);

            if (!_docks.ContainsKey(config.MonitorDeviceId))
            {
                CreateDockForMonitor(config.MonitorDeviceId, dockSettings);
            }
        }

        // Remove dock windows for monitors that are no longer desired
        var toRemove = new List<string>();
        foreach (var id in _docks.Keys)
        {
            if (!desiredMonitorIds.Contains(id))
            {
                toRemove.Add(id);
            }
        }

        for (var i = 0; i < toRemove.Count; i++)
        {
            var id = toRemove[i];
            if (_docks.Remove(id, out var dock))
            {
                dock.Window.Close();
                dock.ViewModel.Dispose();
            }
        }

        if (!refreshDockWindows)
        {
            return;
        }

        foreach (var (_, (window, _)) in _docks)
        {
            window.RefreshForMonitorChange();
        }
    }

    /// <summary>
    /// Cycles from the focused dock, using the navigation settings to choose where a new cycle begins.
    /// </summary>
    public void FocusDock(bool reverse = false)
    {
        if (_docks.Count == 0)
        {
            return;
        }

        PInvoke.GetCursorPos(out var cursor);

        var monitors = _monitorService.GetMonitors();
        var focusedDockId = _docks.FirstOrDefault(static dock => dock.Value.Window.HasKeyboardFocus).Key;
        if (reverse && focusedDockId is null)
        {
            return;
        }

        var targetId = focusedDockId ?? DockFocusTargetResolver.Resolve(
            monitors,
            _docks.Keys,
            cursor.X,
            cursor.Y,
            focusPrimaryFirst: _settingsService.Settings.DockFocusPrimaryFirst);

        if (targetId is null)
        {
            return;
        }

        var moveFromCurrent = focusedDockId is not null;

        // Restore only on the starting dock so crossing docks still visits every item.
        var restoreLastFocus = !moveFromCurrent && _settingsService.Settings.DockRememberLastFocusedItem;
        if (!_settingsService.Settings.DockFocusAcrossMonitors)
        {
            if (!TryFocusDock(targetId, moveFromCurrent, wrap: true, reverse, restoreLastFocus))
            {
                _docks[targetId].Window.ReleaseKeyboardFocus(restoreForeground: true);
            }

            return;
        }

        var dockOrder = DockFocusTargetResolver.GetTraversalOrder(monitors, _docks.Keys, targetId, reverse);
        if (DockFocusNavigation.TryFocusAcrossDocks(
            dockOrder,
            targetId,
            moveFromCurrent,
            restoreLastFocus,
            (monitorId, move, restore) => TryFocusDock(monitorId, move, wrap: false, reverse, restore),
            monitorId => _docks[monitorId].Window.ResetRememberedFocus()))
        {
            return;
        }

        // Complete the cycle within the dock where this press started.
        if (!moveFromCurrent ||
            !dockOrder.Contains(targetId, StringComparer.OrdinalIgnoreCase) ||
            !TryFocusDock(targetId, moveFromCurrent: false, wrap: false, reverse, restoreLastFocus: false))
        {
            _docks[targetId].Window.ReleaseKeyboardFocus(restoreForeground: true);
        }
    }

    private bool TryFocusDock(string monitorId, bool moveFromCurrent, bool wrap, bool reverse, bool restoreLastFocus)
    {
        var target = _docks[monitorId].Window;
        var previousDock = _docks.Values.FirstOrDefault(static dock => dock.Window.HasKeyboardFocus).Window;
        if (!target.TryFocusNextItem(moveFromCurrent, wrap, reverse, restoreLastFocus, previousDock))
        {
            return false;
        }

        foreach (var (_, (window, _)) in _docks)
        {
            if (window != target)
            {
                window.ReleaseKeyboardFocus(restoreForeground: false);
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _monitorService.MonitorsChanged -= OnMonitorsChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;

        _monitorsChangedDebounceTimer.Stop();

        HideDocks();
    }

    private void CreateDockForMonitor(string monitorDeviceId, DockSettings dockSettings)
    {
        var viewModel = CreateDockViewModel(monitorDeviceId);

        var monitor = _monitorService.GetMonitorByStableId(monitorDeviceId);
        var sideOverride = dockSettings.GetSideForMonitor(monitorDeviceId);

        var window = new DockWindow(viewModel, monitor, sideOverride);
        _docks[monitorDeviceId] = (window, viewModel);
        window.Show();

        viewModel.InitializeBands();
    }

    private DockViewModel CreateDockViewModel(string monitorDeviceId)
    {
        var serviceProvider = App.Current.Services;
        var tlcManager = serviceProvider.GetRequiredService<TopLevelCommandManager>();
        var contextMenuFactory = serviceProvider.GetRequiredService<IContextMenuFactory>();
        var scheduler = serviceProvider.GetRequiredService<TaskScheduler>();

        return new DockViewModel(tlcManager, contextMenuFactory, scheduler, _settingsService, monitorDeviceId);
    }

    private void OnMonitorsChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            _monitorsChangedDebounceTimer.Debounce(
                () => SyncDocksToSettings(refreshDockWindows: true),
                interval: MonitorsChangedDebounceInterval,
                immediate: false);
        });
    }

    private void OnSettingsChanged(ISettingsService sender, SettingsModel args)
    {
        if (args.EnableDock == _lastSyncedEnableDock && args.DockSettings == _lastSyncedDockSettings)
        {
            return;
        }

        _lastSyncedEnableDock = args.EnableDock;
        _lastSyncedDockSettings = args.DockSettings;

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                SyncDocksToSettings();
            }
        });
    }

    /// <summary>
    /// Returns the effective list of monitor configs. If settings have no explicit
    /// configs (legacy / first-run), synthesizes one for the primary monitor.
    /// </summary>
    private IReadOnlyList<DockMonitorConfig> GetEffectiveConfigs(DockSettings dockSettings)
    {
        var configs = dockSettings.MonitorConfigs ?? System.Collections.Immutable.ImmutableList<DockMonitorConfig>.Empty;
        if (configs.Count > 0)
        {
            return configs;
        }

        // Legacy / migration: no per-monitor configs saved yet.
        // Synthesize a config for the primary monitor inheriting global Side.
        var primary = _monitorService.GetPrimaryMonitor();
        if (primary is null)
        {
            return Array.Empty<DockMonitorConfig>();
        }

        return new[]
        {
            new DockMonitorConfig
            {
                MonitorDeviceId = primary.StableId,
                Enabled = true,
                Side = null,
                IsPrimary = true,
            },
        };
    }
}
