// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// ViewModel wrapping a <see cref="DockMonitorConfig"/> paired with its
/// <see cref="MonitorInfo"/>. Exposes bindable properties for the monitor
/// config UI and persists changes through <see cref="ISettingsService"/>.
/// </summary>
public partial class DockMonitorConfigViewModel : ObservableObject
{
    private static readonly CompositeFormat ResolutionFormat = CompositeFormat.Parse("{0} \u00D7 {1}");

    private readonly MonitorInfo _monitorInfo;
    private readonly ISettingsService _settingsService;
    private readonly string _monitorDeviceId;

    public DockMonitorConfigViewModel(
        DockMonitorConfig config,
        MonitorInfo monitorInfo,
        ISettingsService settingsService)
    {
        _monitorInfo = monitorInfo;
        _settingsService = settingsService;
        _monitorDeviceId = config.MonitorDeviceId;
    }

    /// <summary>Gets the human-readable display name from the monitor hardware.</summary>
    public string DisplayName => _monitorInfo.DisplayName;

    /// <summary>Gets the stable device identifier for this monitor.</summary>
    public string DeviceId => _monitorInfo.DeviceId;

    /// <summary>Gets a value indicating whether this is the primary monitor.</summary>
    public bool IsPrimary => _monitorInfo.IsPrimary;

    /// <summary>Gets the monitor resolution formatted as "W × H".</summary>
    public string Resolution => string.Format(
        CultureInfo.CurrentCulture,
        ResolutionFormat,
        _monitorInfo.Bounds.Width,
        _monitorInfo.Bounds.Height);

    /// <summary>
    /// Gets or sets a value indicating whether the dock is enabled on this monitor.
    /// </summary>
    public bool IsEnabled
    {
        get => GetConfig()?.Enabled ?? true;
        set
        {
            UpdateConfig(c => c with { Enabled = value });
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the side-override index for ComboBox binding.
    /// 0 = "Use default" (inherit), 1 = Left, 2 = Top, 3 = Right, 4 = Bottom.
    /// </summary>
    public int SideOverrideIndex
    {
        get => GetConfig()?.Side switch
        {
            null => 0,
            DockSide.Left => 1,
            DockSide.Top => 2,
            DockSide.Right => 3,
            DockSide.Bottom => 4,
            _ => 0,
        };
        set
        {
            var newSide = value switch
            {
                1 => (DockSide?)DockSide.Left,
                2 => (DockSide?)DockSide.Top,
                3 => (DockSide?)DockSide.Right,
                4 => (DockSide?)DockSide.Bottom,
                _ => null,
            };

            UpdateConfig(c => c with { Side = newSide });
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSideOverride));
        }
    }

    /// <summary>Gets a value indicating whether this monitor has a per-monitor side override.</summary>
    public bool HasSideOverride => GetConfig()?.Side is not null;

    /// <summary>
    /// Gets or sets a value indicating whether this monitor uses custom band pinning.
    /// When toggled ON, forks band lists from global settings.
    /// When toggled OFF, clears per-monitor bands.
    /// </summary>
    public bool IsCustomized
    {
        get => GetConfig()?.IsCustomized ?? false;
        set
        {
            _settingsService.UpdateSettings(s =>
            {
                var dockSettings = s.DockSettings;
                var configs = dockSettings.MonitorConfigs;
                var index = FindConfigIndex(configs);
                if (index < 0)
                {
                    return s;
                }

                var config = configs[index];
                DockMonitorConfig updated;

                if (value)
                {
                    updated = config.ForkFromGlobal(dockSettings);
                }
                else
                {
                    updated = config with
                    {
                        IsCustomized = false,
                        StartBands = ImmutableList<DockBandSettings>.Empty,
                        CenterBands = ImmutableList<DockBandSettings>.Empty,
                        EndBands = ImmutableList<DockBandSettings>.Empty,
                    };
                }

                return s with
                {
                    DockSettings = dockSettings with { MonitorConfigs = configs.SetItem(index, updated) },
                };
            });

            OnPropertyChanged();
        }
    }

    private DockMonitorConfig? GetConfig()
    {
        var configs = _settingsService.Settings.DockSettings.MonitorConfigs;
        for (var i = 0; i < configs.Count; i++)
        {
            if (string.Equals(configs[i].MonitorDeviceId, _monitorDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return configs[i];
            }
        }

        return null;
    }

    private void UpdateConfig(Func<DockMonitorConfig, DockMonitorConfig> transform)
    {
        _settingsService.UpdateSettings(s =>
        {
            var dockSettings = s.DockSettings;
            var configs = dockSettings.MonitorConfigs;
            var index = FindConfigIndex(configs);
            if (index < 0)
            {
                return s;
            }

            var updated = transform(configs[index]);
            return s with
            {
                DockSettings = dockSettings with { MonitorConfigs = configs.SetItem(index, updated) },
            };
        });
    }

    private int FindConfigIndex(ImmutableList<DockMonitorConfig> configs)
    {
        for (var i = 0; i < configs.Count; i++)
        {
            if (string.Equals(configs[i].MonitorDeviceId, _monitorDeviceId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
