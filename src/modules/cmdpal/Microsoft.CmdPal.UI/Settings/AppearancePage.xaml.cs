// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Windows.Storage.Pickers;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AppearancePage : Page
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    internal SettingsViewModel ViewModel { get; }

    public AppearancePage()
    {
        InitializeComponent();

        var themeService = App.Current.Services.GetRequiredService<IThemeService>();
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        ViewModel = new SettingsViewModel(topLevelCommandManager, _mainTaskScheduler, themeService, settingsService);
    }

    private async void PickBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (XamlRoot?.ContentIslandEnvironment is null)
            {
                return;
            }

            var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId ?? new WindowId(0);

            var picker = new FileOpenPicker(windowId)
            {
                CommitButtonText = ViewModels.Properties.Resources.builtin_settings_appearance_pick_background_image_title!,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail,
            };

            foreach (var ext in BackgroundImagePathResolver.SupportedImageExtensions)
            {
                picker.FileTypeFilter!.Add(ext);
            }

            var file = await picker.PickSingleFileAsync()!;
            if (file != null)
            {
                ViewModel.Appearance.ColorizationMode = ColorizationMode.Image;
                ViewModel.Appearance.BackgroundImagePath = file.Path ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick background image file", ex);
        }
    }

    private async void PickBackgroundImageFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (XamlRoot?.ContentIslandEnvironment is null)
            {
                return;
            }

            var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId ?? new WindowId(0);

            var picker = new FolderPicker(windowId)
            {
                CommitButtonText = ViewModels.Properties.Resources.builtin_settings_appearance_pick_background_folder_title!,
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail,
            };

            var folder = await picker.PickSingleFolderAsync()!;
            if (folder != null)
            {
                ViewModel.Appearance.ColorizationMode = ColorizationMode.Slideshow;
                ViewModel.Appearance.BackgroundImageSlideshowFolderPath = folder.Path ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick background image folder", ex);
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

    private void OpenCommandPalette_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, HWND.Null));
    }
}
