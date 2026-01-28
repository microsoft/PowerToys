// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly DispatcherQueue _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread()!;

    [ObservableProperty]
    public partial ImageSource? BackgroundImageSource { get; private set; }

    [ObservableProperty]
    public partial Stretch BackgroundImageStretch { get; private set; } = Stretch.Fill;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveImageOpacity))]
    public partial double BackgroundImageOpacity { get; private set; }

    [ObservableProperty]
    public partial Color BackgroundImageTint { get; private set; }

    [ObservableProperty]
    public partial double BackgroundImageTintIntensity { get; private set; }

    [ObservableProperty]
    public partial int BackgroundImageBlurAmount { get; private set; }

    [ObservableProperty]
    public partial double BackgroundImageBrightness { get; private set; }

    [ObservableProperty]
    public partial bool ShowBackgroundImage { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveBackdropStyle))]
    [NotifyPropertyChangedFor(nameof(EffectiveImageOpacity))]
    public partial BackdropStyle BackdropStyle { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveBackdropStyle))]
    [NotifyPropertyChangedFor(nameof(EffectiveImageOpacity))]
    public partial float BackdropOpacity { get; private set; } = 1.0f;

    // Returns null when no transparency needed (BlurImageControl uses this to decide source type)
    public BackdropStyle? EffectiveBackdropStyle =>
        BackdropStyle == BackdropStyle.Clear || BackdropOpacity < 1.0f
            ? BackdropStyle
            : null;

    // When transparency is enabled, use square root curve so image stays visible longer as backdrop fades
    public double EffectiveImageOpacity =>
        EffectiveBackdropStyle is not null
            ? BackgroundImageOpacity * Math.Sqrt(BackdropOpacity)
            : BackgroundImageOpacity;

    public MainWindowViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
    }

    private void ThemeService_ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _uiDispatcherQueue.TryEnqueue(() =>
        {
            BackgroundImageSource = _themeService.Current.BackgroundImageSource;
            BackgroundImageStretch = _themeService.Current.BackgroundImageStretch;
            BackgroundImageOpacity = _themeService.Current.BackgroundImageOpacity;

            BackgroundImageBrightness = _themeService.Current.BackgroundBrightness;
            BackgroundImageTint = _themeService.Current.Tint;
            BackgroundImageTintIntensity = _themeService.Current.TintIntensity;
            BackgroundImageBlurAmount = _themeService.Current.BlurAmount;

            BackdropStyle = _themeService.Current.BackdropParameters.Style;
            BackdropOpacity = _themeService.Current.BackdropOpacity;

            ShowBackgroundImage = BackgroundImageSource != null;
        });
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        GC.SuppressFinalize(this);
    }
}
