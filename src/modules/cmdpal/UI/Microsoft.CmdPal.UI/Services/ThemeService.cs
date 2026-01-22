// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Microsoft.CmdPal.UI.Services;

/// <summary>
/// ThemeService is a hub that translates user settings and system preferences into concrete
/// theme resources and notifies listeners of changes.
/// </summary>
internal sealed partial class ThemeService : IThemeService, IDisposable
{
    private static readonly TimeSpan ReloadDebounceInterval = TimeSpan.FromMilliseconds(500);

    private readonly UISettings _uiSettings;
    private readonly SettingsModel _settings;
    private readonly ResourceSwapper _resourceSwapper;
    private readonly NormalThemeProvider _normalThemeProvider;
    private readonly ColorfulThemeProvider _colorfulThemeProvider;

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _dispatcherQueueTimer;
    private bool _isInitialized;
    private bool _disposed;
    private InternalThemeState _currentState;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public ThemeSnapshot Current => Volatile.Read(ref _currentState).Snapshot;

    /// <summary>
    /// Initializes the theme service. Must be called after the application window is activated and on UI thread.
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (_dispatcherQueue is null)
        {
            throw new InvalidOperationException("Failed to get DispatcherQueue for the current thread. Ensure Initialize is called on the UI thread after window activation.");
        }

        _dispatcherQueueTimer = _dispatcherQueue.CreateTimer();

        _resourceSwapper.Initialize();
        _isInitialized = true;
        Reload();
    }

    private void Reload()
    {
        if (!_isInitialized)
        {
            return;
        }

        // provider selection
        var intensity = Math.Clamp(_settings.CustomThemeColorIntensity, 0, 100);
        IThemeProvider provider = intensity > 0 && _settings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor or ColorizationMode.Image
                ? _colorfulThemeProvider
                : _normalThemeProvider;

        // Calculate values
        var tint = _settings.ColorizationMode switch
        {
            ColorizationMode.CustomColor => _settings.CustomThemeColor,
            ColorizationMode.WindowsAccentColor => _uiSettings.GetColorValue(UIColorType.Accent),
            ColorizationMode.Image => _settings.CustomThemeColor,
            _ => Colors.Transparent,
        };
        var effectiveTheme = GetElementTheme((ElementTheme)_settings.Theme);
        var imageSource = _settings.ColorizationMode == ColorizationMode.Image
            ? LoadImageSafe(_settings.BackgroundImagePath)
            : null;
        var stretch = _settings.BackgroundImageFit switch
        {
            BackgroundImageFit.Fill => Stretch.Fill,
            _ => Stretch.UniformToFill,
        };
        var opacity = Math.Clamp(_settings.BackgroundImageOpacity, 0, 100) / 100.0;

        // create context and offload to actual theme provider
        var context = new ThemeContext
        {
            Tint = tint,
            ColorIntensity = intensity,
            Theme = effectiveTheme,
            BackgroundImageSource = imageSource,
            BackgroundImageStretch = stretch,
            BackgroundImageOpacity = opacity,
        };
        var backdrop = provider.GetAcrylicBackdrop(context);
        var blur = _settings.BackgroundImageBlurAmount;
        var brightness = _settings.BackgroundImageBrightness;

        // Create public snapshot (no provider!)
        var snapshot = new ThemeSnapshot
        {
            Tint = tint,
            TintIntensity = intensity / 100f,
            Theme = effectiveTheme,
            BackgroundImageSource = imageSource,
            BackgroundImageStretch = stretch,
            BackgroundImageOpacity = opacity,
            BackdropParameters = backdrop,
            BlurAmount = blur,
            BackgroundBrightness = brightness / 100f,
        };

        // Bundle with provider for internal use
        var newState = new InternalThemeState
        {
            Snapshot = snapshot,
            Provider = provider,
        };

        // Atomic swap
        Interlocked.Exchange(ref _currentState, newState);

        _resourceSwapper.TryActivateTheme(provider.ThemeKey);
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs());
    }

    private static BitmapImage? LoadImageSafe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            // If it looks like a file path and exists, prefer absolute file URI
            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out var uri))
            {
                return null;
            }

            if (!uri.IsAbsoluteUri && File.Exists(path))
            {
                uri = new Uri(Path.GetFullPath(path));
            }

            return new BitmapImage(uri);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Failed to load background image '{path}'. {ex.Message}");
            return null;
        }
    }

    public ThemeService(SettingsModel settings, ResourceSwapper resourceSwapper)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(resourceSwapper);

        _settings = settings;
        _settings.SettingsChanged += SettingsOnSettingsChanged;

        _resourceSwapper = resourceSwapper;

        _uiSettings = new UISettings();
        _uiSettings.ColorValuesChanged += UiSettings_ColorValuesChanged;

        _normalThemeProvider = new NormalThemeProvider(_uiSettings);
        _colorfulThemeProvider = new ColorfulThemeProvider(_uiSettings);
        List<IThemeProvider> providers = [_normalThemeProvider, _colorfulThemeProvider];

        foreach (var provider in providers)
        {
            _resourceSwapper.RegisterTheme(provider.ThemeKey, provider.ResourcePath);
        }

        _currentState = new InternalThemeState
        {
            Snapshot = new ThemeSnapshot
            {
                Tint = Colors.Transparent,
                Theme = ElementTheme.Light,
                BackdropParameters = new AcrylicBackdropParameters(Colors.Black, Colors.Black, 0.5f, 0.5f),
                BackgroundImageOpacity = 1,
                BackgroundImageSource = null,
                BackgroundImageStretch = Stretch.Fill,
                BlurAmount = 0,
                TintIntensity = 1.0f,
                BackgroundBrightness = 0,
            },
            Provider = _normalThemeProvider,
        };
    }

    private void RequestReload()
    {
        if (!_isInitialized || _dispatcherQueueTimer is null)
        {
            return;
        }

        _dispatcherQueueTimer.Debounce(Reload, ReloadDebounceInterval);
    }

    private ElementTheme GetElementTheme(ElementTheme theme)
    {
        return theme switch
        {
            ElementTheme.Light => ElementTheme.Light,
            ElementTheme.Dark => ElementTheme.Dark,
            _ => _uiSettings.GetColorValue(UIColorType.Background).CalculateBrightness() < 0.5
                ? ElementTheme.Dark
                : ElementTheme.Light,
        };
    }

    private void SettingsOnSettingsChanged(SettingsModel sender, object? args)
    {
        RequestReload();
    }

    private void UiSettings_ColorValuesChanged(UISettings sender, object args)
    {
        RequestReload();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcherQueueTimer?.Stop();
        _uiSettings.ColorValuesChanged -= UiSettings_ColorValuesChanged;
        _settings.SettingsChanged -= SettingsOnSettingsChanged;
    }

    private sealed class InternalThemeState
    {
        public required ThemeSnapshot Snapshot { get; init; }

        public required IThemeProvider Provider { get; init; }
    }
}
