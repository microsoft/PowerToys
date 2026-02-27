// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
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
    private readonly SettingsModel _settings;
    private readonly DockSettings _dockSettings;
    private readonly UISettings _uiSettings;
    private readonly IThemeService _themeService;
    private readonly DispatcherQueueTimer _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly DispatcherQueue _uiDispatcher = DispatcherQueue.GetForCurrentThread();

    private ElementTheme? _elementThemeOverride;
    private Color _currentSystemAccentColor;

    public ObservableCollection<Color> Swatches => AppearanceSettingsViewModel.WindowsColorSwatches;

    public int ThemeIndex
    {
        get => (int)_dockSettings.Theme;
        set => Theme = (UserTheme)value;
    }

    public UserTheme Theme
    {
        get => _dockSettings.Theme;
        set
        {
            if (_dockSettings.Theme != value)
            {
                _dockSettings.Theme = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeIndex));
                Save();
            }
        }
    }

    public int BackdropIndex
    {
        get => (int)_dockSettings.Backdrop;
        set => Backdrop = (DockBackdrop)value;
    }

    public DockBackdrop Backdrop
    {
        get => _dockSettings.Backdrop;
        set
        {
            if (_dockSettings.Backdrop != value)
            {
                _dockSettings.Backdrop = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackdropIndex));
                Save();
            }
        }
    }

    public ColorizationMode ColorizationMode
    {
        get => _dockSettings.ColorizationMode;
        set
        {
            if (_dockSettings.ColorizationMode != value)
            {
                _dockSettings.ColorizationMode = value;
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
        get => (int)_dockSettings.ColorizationMode;
        set => ColorizationMode = (ColorizationMode)value;
    }

    public Color ThemeColor
    {
        get => _dockSettings.CustomThemeColor;
        set
        {
            if (_dockSettings.CustomThemeColor != value)
            {
                _dockSettings.CustomThemeColor = value;

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
        get => _dockSettings.CustomThemeColorIntensity;
        set
        {
            _dockSettings.CustomThemeColorIntensity = value;
            OnPropertyChanged();
            Save();
        }
    }

    public string BackgroundImagePath
    {
        get => _dockSettings.BackgroundImagePath ?? string.Empty;
        set
        {
            if (_dockSettings.BackgroundImagePath != value)
            {
                _dockSettings.BackgroundImagePath = value;
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
        get => _dockSettings.BackgroundImageOpacity;
        set
        {
            if (_dockSettings.BackgroundImageOpacity != value)
            {
                _dockSettings.BackgroundImageOpacity = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public int BackgroundImageBrightness
    {
        get => _dockSettings.BackgroundImageBrightness;
        set
        {
            if (_dockSettings.BackgroundImageBrightness != value)
            {
                _dockSettings.BackgroundImageBrightness = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public int BackgroundImageBlurAmount
    {
        get => _dockSettings.BackgroundImageBlurAmount;
        set
        {
            if (_dockSettings.BackgroundImageBlurAmount != value)
            {
                _dockSettings.BackgroundImageBlurAmount = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public BackgroundImageFit BackgroundImageFit
    {
        get => _dockSettings.BackgroundImageFit;
        set
        {
            if (_dockSettings.BackgroundImageFit != value)
            {
                _dockSettings.BackgroundImageFit = value;
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

    public bool IsCustomTintVisible => _dockSettings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.Image;

    public bool IsCustomTintIntensityVisible => _dockSettings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor or ColorizationMode.Image;

    public bool IsBackgroundControlsVisible => _dockSettings.ColorizationMode is ColorizationMode.Image;

    public bool IsNoBackgroundVisible => _dockSettings.ColorizationMode is ColorizationMode.None;

    public bool IsAccentColorControlsVisible => _dockSettings.ColorizationMode is ColorizationMode.WindowsAccentColor;

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
        && !string.IsNullOrWhiteSpace(BackgroundImagePath)
        && Uri.TryCreate(BackgroundImagePath, UriKind.RelativeOrAbsolute, out var uri)
            ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri)
            : null;

    public DockAppearanceSettingsViewModel(IThemeService themeService, SettingsModel settings)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += ThemeServiceOnThemeChanged;
        _settings = settings;
        _dockSettings = settings.DockSettings;

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += UiSettingsOnColorValuesChanged;
        UpdateAccentColor(_uiSettings);

        Reapply();

        IsColorizationDetailsExpanded = _dockSettings.ColorizationMode != ColorizationMode.None;
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
        SettingsModel.SaveSettings(_settings);
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
