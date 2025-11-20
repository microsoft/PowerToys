// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;
#pragma warning disable SA1402 // File may only contain a single type

public partial class DockBandSettingsViewModel : ObservableObject
{
    private readonly SettingsModel _settingsModel;
    private readonly DockBandSettings _dockSettingsModel;
    private readonly TopLevelViewModel _adapter;
    private readonly DockBandViewModel? _bandViewModel;

    public string Title => _adapter.Title;

    public string Description
    {
        get
        {
            // TODO! we should have a way of saying "pinned from {extension}" vs
            // just a band that's from an extension
            List<string> parts = [_adapter.ExtensionName];

            // Add the number of items in the band
            var itemCount = NumItemsInBand();
            if (itemCount > 0)
            {
                var itemsString = itemCount == 1 ? "1 item" : $"{itemCount} items"; // TODO!Loc
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
                _dockSettingsModel.ShowLabels = value switch
                {
                    ShowLabelsOption.Default => null,
                    ShowLabelsOption.ShowLabels => true,
                    ShowLabelsOption.HideLabels => false,
                    _ => null,
                };
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
    } // TODO! persist to settings

    private DockPinSide _pinSide;

    public int PinSideIndex
    {
        get => (int)PinSide;
        set => PinSide = (DockPinSide)value;
    }

    public DockBandSettingsViewModel(
        DockBandSettings dockSettingsModel,
        TopLevelViewModel topLevelAdapter,
        DockBandViewModel? bandViewModel,
        SettingsModel settingsModel)
    {
        _dockSettingsModel = dockSettingsModel;
        _adapter = topLevelAdapter;
        _bandViewModel = bandViewModel;
        _settingsModel = settingsModel;
        _pinSide = FetchPinSide();
        _showLabels = FetchShowLabels();
    }

    private DockPinSide FetchPinSide()
    {
        var dockSettings = _settingsModel.DockSettings;
        var inStart = dockSettings.StartBands.Any(b => b.Id == _dockSettingsModel.Id);
        if (inStart)
        {
            return DockPinSide.Start;
        }

        var inEnd = dockSettings.EndBands.Any(b => b.Id == _dockSettingsModel.Id);
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

        return _bandViewModel!.Items.Count;
    }

    private void Save()
    {
        SettingsModel.SaveSettings(_settingsModel);
    }

    private void UpdatePinSide(DockPinSide value)
    {
        OnPinSideChanged(value);
        OnPropertyChanged(nameof(PinSideIndex));
        OnPropertyChanged(nameof(PinSide));
    }

    public void SetBandPosition(DockPinSide side, int? index)
    {
        var dockSettings = _settingsModel.DockSettings;

        // Remove from both sides first
        dockSettings.StartBands.RemoveAll(b => b.Id == _dockSettingsModel.Id);
        dockSettings.EndBands.RemoveAll(b => b.Id == _dockSettingsModel.Id);

        // Add to the selected side
        switch (side)
        {
            case DockPinSide.Start:
                {
                    var insertIndex = index ?? dockSettings.StartBands.Count;
                    dockSettings.StartBands.Insert(insertIndex, _dockSettingsModel);
                    break;
                }

            case DockPinSide.End:
                {
                    var insertIndex = index ?? dockSettings.EndBands.Count;
                    dockSettings.EndBands.Insert(insertIndex, _dockSettingsModel);
                    break;
                }

            case DockPinSide.None:
            default:
                // Do nothing
                break;
        }

        Save();
    }

    private void OnPinSideChanged(DockPinSide value)
    {
        SetBandPosition(value, null);
    }
}

public enum DockPinSide
{
    None,
    Start,
    End,
}

public enum ShowLabelsOption
{
    Default,
    ShowLabels,
    HideLabels,
}

#pragma warning restore SA1402 // File may only contain a single type
