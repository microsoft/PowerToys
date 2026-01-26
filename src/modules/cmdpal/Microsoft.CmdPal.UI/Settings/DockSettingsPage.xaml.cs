// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Windows.Storage.Pickers;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class DockSettingsPage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    internal SettingsViewModel ViewModel { get; }

    public List<DockBandSettingsViewModel> AllDockBandItems => GetAllBandSettings();

    public DockSettingsPage()
    {
        this.InitializeComponent();

        var settings = App.Current.Services.GetService<SettingsModel>()!;
        var themeService = App.Current.Services.GetService<IThemeService>()!;
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;

        ViewModel = new SettingsViewModel(settings, topLevelCommandManager, _mainTaskScheduler, themeService);

        // Initialize UI state
        InitializeSettings();
    }

    private void InitializeSettings()
    {
        // Initialize UI controls to match current settings
        DockPositionComboBox.SelectedIndex = SelectedSideIndex;
        BackdropComboBox.SelectedIndex = SelectedBackdropIndex;
    }

    private async void PickBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (XamlRoot?.ContentIslandEnvironment is null)
            {
                return;
            }

            var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId ?? new Microsoft.UI.WindowId(0);

            var picker = new FileOpenPicker(windowId)
            {
                CommitButtonText = ViewModels.Properties.Resources.builtin_settings_appearance_pick_background_image_title!,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail,
            };

            string[] extensions = [".png", ".bmp", ".jpg", ".jpeg", ".jfif", ".gif", ".tiff", ".tif", ".webp", ".jxr"];
            foreach (var ext in extensions)
            {
                picker.FileTypeFilter!.Add(ext);
            }

            var file = await picker.PickSingleFileAsync()!;
            if (file != null)
            {
                ViewModel.DockAppearance.BackgroundImagePath = file.Path ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick background image file for dock", ex);
        }
    }

    private void OpenWindowsColorsSettings_Click(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        // LOAD BEARING (or BEAR LOADING?): Process.Start with UseShellExecute inside a XAML input event can trigger WinUI reentrancy
        // and cause FailFast crashes. Task.Run moves the call off the UI thread to prevent hard process termination.
        Task.Run(() =>
        {
            try
            {
                _ = Process.Start(new ProcessStartInfo("ms-settings:colors") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to open Windows Settings", ex);
            }
        });
    }

    // Property bindings for ComboBoxes
    public int SelectedDockSizeIndex
    {
        get => DockSizeToSelectedIndex(ViewModel.Dock_DockSize);
        set => ViewModel.Dock_DockSize = SelectedIndexToDockSize(value);
    }

    public int SelectedSideIndex
    {
        get => SideToSelectedIndex(ViewModel.Dock_Side);
        set => ViewModel.Dock_Side = SelectedIndexToSide(value);
    }

    public int SelectedBackdropIndex
    {
        get => BackdropToSelectedIndex(ViewModel.Dock_Backdrop);
        set => ViewModel.Dock_Backdrop = SelectedIndexToBackdrop(value);
    }

    public bool ShowLabels
    {
        get => ViewModel.Dock_ShowLabels;
        set => ViewModel.Dock_ShowLabels = value;
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
        DockBackdrop.Transparent => 0,
        DockBackdrop.Acrylic => 1,
        _ => 2,
    };

    private static DockBackdrop SelectedIndexToBackdrop(int index) => index switch
    {
        0 => DockBackdrop.Transparent,
        1 => DockBackdrop.Acrylic,
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
