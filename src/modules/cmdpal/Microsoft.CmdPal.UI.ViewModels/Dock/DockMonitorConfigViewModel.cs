// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// ViewModel wrapping a <see cref="DockMonitorConfig"/> paired with its
/// <see cref="MonitorInfo"/>. Exposes bindable properties for the monitor
/// config UI and persists changes through <see cref="SettingsService"/>.
/// </summary>
public partial class DockMonitorConfigViewModel : ObservableObject
{
    private static readonly CompositeFormat ResolutionFormat = CompositeFormat.Parse("{0} \u00D7 {1}");

    private readonly DockMonitorConfig _config;
    private readonly MonitorInfo _monitorInfo;
    private readonly SettingsService _settingsService;

    public DockMonitorConfigViewModel(
        DockMonitorConfig config,
        MonitorInfo monitorInfo,
        SettingsService settingsService)
    {
        _config = config;
        _monitorInfo = monitorInfo;
        _settingsService = settingsService;
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
    /// Persists the change immediately.
    /// </summary>
    public bool IsEnabled
    {
        get => _config.Enabled;
        set
        {
            if (_config.Enabled != value)
            {
                _config.Enabled = value;
                Save();
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the side-override index for ComboBox binding.
    /// 0 = "Use default" (inherit), 1 = Left, 2 = Top, 3 = Right, 4 = Bottom.
    /// Persists the change immediately.
    /// </summary>
    public int SideOverrideIndex
    {
        get => _config.Side switch
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

            if (_config.Side != newSide)
            {
                _config.Side = newSide;
                Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSideOverride));
            }
        }
    }

    /// <summary>Gets a value indicating whether this monitor has a per-monitor side override.</summary>
    public bool HasSideOverride => _config.Side is not null;

    private void Save()
    {
        _settingsService.SaveSettings(_settingsService.CurrentSettings, true);
    }
}
