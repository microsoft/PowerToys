// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class DockSettingsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private readonly SettingsViewModel viewModel;

    public DockSettingsPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        viewModel = new SettingsViewModel(settings, App.Current.Services, _mainTaskScheduler);

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
}
