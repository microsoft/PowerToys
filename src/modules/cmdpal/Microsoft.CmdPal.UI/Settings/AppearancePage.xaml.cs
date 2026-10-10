// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AppearancePage : Page, IDisposable
{
    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private CompactPositionPickerWindow? _compactPositionPickerWindow;
    private bool _isOpeningCompactPositionPicker;

    internal SettingsViewModel ViewModel { get; }

    public AppearancePage()
    {
        InitializeComponent();

        var themeService = App.Current.Services.GetRequiredService<IThemeService>();
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        var languageService = App.Current.Services.GetRequiredService<ILanguageService>();
        ViewModel = new SettingsViewModel(topLevelCommandManager, _mainTaskScheduler, themeService, settingsService, languageService);
        Unloaded += AppearancePage_Unloaded;
    }

    public void Dispose() => ViewModel.Dispose();

    private void AppearancePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _compactPositionPickerWindow?.Close();
        _compactPositionPickerWindow = null;
    }

    private void OpenRecentItemsSettings_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(
            SettingsLinkId: SettingsLinkIds.Appearance.HomeRecentCommands));
    }

    private async void OpenCompactPositionPicker_Click(object sender, RoutedEventArgs e)
    {
        if (_compactPositionPickerWindow is not null)
        {
            _compactPositionPickerWindow.Activate();
            return;
        }

        if (_isOpeningCompactPositionPicker || XamlRoot?.ContentIslandEnvironment is null)
        {
            return;
        }

        _isOpeningCompactPositionPicker = true;
        OpenCompactPositionPickerButton.IsEnabled = false;

        try
        {
            var windowId = XamlRoot.ContentIslandEnvironment.AppWindowId;
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Nearest) ?? DisplayArea.Primary;
            using var softwareBitmap = await DesktopScreenshotHelper.CaptureAsync(displayArea.OuterBounds);
            if (!IsLoaded)
            {
                return;
            }

            SoftwareBitmapSource? screenshotSource = null;
            if (softwareBitmap is not null)
            {
                screenshotSource = new SoftwareBitmapSource();
                await screenshotSource.SetBitmapAsync(softwareBitmap);
            }

            var picker = new CompactPositionPickerWindow(
                displayArea,
                screenshotSource,
                ViewModel.CompactCenterHeightPercentage);
            picker.PositionSaved += percentage => ViewModel.CompactCenterHeightPercentage = percentage;
            picker.Closed += (_, _) =>
            {
                if (ReferenceEquals(_compactPositionPickerWindow, picker))
                {
                    _compactPositionPickerWindow = null;
                }

                _ = OpenCompactPositionPickerButton.Focus(FocusState.Programmatic);
            };

            _compactPositionPickerWindow = picker;
            picker.Activate();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open the compact position picker", ex);
        }
        finally
        {
            _isOpeningCompactPositionPicker = false;
            if (IsLoaded)
            {
                OpenCompactPositionPickerButton.IsEnabled = true;
            }
        }
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

            string[] extensions = [".png", ".bmp", ".jpg", ".jpeg", ".jfif", ".gif", ".tiff", ".tif", ".webp", ".jxr"];
            foreach (var ext in extensions)
            {
                picker.FileTypeFilter!.Add(ext);
            }

            var file = await picker.PickSingleFileAsync()!;
            if (file != null)
            {
                ViewModel.Appearance.BackgroundImagePath = file.Path ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick background image file", ex);
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

    private void OpenSystemSettings_Click(object sender, RoutedEventArgs e)
    {
        // Hyperlink with NavigateUri won't work for this URI, so we have to do it manually.
        _ = global::Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:notifications"));
    }

    private void OpenCommandPalette_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, HWND.Null));
    }
}
