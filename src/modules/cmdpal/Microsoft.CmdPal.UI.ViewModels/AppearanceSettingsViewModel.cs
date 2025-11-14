// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class AppearanceSettingsViewModel : ObservableObject, IDisposable
{
    private readonly SettingsModel _settings;
    private readonly UISettings _uiSettings;
    private readonly IThemeService _themeService;
    private readonly DispatcherQueueTimer _saveTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
    private readonly DispatcherQueue _uiDispatcher = DispatcherQueue.GetForCurrentThread();

    private ElementTheme? _elementThemeOverride;
    private Color _currentSystemAccentColor;

    public ObservableCollection<Color> Swatches { get; } =
    [

        // row 0
        Color.FromArgb(255, 255, 185, 0), // #ffb900
        Color.FromArgb(255, 255, 140, 0), // #ff8c00
        Color.FromArgb(255, 247, 99, 12), // #f7630c
        Color.FromArgb(255, 202, 80, 16), // #ca5010
        Color.FromArgb(255, 218, 59, 1), // #da3b01
        Color.FromArgb(255, 239, 105, 80), // #ef6950

        // row 1
        Color.FromArgb(255, 209, 52, 56), // #d13438
        Color.FromArgb(255, 255, 67, 67), // #ff4343
        Color.FromArgb(255, 231, 72, 86), // #e74856
        Color.FromArgb(255, 232, 17, 35), // #e81123
        Color.FromArgb(255, 234, 0, 94), // #ea005e
        Color.FromArgb(255, 195, 0, 82), // #c30052

        // row 2
        Color.FromArgb(255, 227, 0, 140), // #e3008c
        Color.FromArgb(255, 191, 0, 119), // #bf0077
        Color.FromArgb(255, 194, 57, 179), // #c239b3
        Color.FromArgb(255, 154, 0, 137), // #9a0089
        Color.FromArgb(255, 0, 120, 212), // #0078d4
        Color.FromArgb(255, 0, 99, 177), // #0063b1

        // row 3
        Color.FromArgb(255, 142, 140, 216), // #8e8cd8
        Color.FromArgb(255, 107, 105, 214), // #6b69d6
        Color.FromArgb(255, 135, 100, 184), // #8764b8
        Color.FromArgb(255, 116, 77, 169), // #744da9
        Color.FromArgb(255, 177, 70, 194), // #b146c2
        Color.FromArgb(255, 136, 23, 152), // #881798

        // row 4
        Color.FromArgb(255, 0, 153, 188), // #0099bc
        Color.FromArgb(255, 45, 125, 154), // #2d7d9a
        Color.FromArgb(255, 0, 183, 195), // #00b7c3
        Color.FromArgb(255, 3, 131, 135), // #038387
        Color.FromArgb(255, 0, 178, 148), // #00b294
        Color.FromArgb(255, 1, 133, 116), // #018574

        // row 5
        Color.FromArgb(255, 0, 204, 106), // #00cc6a
        Color.FromArgb(255, 16, 137, 62), // #10893e
        Color.FromArgb(255, 122, 117, 116), // #7a7574
        Color.FromArgb(255, 93, 90, 88), // #5d5a58
        Color.FromArgb(255, 104, 118, 138), // #68768a
        Color.FromArgb(255, 81, 92, 107), // #515c6b

        // row 6
        Color.FromArgb(255, 86, 124, 115), // #567c73
        Color.FromArgb(255, 72, 104, 96), // #486860
        Color.FromArgb(255, 73, 130, 5), // #498205
        Color.FromArgb(255, 16, 124, 16), // #107c10
        Color.FromArgb(255, 118, 118, 118), // #767676
        Color.FromArgb(255, 76, 74, 72), // #4c4a48

        // row 7
        Color.FromArgb(255, 105, 121, 126), // #69797e
        Color.FromArgb(255, 74, 84, 89), // #4a5459
        Color.FromArgb(255, 100, 124, 100), // #647c64
        Color.FromArgb(255, 82, 94, 84), // #525e54
        Color.FromArgb(255, 132, 117, 69), // #847545
        Color.FromArgb(255, 126, 115, 95), // #7e735f
    ];

    public int ThemeIndex
    {
        get => (int)_settings.Theme;
        set => Theme = (UserTheme)value;
    }

    public UserTheme Theme
    {
        get => _settings.Theme;
        set
        {
            if (_settings.Theme != value)
            {
                _settings.Theme = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeIndex));
                Save();
            }
        }
    }

    public Brush ThemeColorBrush => new SolidColorBrush(ThemeColor);

    public ColorizationMode ColorizationMode
    {
        get => _settings.ColorizationMode;
        set
        {
            if (_settings.ColorizationMode != value)
            {
                _settings.ColorizationMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Colorize));
                OnPropertyChanged(nameof(ColorizationModeIndex));
                OnPropertyChanged(nameof(IsCustomTintVisible));
                OnPropertyChanged(nameof(IsCustomTintIntensityVisible));
                if (value == ColorizationMode.WindowsAccentColor)
                {
                    ThemeColor = _currentSystemAccentColor;
                }

                Save();
            }
        }
    }

    public bool IsCustomTintVisible => _settings.ColorizationMode == ColorizationMode.CustomColor;

    public bool IsCustomTintIntensityVisible => _settings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor;

    public int ColorizationModeIndex
    {
        get => (int)_settings.ColorizationMode;
        set => ColorizationMode = (ColorizationMode)value;
    }

    public bool Colorize
    {
        get => _settings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor;
        set => ColorizationMode = value ? ColorizationMode.CustomColor : ColorizationMode.None;
    }

    public Color ThemeColor
    {
        get => _settings.CustomThemeColor;
        set
        {
            if (_settings.CustomThemeColor != value)
            {
                var currentSwatch = SelectedColorSwatch;
                _settings.CustomThemeColor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeColorBrush));

                if (currentSwatch != SelectedColorSwatch)
                {
                    OnPropertyChanged(nameof(SelectedColorSwatch));
                }

                if (ColorizationMode != ColorizationMode.CustomColor)
                {
                    if (value != _currentSystemAccentColor)
                    {
                        ColorizationMode = ColorizationMode.CustomColor;
                    }
                }

                if (ColorIntensity == 0)
                {
                    ColorIntensity = 100;
                }

                if (!Colorize)
                {
                    Colorize = true;
                }

                Save();
            }
        }
    }

    public Color? SelectedColorSwatch
    {
        get => Swatches.FirstOrDefault(t => t == ThemeColor);
        set
        {
            if (value != ThemeColor && value != null && value != default(Color))
            {
                ThemeColor = value ?? Colors.Black;
            }
        }
    }

    public int ColorIntensity
    {
        get => _settings.CustomThemeColorIntensity;
        set
        {
            _settings.CustomThemeColorIntensity = value;
            OnPropertyChanged();
            Save();
        }
    }

    public string BackgroundImagePath
    {
        get => _settings.BackgroundImagePath ?? string.Empty;
        set
        {
            if (_settings.BackgroundImagePath != value)
            {
                _settings.BackgroundImagePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveBackgroundImageSource));

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
        get => _settings.BackgroundImageOpacity;
        set
        {
            if (_settings.BackgroundImageOpacity != value)
            {
                _settings.BackgroundImageOpacity = value;
                OnPropertyChanged();
                Save();
            }
        }
    }

    public BackgroundImageFit BackgroundImageFit
    {
        get => _settings.BackgroundImageFit;
        set
        {
            if (_settings.BackgroundImageFit != value)
            {
                _settings.BackgroundImageFit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BackgroundImageFitIndex));
                Save();
            }
        }
    }

    public int BackgroundImageFitIndex
    {
        // Naming between UI facing string and enum is a bit confusing, but the enum fields
        // are based on XAML Stretch enum values. So I'm choosing to keep the confusion here, close
        // to the UI.
        // - BackgroundImageFit.Fill corresponds to "Stretch"
        // - BackgroundImageFit.UniformToFill corresponds to "Fill"
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

    public AcrylicBackdropParameters EffectiveBackdrop { get; private set; } = new(Colors.Black, Colors.Black, 0.5f, 0.5f);

    public ElementTheme EffectiveTheme => _elementThemeOverride ?? _themeService.Current.Theme;

    public ImageSource? EffectiveBackgroundImageSource
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(BackgroundImagePath) && Uri.TryCreate(BackgroundImagePath, UriKind.RelativeOrAbsolute, out var uri)
                    ? new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(uri)
                    : null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }
    }

    public AppearanceSettingsViewModel(IThemeService themeService, SettingsModel settings)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += ThemeServiceOnThemeChanged;
        _settings = settings;

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += UiSettingsOnColorValuesChanged;
        _currentSystemAccentColor = _uiSettings.GetColorValue(UIColorType.Accent);

        Reapply();
    }

    private void UiSettingsOnColorValuesChanged(UISettings sender, object args)
    {
        _uiDispatcher.TryEnqueue(() =>
        {
            if (ColorizationMode == ColorizationMode.WindowsAccentColor)
            {
                _currentSystemAccentColor = sender.GetColorValue(UIColorType.Accent);
                ThemeColor = _currentSystemAccentColor;
                Reapply();
            }
        });
    }

    private void ThemeServiceOnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        Reapply();
    }

    private void Save()
    {
        _saveTimer.Debounce(
            () =>
            {
                SettingsModel.SaveSettings(_settings);
                Reapply();
            },
            TimeSpan.FromMilliseconds(200));
    }

    private void Reapply()
    {
        // Theme services recalculates effective color and opacity based on current settings.
        EffectiveBackdrop = _themeService.Current.BackdropParameters;
        OnPropertyChanged(nameof(EffectiveBackdrop));

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
    private void ResetBackgroundImage()
    {
        BackgroundImagePath = string.Empty;
    }

    public void Dispose()
    {
        _uiSettings.ColorValuesChanged -= UiSettingsOnColorValuesChanged;
        _themeService.ThemeChanged -= ThemeServiceOnThemeChanged;
    }
}
