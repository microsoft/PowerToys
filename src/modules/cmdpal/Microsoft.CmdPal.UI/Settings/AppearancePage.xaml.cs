// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.Windows.Storage.Pickers;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.UI.Settings;

/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AppearancePage : Page
{
    private readonly ILogger _logger;

    internal SettingsViewModel ViewModel { get; }

    public AppearancePage(
        SettingsViewModel settingsViewModel,
        ScreenPreview screenPreview,
        ILogger logger)
    {
        InitializeComponent();
        _logger = logger;
        ViewModel = settingsViewModel;

        BindScreenPreview(screenPreview);
    }

    private void BindScreenPreview(ScreenPreview screenPreview)
    {
        // Create CommandPalettePreview and set up bindings
        var commandPalettePreview = new CommandPalettePreview();
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundColorProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveBackdrop.TintColor"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundImageBlurAmountProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveBackgroundImageBlurAmount"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundImageBrightnessProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveBackgroundImageBrightness"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundImageFitProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.BackgroundImageFit"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundImageSourceProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveBackgroundImageSource"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundImageTintProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveThemeColor"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundImageTintIntensityProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.ColorIntensity"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(CommandPalettePreview.PreviewBackgroundOpacityProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveBackdrop.TintOpacity"), Mode = BindingMode.OneWay });
        commandPalettePreview.SetBinding(FrameworkElement.RequestedThemeProperty, new Binding { Source = ViewModel, Path = new PropertyPath("Appearance.EffectiveTheme"), Mode = BindingMode.OneWay });

        screenPreview.PreviewContent = commandPalettePreview;
        ScreenPreviewContainer.Child = screenPreview;
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
            Log_FailureToPickBackgroundImage(ex);
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
                Log_FailureToOpenWindowsSettings(ex);
            }
        });
    }

    private void OpenCommandPalette_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, HWND.Null));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to open Windows Settings")]
    partial void Log_FailureToOpenWindowsSettings(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to pick background image file")]
    partial void Log_FailureToPickBackgroundImage(Exception ex);
}
