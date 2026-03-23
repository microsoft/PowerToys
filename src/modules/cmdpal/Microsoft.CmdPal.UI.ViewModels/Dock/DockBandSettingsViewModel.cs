// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public partial class DockBandSettingsViewModel : ObservableObject
{
    private static readonly CompositeFormat PluralItemsFormatString = CompositeFormat.Parse(Properties.Resources.dock_item_count_plural);
    private readonly ISettingsService _settingsService;
    private readonly TopLevelViewModel _adapter;
    private readonly DockBandViewModel? _bandViewModel;

    private DockBandSettings _dockSettingsModel;

    public string Title => _adapter.Title;

    public string Description
    {
        get
        {
            List<string> parts = [_adapter.ExtensionName];

            // Add the number of items in the band
            var itemCount = NumItemsInBand();
            if (itemCount > 0)
            {
                var itemsString = itemCount == 1 ?
                    Properties.Resources.dock_item_count_singular :
                    string.Format(CultureInfo.CurrentCulture, PluralItemsFormatString, itemCount);
                parts.Add(itemsString);
            }

            return string.Join(" - ", parts);
        }
    }

    public string ProviderId => _adapter.CommandProviderId;

    public IconInfoViewModel Icon => _adapter.IconViewModel;

    private ShowLabelsOption _showLabels;

    public ShowLabelsOption ShowLabels
    {
        get => _showLabels;
        set
        {
            if (value != _showLabels)
            {
                _showLabels = value;
                var newShowTitles = value switch
                {
                    ShowLabelsOption.Default => (bool?)null,
                    ShowLabelsOption.ShowLabels => true,
                    ShowLabelsOption.HideLabels => false,
                    _ => null,
                };
                UpdateModel(_dockSettingsModel with { ShowTitles = newShowTitles });
                Save();
            }
        }
    }

    private ShowLabelsOption FetchShowLabels()
    {
        if (_dockSettingsModel.ShowLabels == null)
        {
            return ShowLabelsOption.Default;
        }

        return _dockSettingsModel.ShowLabels.Value ? ShowLabelsOption.ShowLabels : ShowLabelsOption.HideLabels;
    }

    // used to map to ComboBox selection
    public int ShowLabelsIndex
    {
        get => (int)ShowLabels;
        set => ShowLabels = (ShowLabelsOption)value;
    }

    private DockPinSide PinSide
    {
        get => _pinSide;
        set
        {
            if (value != _pinSide)
            {
                UpdatePinSide(value);
            }
        }
    }

    private DockPinSide _pinSide;

    public int PinSideIndex
    {
        get => (int)PinSide;
        set => PinSide = (DockPinSide)value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the band is pinned to the dock.
    /// When enabled, pins to Center. When disabled, removes from all sides.
    /// </summary>
    public bool IsPinned
    {
        get => PinSide != DockPinSide.None;
        set
        {
            if (value && PinSide == DockPinSide.None)
            {
                // Pin to Center by default when enabling
                PinSide = DockPinSide.Center;
            }
            else if (!value && PinSide != DockPinSide.None)
            {
                // Remove from dock when disabling
                PinSide = DockPinSide.None;
            }
        }
    }

    public DockBandSettingsViewModel(
        DockBandSettings dockSettingsModel,
        TopLevelViewModel topLevelAdapter,
        DockBandViewModel? bandViewModel,
        ISettingsService settingsService)
    {
        _dockSettingsModel = dockSettingsModel;
        _adapter = topLevelAdapter;
        _bandViewModel = bandViewModel;
        _settingsService = settingsService;
        _pinSide = FetchPinSide();
        _showLabels = FetchShowLabels();
    }

    private DockPinSide FetchPinSide()
    {
        var dockSettings = _settingsService.Settings.DockSettings;
        var inStart = dockSettings.StartBands.Any(b => b.CommandId == _dockSettingsModel.CommandId);
        if (inStart)
        {
            return DockPinSide.Start;
        }

        var inCenter = dockSettings.CenterBands.Any(b => b.CommandId == _dockSettingsModel.CommandId);
        if (inCenter)
        {
            return DockPinSide.Center;
        }

        var inEnd = dockSettings.EndBands.Any(b => b.CommandId == _dockSettingsModel.CommandId);
        if (inEnd)
        {
            return DockPinSide.End;
        }

        return DockPinSide.None;
    }

    private int NumItemsInBand()
    {
        var bandVm = _bandViewModel;
        if (bandVm is null)
        {
            return 0;
        }

        return bandVm.Items.Count;
    }

    private void Save()
    {
        _settingsService.UpdateSettings(s => s with { DockSettings = s.DockSettings });
    }

    private void UpdateModel(DockBandSettings newModel)
    {
        var commandId = _dockSettingsModel.CommandId;
        _settingsService.UpdateSettings(
            s =>
            {
                var dockSettings = s.DockSettings;
                return s with
                {
                    DockSettings = dockSettings with
                    {
                        StartBands = ReplaceInList(dockSettings.StartBands, commandId, newModel),
                        CenterBands = ReplaceInList(dockSettings.CenterBands, commandId, newModel),
                        EndBands = ReplaceInList(dockSettings.EndBands, commandId, newModel),
                    },
                };
            },
            hotReload: false);
        _dockSettingsModel = newModel;
    }

    private static ImmutableList<DockBandSettings> ReplaceInList(ImmutableList<DockBandSettings> list, string commandId, DockBandSettings newModel)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].CommandId == commandId)
            {
                return list.SetItem(i, newModel);
            }
        }

        return list;
    }

    private void UpdatePinSide(DockPinSide value)
    {
        OnPinSideChanged(value);
        OnPropertyChanged(nameof(PinSideIndex));
        OnPropertyChanged(nameof(PinSide));
        OnPropertyChanged(nameof(IsPinned));
    }

    public void SetBandPosition(DockPinSide side, int? index)
    {
        var commandId = _dockSettingsModel.CommandId;

        _settingsService.UpdateSettings(s =>
        {
            var dockSettings = s.DockSettings;

            // Remove from all sides first
            var newDock = dockSettings with
            {
                StartBands = dockSettings.StartBands.RemoveAll(b => b.CommandId == commandId),
                CenterBands = dockSettings.CenterBands.RemoveAll(b => b.CommandId == commandId),
                EndBands = dockSettings.EndBands.RemoveAll(b => b.CommandId == commandId),
            };

            // Add to the selected side
            newDock = side switch
            {
                DockPinSide.Start => newDock with { StartBands = newDock.StartBands.Insert(index ?? newDock.StartBands.Count, _dockSettingsModel) },
                DockPinSide.Center => newDock with { CenterBands = newDock.CenterBands.Insert(index ?? newDock.CenterBands.Count, _dockSettingsModel) },
                DockPinSide.End => newDock with { EndBands = newDock.EndBands.Insert(index ?? newDock.EndBands.Count, _dockSettingsModel) },
                _ => newDock,
            };

            return s with { DockSettings = newDock };
        });
    }

    private void OnPinSideChanged(DockPinSide value)
    {
        SetBandPosition(value, null);
        _pinSide = value;
    }
}

public enum DockPinSide
{
    None,
    Start,
    Center,
    End,
}

public enum ShowLabelsOption
{
    Default,
    ShowLabels,
    HideLabels,
}
