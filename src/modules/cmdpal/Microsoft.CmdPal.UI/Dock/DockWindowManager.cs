// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using WinUIEx;

using MonitorInfo = Microsoft.CmdPal.UI.ViewModels.Models.MonitorInfo;

namespace Microsoft.CmdPal.UI.Dock;

/// <summary>
/// Manages multiple <see cref="DockWindow"/> instances, one per enabled monitor.
/// Replaces the single <c>_dockWindow</c> field previously held by ShellPage.
/// </summary>
public sealed partial class DockWindowManager : IDisposable
{
    private readonly IMonitorService _monitorService;
    private readonly SettingsService _settingsService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Dictionary<string, DockWindow> _dockWindows = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public DockWindowManager(
        IMonitorService monitorService,
        SettingsService settingsService,
        DispatcherQueue dispatcherQueue)
    {
        _monitorService = monitorService;
        _settingsService = settingsService;
        _dispatcherQueue = dispatcherQueue;

        _monitorService.MonitorsChanged += OnMonitorsChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    /// <summary>
    /// Creates dock windows for all enabled monitors according to current settings.
    /// Call this on startup when the dock is enabled.
    /// </summary>
    public void ShowDocks()
    {
        var settings = _settingsService.CurrentSettings;
        if (!settings.EnableDock)
        {
            return;
        }

        var dockSettings = settings.DockSettings;
        var configs = GetEffectiveConfigs(dockSettings);

        foreach (var config in configs)
        {
            if (!config.Enabled)
            {
                continue;
            }

            var monitor = _monitorService.GetMonitorByDeviceId(config.MonitorDeviceId);
            if (monitor is null)
            {
                continue; // Monitor not connected
            }

            if (!_dockWindows.ContainsKey(config.MonitorDeviceId))
            {
                CreateDockForMonitor(monitor, config, dockSettings);
            }
        }
    }

    /// <summary>
    /// Destroys all dock windows.
    /// </summary>
    public void HideDocks()
    {
        foreach (var (_, window) in _dockWindows)
        {
            window.Close();
        }

        _dockWindows.Clear();
    }

    /// <summary>
    /// Synchronizes running dock windows to match the current settings.
    /// Creates new windows for newly enabled monitors, destroys windows
    /// for disabled or disconnected monitors, and repositions existing ones.
    /// </summary>
    public void SyncDocksToSettings()
    {
        var settings = _settingsService.CurrentSettings;
        if (!settings.EnableDock)
        {
            HideDocks();
            return;
        }

        var dockSettings = settings.DockSettings;

        // Reconcile stale DeviceIds before matching configs to monitors.
        if (MonitorConfigReconciler.Reconcile(dockSettings.MonitorConfigs, _monitorService.GetMonitors()))
        {
            _settingsService.SaveSettings(settings, true);
        }

        var configs = GetEffectiveConfigs(dockSettings);
        var desiredMonitorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var config in configs)
        {
            if (!config.Enabled)
            {
                continue;
            }

            var monitor = _monitorService.GetMonitorByDeviceId(config.MonitorDeviceId);
            if (monitor is null)
            {
                continue;
            }

            desiredMonitorIds.Add(config.MonitorDeviceId);

            if (!_dockWindows.ContainsKey(config.MonitorDeviceId))
            {
                CreateDockForMonitor(monitor, config, dockSettings);
            }
        }

        // Remove dock windows for monitors that are no longer desired
        List<string> toRemove = [];
        foreach (var id in _dockWindows.Keys)
        {
            if (!desiredMonitorIds.Contains(id))
            {
                toRemove.Add(id);
            }
        }

        foreach (var id in toRemove)
        {
            if (_dockWindows.Remove(id, out var window))
            {
                window.Close();
            }
        }
    }

    private void CreateDockForMonitor(MonitorInfo monitor, DockMonitorConfig config, DockSettings dockSettings)
    {
        // Pass the per-monitor override (nullable) so EffectiveSide follows
        // the global setting dynamically when no override is configured.
        var window = new DockWindow(monitor, config.Side);
        _dockWindows[config.MonitorDeviceId] = window;
        window.Show();
    }

    private void OnMonitorsChanged()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                SyncDocksToSettings();
            }
        });
    }

    private void OnSettingsChanged(SettingsModel sender, object? args)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!_disposed)
            {
                SyncDocksToSettings();
            }
        });
    }

    /// <summary>
    /// Returns the effective list of monitor configs. If the settings have
    /// no explicit configs (legacy / first-run), synthesizes one for the
    /// primary monitor.
    /// </summary>
    private List<DockMonitorConfig> GetEffectiveConfigs(DockSettings dockSettings)
    {
        if (dockSettings.MonitorConfigs.Count > 0)
        {
            return dockSettings.MonitorConfigs;
        }

        // Legacy / migration path: no per-monitor configs saved yet.
        // Synthesize a config for the primary monitor using the global Side.
        var primary = _monitorService.GetPrimaryMonitor();
        return
        [
            new DockMonitorConfig
            {
                MonitorDeviceId = primary.DeviceId,
                Enabled = true,
                Side = null, // Inherit from global Side
            },
        ];
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

        foreach (var (_, window) in _dockWindows)
        {
            window.Dispose();
        }

        _dockWindows.Clear();
    }
}
