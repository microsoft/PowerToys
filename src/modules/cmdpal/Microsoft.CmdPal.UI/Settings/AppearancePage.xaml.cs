// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
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
    private readonly IAudioCueService _audioCueService;

    internal SettingsViewModel ViewModel { get; }

    public AppearancePage()
    {
        InitializeComponent();

        var themeService = App.Current.Services.GetRequiredService<IThemeService>();
        var topLevelCommandManager = App.Current.Services.GetService<TopLevelCommandManager>()!;
        var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
        _audioCueService = App.Current.Services.GetRequiredService<IAudioCueService>();
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

    private void OpenCommandPalette_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, HWND.Null));
    }

    private void PreviewAudioCue_Click(object sender, RoutedEventArgs e)
    {
        if (TryGetAudioCue(sender, out var cue))
        {
            _audioCueService.Preview(cue);
        }
    }

    private void AudioCueSound_OptionHighlighted(object sender, AudioCueSoundOption option)
    {
        // Fired while the user browses the open dropdown (keyboard focus or pointer hover),
        // before any selection is committed.
        if (sender is FrameworkElement { Tag: AudioCueEffectSettingsViewModel effect } && option.SoundId is not null)
        {
            _audioCueService.Preview(effect.Cue, option.SoundId, effect.CustomSoundPath);
        }
    }

    private async void AudioCueSound_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // IsLoaded filters out the initial programmatic assignment made while the template binds.
        if (sender is not ComboBox { IsLoaded: true, Tag: AudioCueEffectSettingsViewModel effect } comboBox
            || comboBox.SelectedItem is not AudioCueSoundOption option)
        {
            return;
        }

        if (option.SoundId != AudioCueCatalog.CustomSoundId)
        {
            // Covers commits that had no focus-preview: closed-dropdown arrowing and mouse clicks.
            _audioCueService.Preview(effect.Cue, option.SoundId, effect.CustomSoundPath);
            return;
        }

        var path = await PickCustomSoundFileAsync();
        if (path is not null)
        {
            effect.SetCustomSoundPath(path);
            _audioCueService.Preview(effect.Cue);
        }
        else if (string.IsNullOrWhiteSpace(effect.CustomSoundPath))
        {
            // Nothing picked and no earlier file to fall back to; undo the switch to Custom.
            var previousOption = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as AudioCueSoundOption : null;
            comboBox.SelectedItem = previousOption ?? effect.SoundOptions[0];
        }
    }

    private async void BrowseCustomAudioCue_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: AudioCueEffectSettingsViewModel effect })
        {
            return;
        }

        var path = await PickCustomSoundFileAsync();
        if (path is not null)
        {
            effect.SetCustomSoundPath(path);
            _audioCueService.Preview(effect.Cue);
        }
    }

    private async Task<string?> PickCustomSoundFileAsync()
    {
        try
        {
            if (XamlRoot?.ContentIslandEnvironment is null)
            {
                return null;
            }

            var windowId = XamlRoot?.ContentIslandEnvironment?.AppWindowId ?? new WindowId(0);

            var picker = new FileOpenPicker(windowId)
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
            };

            string[] extensions = [".wav", ".mp3", ".m4a", ".aac", ".wma", ".flac"];
            foreach (var ext in extensions)
            {
                picker.FileTypeFilter!.Add(ext);
            }

            var file = await picker.PickSingleFileAsync()!;
            return file?.Path;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to pick custom audio cue sound", ex);
            return null;
        }
    }

    private static bool TryGetAudioCue(object sender, out AudioCue cue)
    {
        cue = default;
        if (sender is FrameworkElement { Tag: AudioCue taggedCue })
        {
            cue = taggedCue;
            return true;
        }

        return false;
    }
}
