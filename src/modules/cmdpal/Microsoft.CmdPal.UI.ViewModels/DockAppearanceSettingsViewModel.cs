// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.ViewModels;

/// <summary>
/// View model for dock appearance settings, controlling theme, backdrop, colorization,
/// and background image settings for the dock.
/// </summary>
public sealed partial class DockAppearanceSettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly UISettings _uiSettings;
    private readonly IThemeService _themeService;
    private readonly DispatcherQueueTimer _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly DispatcherQueue _uiDispatcher = DispatcherQueue.GetForCurrentThread();

    private ElementTheme? _elementThemeOverride;
    private Color _currentSystemAccentColor;

    public ObservableCollection<Color> Swatches => AppearanceSettingsViewModel.WindowsColorSwatches;

    public int ThemeIndex
    {
        get => (int)_settingsService.Settings.DockSettings.Theme;
        set => Theme = (UserTheme)value;
    }

    public UserTheme Theme
    {
        get => _settingsService.Settings.DockSettings.Theme;
        set
        {
            if (_settingsService.Settings.DockSettings.Theme != value)
            {
                _settingsService.Settings.DockSettings.Theme = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeIndex));
                Save();
            }
        }
    }

    public int BackdropIndex
    {
        get => (int)_settingsService.Settings.DockSettings.Backdrop;
        set => Backdrop = (DockBackdrop)value;
    }

    public DockBackdrop Backdrop
    {
        get => _settingsService.Settings.DockSettings.Backdrop;
        set
        {
            if (_settingsService.Settings.DockSettings.Backdrop != value)
            {
                _settingsService.Settings.DockSettings.Backdrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackdropIndex));
                Save();
            }
        }
    }

    public ColorizationMode ColorizationMode
    {
        get => _settingsService.Settings.DockSettings.ColorizationMode;
        set
        {
            if (_settingsService.Settings.DockSettings.ColorizationMode != value)
            {
                _settingsService.Settings.DockSettings.ColorizationMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ColorizationModeIndex));
                OnPropertyChanged(nameof(IsCustomTintVisible));
                OnPropertyChanged(nameof(IsCustomTintIntensityVisible));
                OnPropertyChanged(nameof(IsBackgroundControlsVisible));
                OnPropertyChanged(nameof(IsNoBackgroundVisible));
                OnPropertyChanged(nameof(IsAccentColorControlsVisible));

                if (value == ColorizationMode.WindowsAccentColor)
                {
                    ThemeColor = _currentSystemAccentColor;
                }

                IsColorizationDetailsExpanded = value != ColorizationMode.None;

                Save();
            }
        }
    }

    public int ColorizationModeIndex
    {
        get => (int)_settingsService.Settings.DockSettings.ColorizationMode;
        set => ColorizationMode = (ColorizationMode)value;
    }

    public Color ThemeColor
    {
        get => _settingsService.Settings.DockSettings.CustomThemeColor;
        set
        {
            if (_settingsService.Settings.DockSettings.CustomThemeColor != value)
            {
                _settingsService.Settings.DockSettings.CustomThemeColor = value;

                OnPropertyChanged();

                if (ColorIntensity == 0)
                {
                    ColorIntensity = 100;
                }

                Save();
            }
        }
    }

    public int ColorIntensity
    {
        get => _settingsService.Settings.DockSettings.CustomThemeColorIntensity;
        set
        {
            _settingsService.Settings.DockSettings.CustomThemeColorIntensity = value;
            OnPropertyChanged();
            Save();
        }
    }

    public string BackgroundImagePath
    {
        get => _settingsService.Settings.DockSettings.BackgroundImagePath ?? string.Empty;
        set
        {
            if (_settingsService.Settings.DockSettings.BackgroundImagePath != value)
            {
                _settingsService.Settings.DockSettings.BackgroundImagePath = value;
                OnPropertyChanged();

                if (BackgroundImageOpacity == 0)
                {
                    BackgroundImageOpacity = 100;
                }

                Save();
            }
        }
    }

    public int BackgroundImageOpacity
    {
        get => _settingsService.Settings.DockSettings.BackgroundImageOpacity;
        set
        {
            if (_settingsService.Settings.DockSettings.BackgroundImageOpacity != value)
            {
                _settingsService.Settings.DockSettings.BackgroundImageOpacity = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public int BackgroundImageBrightness
    {
        get => _settingsService.Settings.DockSettings.BackgroundImageBrightness;
        set
        {
            if (_settingsService.Settings.DockSettings.BackgroundImageBrightness != value)
            {
                _settingsService.Settings.DockSettings.BackgroundImageBrightness = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public int BackgroundImageBlurAmount
    {
        get => _settingsService.Settings.DockSettings.BackgroundImageBlurAmount;
        set
        {
            if (_settingsService.Settings.DockSettings.BackgroundImageBlurAmount != value)
            {
                _settingsService.Settings.DockSettings.BackgroundImageBlurAmount = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public BackgroundImageFit BackgroundImageFit
    {
        get => _settingsService.Settings.DockSettings.BackgroundImageFit;
        set
        {
            if (_settingsService.Settings.DockSettings.BackgroundImageFit != value)
            {
                _settingsService.Settings.DockSettings.BackgroundImageFit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundImageFitIndex));
                Save();
            }
        }
    }

    public int BackgroundImageFitIndex
    {
        get => BackgroundImageFit switch
        {
            BackgroundImageFit.Fill => 1,
            _ => 0,
        };
        set => BackgroundImageFit = value switch
        {
            1 => BackgroundImageFit.Fill,
            _ => BackgroundImageFit.UniformToFill,
        };
    }

    [ObservableProperty]
    public partial bool IsColorizationDetailsExpanded { get; set; }

    public bool IsCustomTintVisible => _settingsService.Settings.DockSettings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.Image;

    public bool IsCustomTintIntensityVisible => _settingsService.Settings.DockSettings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor or ColorizationMode.Image;

    public bool IsBackgroundControlsVisible => _settingsService.Settings.DockSettings.ColorizationMode is ColorizationMode.Image;

    public bool IsNoBackgroundVisible => _settingsService.Settings.DockSettings.ColorizationMode is ColorizationMode.None;

    public bool IsAccentColorControlsVisible => _settingsService.Settings.DockSettings.ColorizationMode is ColorizationMode.WindowsAccentColor;

    public ElementTheme EffectiveTheme => _elementThemeOverride ?? _themeService.Current.Theme;

    public Color EffectiveThemeColor => ColorizationMode switch
    {
        ColorizationMode.WindowsAccentColor => _currentSystemAccentColor,
        ColorizationMode.CustomColor or ColorizationMode.Image => ThemeColor,
        _ => Colors.Transparent,
    };

    // Since the blur amount is absolute, we need to scale it down for the preview (which is smaller than full screen).
    public int EffectiveBackgroundImageBlurAmount => (int)Math.Round(BackgroundImageBlurAmount / 4f);

    public double EffectiveBackgroundImageBrightness => BackgroundImageBrightness / 100.0;

    public ImageSource? EffectiveBackgroundImageSource =>
        ColorizationMode is ColorizationMode.Image
        && BackgroundImagePathResolver.ResolvePreviewImagePath(BackgroundImagePath) is string imagePath
        && Uri.TryCreate(imagePath, UriKind.RelativeOrAbsolute, out var uri)
            ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                !uri.IsAbsoluteUri && File.Exists(imagePath)
                    ? new Uri(Path.GetFullPath(imagePath))
                    : uri)
            : null;

    public DockAppearanceSettingsViewModel(IThemeService themeService, ISettingsService settingsService)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += ThemeServiceOnThemeChanged;
        _settingsService = settingsService;

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += UiSettingsOnColorValuesChanged;
        UpdateAccentColor(_uiSettings);

        Reapply();

        IsColorizationDetailsExpanded = _settingsService.Settings.DockSettings.ColorizationMode != ColorizationMode.None;
    }

    private void UiSettingsOnColorValuesChanged(UISettings sender, object args) => _uiDispatcher.TryEnqueue(() => UpdateAccentColor(sender));

    private void UpdateAccentColor(UISettings sender)
    {
        _currentSystemAccentColor = sender.GetColorValue(UIColorType.Accent);
        if (ColorizationMode == ColorizationMode.WindowsAccentColor)
        {
            ThemeColor = _currentSystemAccentColor;
        }
    }

    private void ThemeServiceOnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _saveTimer.Debounce(Reapply, TimeSpan.FromMilliseconds(200));
    }

    private void Save()
    {
        _settingsService.Save();
        _saveTimer.Debounce(Reapply, TimeSpan.FromMilliseconds(200));
    }

    private void Reapply()
    {
        OnPropertyChanged(nameof(EffectiveBackgroundImageBrightness));
        OnPropertyChanged(nameof(EffectiveBackgroundImageSource));
        OnPropertyChanged(nameof(EffectiveThemeColor));
        OnPropertyChanged(nameof(EffectiveBackgroundImageBlurAmount));

        // LOAD BEARING:
        // We need to cycle through the EffectiveTheme property to force reload of resources.
        _elementThemeOverride = ElementTheme.Light;
        OnPropertyChanged(nameof(EffectiveTheme));
        _elementThemeOverride = ElementTheme.Dark;
        OnPropertyChanged(nameof(EffectiveTheme));
        _elementThemeOverride = null;
        OnPropertyChanged(nameof(EffectiveTheme));
    }

    [RelayCommand]
    private void ResetBackgroundImageProperties()
    {
        BackgroundImageBrightness = 0;
        BackgroundImageBlurAmount = 0;
        BackgroundImageFit = BackgroundImageFit.UniformToFill;
        BackgroundImageOpacity = 100;
        ColorIntensity = 0;
    }

    public void Dispose()
    {
        _uiSettings.ColorValuesChanged -= UiSettingsOnColorValuesChanged;
        _themeService.ThemeChanged -= ThemeServiceOnThemeChanged;
    }
}
