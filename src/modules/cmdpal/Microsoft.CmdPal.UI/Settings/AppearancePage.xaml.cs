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
public sealed partial class AppearancePage : Page, IDisposable
{
    internal const string QuickAccessShelfSettingsElementTag = "QuickAccessShelf";

    private const int SettingsExpanderAnimationDurationMs = 250;

    private readonly TaskScheduler _mainTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();

    private bool _quickAccessShelfNavigationPending;
    private bool _disposed;

    internal SettingsViewModel ViewModel { get; }

    public AppearancePage()
    {
        InitializeComponent();

        var themeService = App.Current.Services.GetRequiredService<IThemeService>();
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        ViewModel = new SettingsViewModel(topLevelCommandManager, _mainTaskScheduler, themeService, settingsService);
        Loaded += AppearancePage_Loaded;
    }

    public void Dispose()
    {
        _disposed = true;
        Loaded -= AppearancePage_Loaded;
        ViewModel.Dispose();
    }

    internal bool TryNavigateToSettingsElement(string elementTag)
    {
        if (_disposed || !string.Equals(elementTag, QuickAccessShelfSettingsElementTag, StringComparison.Ordinal))
        {
            return false;
        }

        _quickAccessShelfNavigationPending = true;
        NavigateToPendingSettingsElement();
        return true;
    }

    private void AppearancePage_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateToPendingSettingsElement();
    }

    private void NavigateToPendingSettingsElement()
    {
        if (_disposed || !_quickAccessShelfNavigationPending || !IsLoaded)
        {
            return;
        }

        _quickAccessShelfNavigationPending = false;
        CompactModeSettingsExpander.IsExpanded = true;
        _ = BringQuickAccessShelfSettingsIntoViewAsync();
    }

    private async Task BringQuickAccessShelfSettingsIntoViewAsync()
    {
        await Task.Delay(SettingsExpanderAnimationDurationMs);
        if (_disposed || !IsLoaded)
        {
            return;
        }

        QuickAccessShelfSettingsCard.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalOffset = -20,
        });
        _ = QuickAccessShelfToggle.Focus(FocusState.Programmatic);
    }

    private void OpenRecentItemsSettings_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(
            "General",
            SettingsPageElementTag: GeneralPage.RecentItemsSettingsElementTag));
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
