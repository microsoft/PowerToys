// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Microsoft.CmdPal.UI.ViewModels.Dock;

public partial class DockWindowViewModel : ObservableObject, IDisposable
{
    private readonly IThemeService _themeService;
    private readonly DispatcherQueue _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread()!;

    [ObservableProperty]
    public partial ImageSource? BackgroundImageSource { get; private set; }

    [ObservableProperty]
    public partial Stretch BackgroundImageStretch { get; private set; } = Stretch.Fill;

    [ObservableProperty]
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
    public partial bool ShowColorizationOverlay { get; private set; }

    [ObservableProperty]
    public partial Color ColorizationColor { get; private set; }

    [ObservableProperty]
    public partial double ColorizationOpacity { get; private set; }

    public DockWindowViewModel(IThemeService themeService)
    {
        _themeService = themeService;
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
        UpdateFromThemeSnapshot();
    }

    private void ThemeService_ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        _uiDispatcherQueue.TryEnqueue(UpdateFromThemeSnapshot);
    }

    private void UpdateFromThemeSnapshot()
    {
        var snapshot = _themeService.CurrentDockTheme;

        BackgroundImageSource = snapshot.BackgroundImageSource;
        BackgroundImageStretch = snapshot.BackgroundImageStretch;
        BackgroundImageOpacity = snapshot.BackgroundImageOpacity;

        BackgroundImageBrightness = snapshot.BackgroundBrightness;
        BackgroundImageTint = snapshot.Tint;
        BackgroundImageTintIntensity = snapshot.TintIntensity;
        BackgroundImageBlurAmount = snapshot.BlurAmount;

        ShowBackgroundImage = BackgroundImageSource != null;

        // Colorization overlay for transparent backdrop
        ShowColorizationOverlay = snapshot.Backdrop == DockBackdrop.Transparent && snapshot.TintIntensity > 0;
        ColorizationColor = snapshot.Tint;
        ColorizationOpacity = snapshot.TintIntensity;
    }

    public void Dispose()
    {
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        GC.SuppressFinalize(this);
    }
}
