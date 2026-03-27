// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
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
    private readonly ISettingsService _settingsService;

    private readonly ResourceSwapper _resourceSwapper;
    private readonly NormalThemeProvider _normalThemeProvider;
    private readonly ColorfulThemeProvider _colorfulThemeProvider;

    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _dispatcherQueueTimer;
    private bool _isInitialized;
    private bool _disposed;
    private int _rotateMainBackgroundOnActivation;
    private InternalThemeState _currentState;
    private DockThemeSnapshot _currentDockState;
    private string? _mainBackgroundFolderPath;
    private string? _mainBackgroundImagePath;
    private DateTimeOffset _mainBackgroundChangedAtUtc;
    private int _mainBackgroundSequentialIndex = -1;
    private int[]? _shuffledOrder;
    private int _shuffleCursor;

    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    public ThemeSnapshot Current => Volatile.Read(ref _currentState).Snapshot;

    public DockThemeSnapshot CurrentDockTheme => Volatile.Read(ref _currentDockState);

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

    public void RefreshThemeForActivation()
    {
        if (!_isInitialized || _settingsService.Settings.ColorizationMode != ColorizationMode.Slideshow)
        {
            return;
        }

        Interlocked.Exchange(ref _rotateMainBackgroundOnActivation, 1);
        Reload();
    }

    private void Reload()
    {
        if (!_isInitialized)
        {
            return;
        }

        var rotateMainBackgroundOnActivation = Interlocked.Exchange(ref _rotateMainBackgroundOnActivation, 0) == 1;

        // provider selection
        var themeColorIntensity = Math.Clamp(_settingsService.Settings.CustomThemeColorIntensity, 0, 100);
        var imageTintIntensity = Math.Clamp(_settingsService.Settings.BackgroundImageTintIntensity, 0, 100);
        var effectiveColorIntensity = _settingsService.Settings.ColorizationMode is ColorizationMode.Image or ColorizationMode.Slideshow
            ? imageTintIntensity
            : themeColorIntensity;

        IThemeProvider provider = UseColorfulProvider(effectiveColorIntensity) ? _colorfulThemeProvider : _normalThemeProvider;

        // Calculate values
        var tint = _settingsService.Settings.ColorizationMode switch
        {
            ColorizationMode.CustomColor => _settingsService.Settings.CustomThemeColor,
            ColorizationMode.WindowsAccentColor => _uiSettings.GetColorValue(UIColorType.Accent),
            ColorizationMode.Image => _settingsService.Settings.CustomThemeColor,
            ColorizationMode.Slideshow => _settingsService.Settings.CustomThemeColor,
            _ => Colors.Transparent,
        };
        var effectiveTheme = GetElementTheme((ElementTheme)_settingsService.Settings.Theme);
        var imageSource = _settingsService.Settings.ColorizationMode is ColorizationMode.Image or ColorizationMode.Slideshow
            ? LoadImageSafe(ResolveMainBackgroundImagePath(rotateMainBackgroundOnActivation))
            : null;
        var stretch = _settingsService.Settings.BackgroundImageFit switch
        {
            BackgroundImageFit.Fill => Stretch.Fill,
            _ => Stretch.UniformToFill,
        };
        var opacity = Math.Clamp(_settingsService.Settings.BackgroundImageOpacity, 0, 100) / 100.0;

        // create input and offload to actual theme provider
        var context = new ThemeContext
        {
            Tint = tint,
            ColorIntensity = effectiveColorIntensity,
            Theme = effectiveTheme,
            BackgroundImageSource = imageSource,
            BackgroundImageStretch = stretch,
            BackgroundImageOpacity = opacity,
            BackdropStyle = _settingsService.Settings.BackdropStyle,
            BackdropOpacity = Math.Clamp(_settingsService.Settings.BackdropOpacity, 0, 100) / 100f,
        };
        var backdrop = provider.GetBackdropParameters(context);
        var blur = _settingsService.Settings.BackgroundImageBlurAmount;
        var brightness = _settingsService.Settings.BackgroundImageBrightness;

        // Create public snapshot (no provider!)
        var hasColorization = effectiveColorIntensity > 0
            && _settingsService.Settings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor or ColorizationMode.Image or ColorizationMode.Slideshow;

        var snapshot = new ThemeSnapshot
        {
            Tint = tint,
            TintIntensity = effectiveColorIntensity / 100f,
            Theme = effectiveTheme,
            BackgroundImageSource = imageSource,
            BackgroundImageStretch = stretch,
            BackgroundImageOpacity = opacity,
            BackdropParameters = backdrop,
            BackdropOpacity = context.BackdropOpacity,
            BlurAmount = blur,
            BackgroundBrightness = brightness / 100f,
            HasColorization = hasColorization,
        };

        // Bundle with provider for internal use
        var newState = new InternalThemeState
        {
            Snapshot = snapshot,
            Provider = provider,
        };

        // Atomic swap
        Interlocked.Exchange(ref _currentState, newState);

        // Compute DockThemeSnapshot from DockSettings
        var dockSettings = _settingsService.Settings.DockSettings;
        var dockIntensity = Math.Clamp(dockSettings.CustomThemeColorIntensity, 0, 100);
        IThemeProvider dockProvider = dockIntensity > 0 && dockSettings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor or ColorizationMode.Image
                ? _colorfulThemeProvider
                : _normalThemeProvider;

        var dockTint = dockSettings.ColorizationMode switch
        {
            ColorizationMode.CustomColor => dockSettings.CustomThemeColor,
            ColorizationMode.WindowsAccentColor => _uiSettings.GetColorValue(UIColorType.Accent),
            ColorizationMode.Image => dockSettings.CustomThemeColor,
            _ => Colors.Transparent,
        };
        var dockEffectiveTheme = GetElementTheme((ElementTheme)dockSettings.Theme);
        var dockImageSource = dockSettings.ColorizationMode == ColorizationMode.Image
            ? LoadImageSafe(BackgroundImagePathResolver.ResolvePreviewImagePath(dockSettings.BackgroundImagePath))
            : null;
        var dockStretch = dockSettings.BackgroundImageFit switch
        {
            BackgroundImageFit.Fill => Stretch.Fill,
            _ => Stretch.UniformToFill,
        };
        var dockOpacity = Math.Clamp(dockSettings.BackgroundImageOpacity, 0, 100) / 100.0;

        var dockContext = new ThemeContext
        {
            Tint = dockTint,
            ColorIntensity = dockIntensity,
            Theme = dockEffectiveTheme,
            BackgroundImageSource = dockImageSource,
            BackgroundImageStretch = dockStretch,
            BackgroundImageOpacity = dockOpacity,
        };
        var dockBackdrop = dockProvider.GetBackdropParameters(dockContext);
        var dockBlur = dockSettings.BackgroundImageBlurAmount;
        var dockBrightness = dockSettings.BackgroundImageBrightness;

        var dockSnapshot = new DockThemeSnapshot
        {
            Tint = dockTint,
            TintIntensity = dockIntensity / 100f,
            Theme = dockEffectiveTheme,
            Backdrop = dockSettings.Backdrop,
            BackgroundImageSource = dockImageSource,
            BackgroundImageStretch = dockStretch,
            BackgroundImageOpacity = dockOpacity,
            BackdropParameters = dockBackdrop,
            BlurAmount = dockBlur,
            BackgroundBrightness = dockBrightness / 100f,
        };

        Interlocked.Exchange(ref _currentDockState, dockSnapshot);

        _resourceSwapper.TryActivateTheme(provider.ThemeKey);
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs());
    }

    private bool UseColorfulProvider(int effectiveColorIntensity)
    {
        return _settingsService.Settings.ColorizationMode == ColorizationMode.Image
               || _settingsService.Settings.ColorizationMode == ColorizationMode.Slideshow
               || (effectiveColorIntensity > 0 && _settingsService.Settings.ColorizationMode is ColorizationMode.CustomColor or ColorizationMode.WindowsAccentColor);
    }

    private static string? NormalizeConfiguredPath(string? configuredPath)
        => string.IsNullOrWhiteSpace(configuredPath) ? null : configuredPath.Trim();

    private string? ResolveMainBackgroundImagePath(bool rotateOnActivation)
    {
        if (_settingsService.Settings.ColorizationMode != ColorizationMode.Slideshow)
        {
            ResetMainBackgroundImageState();

            var configuredImagePath = NormalizeConfiguredPath(_settingsService.Settings.BackgroundImagePath);
            if (BackgroundImagePathResolver.TryGetLocalFolderPath(configuredImagePath, out _))
            {
                return BackgroundImagePathResolver.ResolvePreviewImagePath(configuredImagePath);
            }

            return configuredImagePath;
        }

        if (!BackgroundImagePathResolver.TryGetLocalFolderPath(NormalizeConfiguredPath(_settingsService.Settings.BackgroundImageSlideshowFolderPath), out var folderPath))
        {
            ResetMainBackgroundImageState();
            return null;
        }

        var files = BackgroundImagePathResolver.GetSupportedImageFiles(folderPath);
        if (files.Count == 0)
        {
            ResetMainBackgroundImageState();
            return null;
        }

        var folderChanged = !string.Equals(_mainBackgroundFolderPath, folderPath, StringComparison.OrdinalIgnoreCase);
        var hasSelection = !string.IsNullOrWhiteSpace(_mainBackgroundImagePath);
        var selectedPath = _mainBackgroundImagePath ?? string.Empty;
        var selectedImageStillValid =
            hasSelection
            && File.Exists(selectedPath)
            && files.Any(path => string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase));

        var intervalMinutes = Math.Max(_settingsService.Settings.BackgroundImageChangeIntervalMinutes, 0);
        var intervalElapsed = intervalMinutes == 0
            || (DateTimeOffset.UtcNow - _mainBackgroundChangedAtUtc) >= TimeSpan.FromMinutes(intervalMinutes);

        var shouldRotate =
            folderChanged
            || !selectedImageStillValid
            || (rotateOnActivation && intervalElapsed);

        if (!shouldRotate && selectedImageStillValid)
        {
            return _mainBackgroundImagePath;
        }

        var nextIndex = GetNextMainBackgroundImageIndex(files.Count, folderChanged, _settingsService.Settings.BackgroundImageShuffle);
        _mainBackgroundFolderPath = folderPath;
        _mainBackgroundSequentialIndex = nextIndex;
        _mainBackgroundImagePath = files[nextIndex];
        _mainBackgroundChangedAtUtc = DateTimeOffset.UtcNow;
        return _mainBackgroundImagePath;
    }

    private int GetNextMainBackgroundImageIndex(int fileCount, bool folderChanged, bool shuffle)
    {
        if (fileCount <= 1)
        {
            return 0;
        }

        if (shuffle)
        {
            if (folderChanged || _shuffledOrder is null || _shuffledOrder.Length != fileCount || _shuffleCursor >= _shuffledOrder.Length)
            {
                _shuffledOrder = FisherYatesShuffle(fileCount);
                _shuffleCursor = 0;

                // Avoid repeating the last image at the start of a new deck.
                if (!folderChanged
                    && _shuffledOrder.Length > 1
                    && _mainBackgroundSequentialIndex >= 0
                    && _shuffledOrder[0] == _mainBackgroundSequentialIndex)
                {
                    (_shuffledOrder[0], _shuffledOrder[^1]) = (_shuffledOrder[^1], _shuffledOrder[0]);
                }
            }

            return _shuffledOrder[_shuffleCursor++];
        }

        if (folderChanged || _mainBackgroundSequentialIndex < 0 || _mainBackgroundSequentialIndex >= fileCount)
        {
            return 0;
        }

        return (_mainBackgroundSequentialIndex + 1) % fileCount;
    }

    private static int[] FisherYatesShuffle(int count)
    {
        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
        }

        for (var i = count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
    }

    private void ResetMainBackgroundImageState()
    {
        _mainBackgroundFolderPath = null;
        _mainBackgroundImagePath = null;
        _mainBackgroundChangedAtUtc = default;
        _mainBackgroundSequentialIndex = -1;
        _shuffledOrder = null;
        _shuffleCursor = 0;
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

    public ThemeService(ResourceSwapper resourceSwapper, ISettingsService settingsService)
    {
        ArgumentNullException.ThrowIfNull(resourceSwapper);

        _settingsService = settingsService;
        _settingsService.SettingsChanged += SettingsOnSettingsChanged;

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
                BackdropParameters = new BackdropParameters(Colors.Black, Colors.Black, EffectiveOpacity: 0.5f, EffectiveLuminosityOpacity: 0.5f),
                BackdropOpacity = 1.0f,
                BackgroundImageOpacity = 1,
                BackgroundImageSource = null,
                BackgroundImageStretch = Stretch.Fill,
                BlurAmount = 0,
                TintIntensity = 1.0f,
                BackgroundBrightness = 0,
                HasColorization = false,
            },
            Provider = _normalThemeProvider,
        };

        _currentDockState = new DockThemeSnapshot
        {
            Tint = Colors.Transparent,
            TintIntensity = 1.0f,
            Theme = ElementTheme.Light,
            Backdrop = DockBackdrop.Acrylic,
            BackdropParameters = new BackdropParameters(Colors.Black, Colors.Black, 0.5f, 0.5f),
            BackgroundImageOpacity = 1,
            BackgroundImageSource = null,
            BackgroundImageStretch = Stretch.Fill,
            BlurAmount = 0,
            BackgroundBrightness = 0,
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

    private void SettingsOnSettingsChanged(ISettingsService sender, SettingsModel args)
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
        _settingsService.SettingsChanged -= SettingsOnSettingsChanged;
    }

    private sealed class InternalThemeState
    {
        public required ThemeSnapshot Snapshot { get; init; }

        public required IThemeProvider Provider { get; init; }
    }
}
