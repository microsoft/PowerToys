// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

/// <summary>
/// ViewModel for a single band entry within a per-monitor band configuration.
/// Unlike <see cref="DockBandSettingsViewModel"/> which operates on global
/// <see cref="DockSettings"/> lists, this targets the band lists on a specific
/// <see cref="DockMonitorConfig"/>.
/// </summary>
public partial class MonitorBandSettingsViewModel : ObservableObject
{
    private readonly DockMonitorConfig _monitorConfig;
    private readonly string _providerId;
    private readonly string _commandId;
    private readonly SettingsService _settingsService;
    private DockPinSide _pinSide;

    public MonitorBandSettingsViewModel(
        string name,
        string providerId,
        string commandId,
        DockMonitorConfig monitorConfig,
        SettingsService settingsService)
    {
        Name = name;
        _providerId = providerId;
        _commandId = commandId;
        _monitorConfig = monitorConfig;
        _settingsService = settingsService;

        _pinSide = FetchPinSide();
    }

    /// <summary>Gets the display name of the band.</summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets a value indicating whether this band is pinned on the monitor.
    /// When enabled, pins to Center. When disabled, removes from all sides.
    /// </summary>
    public bool IsPinned
    {
        get => _pinSide != DockPinSide.None;
        set
        {
            if (value && _pinSide == DockPinSide.None)
            {
                PinSide = DockPinSide.Center;
            }
            else if (!value && _pinSide != DockPinSide.None)
            {
                PinSide = DockPinSide.None;
            }
        }
    }

    /// <summary>
    /// Gets or sets the pin side index for ComboBox binding.
    /// 0 = Start, 1 = Center, 2 = End.
    /// </summary>
    public int PinSideIndex
    {
        get => _pinSide switch
        {
            DockPinSide.Start => 0,
            DockPinSide.Center => 1,
            DockPinSide.End => 2,
            _ => 1,
        };
        set
        {
            var side = value switch
            {
                0 => DockPinSide.Start,
                1 => DockPinSide.Center,
                2 => DockPinSide.End,
                _ => DockPinSide.Center,
            };

            PinSide = side;
        }
    }

    private DockPinSide PinSide
    {
        get => _pinSide;
        set
        {
            if (value != _pinSide)
            {
                ApplyPinSide(value);
                _pinSide = value;
                OnPropertyChanged(nameof(PinSide));
                OnPropertyChanged(nameof(PinSideIndex));
                OnPropertyChanged(nameof(IsPinned));
            }
        }
    }

    private DockPinSide FetchPinSide()
    {
        if (_monitorConfig.StartBands is not null)
        {
            foreach (var b in _monitorConfig.StartBands)
            {
                if (b.CommandId == _commandId)
                {
                    return DockPinSide.Start;
                }
            }
        }

        if (_monitorConfig.CenterBands is not null)
        {
            foreach (var b in _monitorConfig.CenterBands)
            {
                if (b.CommandId == _commandId)
                {
                    return DockPinSide.Center;
                }
            }
        }

        if (_monitorConfig.EndBands is not null)
        {
            foreach (var b in _monitorConfig.EndBands)
            {
                if (b.CommandId == _commandId)
                {
                    return DockPinSide.End;
                }
            }
        }

        return DockPinSide.None;
    }

    private void ApplyPinSide(DockPinSide newSide)
    {
        // Remove from all monitor band lists
        _monitorConfig.StartBands?.RemoveAll(b => b.CommandId == _commandId);
        _monitorConfig.CenterBands?.RemoveAll(b => b.CommandId == _commandId);
        _monitorConfig.EndBands?.RemoveAll(b => b.CommandId == _commandId);

        if (newSide == DockPinSide.None)
        {
            Save();
            return;
        }

        var entry = new DockBandSettings
        {
            ProviderId = _providerId,
            CommandId = _commandId,
        };

        switch (newSide)
        {
            case DockPinSide.Start:
                _monitorConfig.StartBands ??= new List<DockBandSettings>();
                _monitorConfig.StartBands.Add(entry);
                break;
            case DockPinSide.Center:
                _monitorConfig.CenterBands ??= new List<DockBandSettings>();
                _monitorConfig.CenterBands.Add(entry);
                break;
            case DockPinSide.End:
                _monitorConfig.EndBands ??= new List<DockBandSettings>();
                _monitorConfig.EndBands.Add(entry);
                break;
        }

        Save();
    }

    private void Save()
    {
        _settingsService.SaveSettings(_settingsService.CurrentSettings, true);
    }
}
