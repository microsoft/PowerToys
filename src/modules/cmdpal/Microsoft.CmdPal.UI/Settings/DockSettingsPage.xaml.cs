// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class DockSettingsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel viewModel;

    public List<DockBandSettingsViewModel> AllDockBandItems => GetAllBandSettings();

    public DockSettingsPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;

        viewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService);

        // Initialize UI state
        InitializeSettings();
    }

    private void InitializeSettings()
    {
        // Initialize UI controls to match current settings
        DockSizeComboBox.SelectedIndex = SelectedDockSizeIndex;
        DockPositionComboBox.SelectedIndex = SelectedSideIndex;
        BackdropComboBox.SelectedIndex = SelectedBackdropIndex;
    }

    // Property bindings for ComboBoxes
    public int SelectedDockSizeIndex
    {
        get => DockSizeToSelectedIndex(viewModel.Dock_DockSize);
        set => viewModel.Dock_DockSize = SelectedIndexToDockSize(value);
    }

    public int SelectedSideIndex
    {
        get => SideToSelectedIndex(viewModel.Dock_Side);
        set => viewModel.Dock_Side = SelectedIndexToSide(value);
    }

    public int SelectedBackdropIndex
    {
        get => BackdropToSelectedIndex(viewModel.Dock_Backdrop);
        set => viewModel.Dock_Backdrop = SelectedIndexToBackdrop(value);
    }

    public bool ShowLabels
    {
        get => viewModel.Dock_ShowLabels;
        set => viewModel.Dock_ShowLabels = value;
    }

    // Conversion methods for ComboBox bindings
    private static int DockSizeToSelectedIndex(DockSize size) => size switch
    {
        DockSize.Small => 0,
        DockSize.Medium => 1,
        DockSize.Large => 2,
        _ => 0,
    };

    private static DockSize SelectedIndexToDockSize(int index) => index switch
    {
        0 => DockSize.Small,
        1 => DockSize.Medium,
        2 => DockSize.Large,
        _ => DockSize.Small,
    };

    private static int SideToSelectedIndex(DockSide side) => side switch
    {
        DockSide.Left => 0,
        DockSide.Top => 1,
        DockSide.Right => 2,
        DockSide.Bottom => 3,
        _ => 1,
    };

    private static DockSide SelectedIndexToSide(int index) => index switch
    {
        0 => DockSide.Left,
        1 => DockSide.Top,
        2 => DockSide.Right,
        3 => DockSide.Bottom,
        _ => DockSide.Top,
    };

    private static int BackdropToSelectedIndex(DockBackdrop backdrop) => backdrop switch
    {
        DockBackdrop.Mica => 0,
        DockBackdrop.Transparent => 1,
        DockBackdrop.Acrylic => 2,
        _ => 2,
    };

    private static DockBackdrop SelectedIndexToBackdrop(int index) => index switch
    {
        0 => DockBackdrop.Mica,
        1 => DockBackdrop.Transparent,
        2 => DockBackdrop.Acrylic,
        _ => DockBackdrop.Acrylic,
    };

    private List<TopLevelViewModel> GetAllBands()
    {
        var allBands = new List<TopLevelViewModel>();

        var tlcManager = App.Current.Services.GetService<TopLevelCommandManager>()!;

        foreach (var item in tlcManager.DockBands)
        {
            if (item.IsDockBand)
            {
                allBands.Add(item);
            }
        }

        return allBands;
    }

    private List<DockBandSettingsViewModel> GetAllBandSettings()
    {
        var allSettings = new List<DockBandSettingsViewModel>();

        // var allBands = GetAllBands();
        var tlcManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var settingsModel = App.Current.Services.GetService<SettingsModel>()!;
        var dockViewModel = App.Current.Services.GetService<DockViewModel>()!;
        var allBands = tlcManager.DockBands;
        foreach (var band in allBands)
        {
            var setting = band.DockBandSettings;
            if (setting is not null)
            {
                var bandVm = dockViewModel.FindBandByTopLevel(band);
                allSettings.Add(new(
                    dockSettingsModel: setting,
                    topLevelAdapter: band,
                    bandViewModel: bandVm,
                    settingsModel: settingsModel
                ));
            }
        }

        return allSettings;
    }
}
