// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Deferred;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// A helper control which takes an <see cref="IconSource"/> and creates the corresponding <see cref="IconElement"/>.
/// </summary>
public partial class IconBox : ContentControl
{
    private const double DefaultIconFontSize = 16.0;

    private double _lastScale;
    private ElementTheme _lastTheme;
    private double _lastFontSize;
    private int _sourceRevision;
    private int _resolvedSourceRevision = -1;
    private int _fallbackLoadingRevision = -1;
    private bool _isDisplayingFallbackSource;
    private bool _isDisplayingLoadingSource;
    private BitmapImage? _trackedBitmapImageSource;
    private SvgImageSource? _trackedSvgImageSource;

    // Prevent the previous IconSource (and its backing stream / SoftwareBitmap)
    // from being GC'd while XAML's internal async pipelines (e.g.
    // AsyncCopyToSurfaceTask, BitmapImage decode) are still in flight.
    // Releasing one cycle later is safe — by the next Source change the
    // prior async work is long complete.
    private IconSource? _previousSource;

    /// <summary>
    /// Gets or sets the <see cref="IconSource"/> to display within the <see cref="IconBox"/>. Overwritten, if <see cref="SourceKey"/> is used instead.
    /// </summary>
    public IconSource? Source
    {
        get => (IconSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    // Using a DependencyProperty as the backing store for Source.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(IconSource), typeof(IconBox), new PropertyMetadata(null, OnSourcePropertyChanged));

    /// <summary>
    /// Gets or sets a value to use as the <see cref="SourceKey"/> to retrieve an <see cref="IconSource"/> to set as the <see cref="Source"/>.
    /// </summary>
    public object? SourceKey
    {
        get => (object?)GetValue(SourceKeyProperty);
        set => SetValue(SourceKeyProperty, value);
    }

    // Using a DependencyProperty as the backing store for SourceKey.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty SourceKeyProperty =
        DependencyProperty.Register(nameof(SourceKey), typeof(object), typeof(IconBox), new PropertyMetadata(null, OnSourceKeyPropertyChanged));

    /// <summary>
    /// Gets or sets a temporary value to use as the <see cref="SourceKey"/> while the primary <see cref="SourceKey"/> is being resolved.
    /// This is useful in virtualized lists to avoid showing the previous item's icon for too long.
    /// </summary>
    public object? LoadingSourceKey
    {
        get => (object?)GetValue(LoadingSourceKeyProperty);
        set => SetValue(LoadingSourceKeyProperty, value);
    }

    // Using a DependencyProperty as the backing store for LoadingSourceKey.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty LoadingSourceKeyProperty =
        DependencyProperty.Register(nameof(LoadingSourceKey), typeof(object), typeof(IconBox), new PropertyMetadata(null, OnLoadingSourceKeyPropertyChanged));

    /// <summary>
    /// Gets or sets a fallback value to use as the <see cref="SourceKey"/> when the primary source cannot be resolved.
    /// </summary>
    public object? FallbackSourceKey
    {
        get => (object?)GetValue(FallbackSourceKeyProperty);
        set => SetValue(FallbackSourceKeyProperty, value);
    }

    // Using a DependencyProperty as the backing store for FallbackSourceKey.  This enables animation, styling, binding, etc...
    public static readonly DependencyProperty FallbackSourceKeyProperty =
        DependencyProperty.Register(nameof(FallbackSourceKey), typeof(object), typeof(IconBox), new PropertyMetadata(null, OnFallbackSourceKeyPropertyChanged));

    private TypedEventHandler<IconBox, SourceRequestedEventArgs>? _sourceRequested;

    /// <summary>
    /// Gets or sets the <see cref="SourceRequested"/> event handler to provide the value of the <see cref="IconSource"/> for the <see cref="Source"/> property from the provided <see cref="SourceKey"/>.
    /// </summary>
    public event TypedEventHandler<IconBox, SourceRequestedEventArgs>? SourceRequested
    {
        add
        {
            _sourceRequested += value;
            if (_sourceRequested?.GetInvocationList().Length == 1)
            {
                Refresh();
            }
#if DEBUG
            if (_sourceRequested?.GetInvocationList().Length > 1)
            {
                Logger.LogWarning("There shouldn't be more than one handler for IconBox.SourceRequested");
            }
#endif
        }
        remove => _sourceRequested -= value;
    }

    public IconBox()
    {
        TabFocusNavigation = KeyboardNavigationMode.Once;
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ActualThemeChanged += OnActualThemeChanged;
        SizeChanged += OnSizeChanged;

        UpdateLastFontSize();
    }

    private void UpdateLastFontSize()
    {
        _lastFontSize =
            Pick(Width)
            ?? Pick(Height)
            ?? Pick(ActualWidth)
            ?? Pick(ActualHeight)
            ?? DefaultIconFontSize;

        return;

        static double? Pick(double value) => double.IsFinite(value) && value > 0 ? value : null;
    }

    private void OnSizeChanged(object s, SizeChangedEventArgs e)
    {
        UpdateLastFontSize();

        if (Source is FontIconSource fontIcon)
        {
            fontIcon.FontSize = _lastFontSize;
            UpdatePaddingForFontIcon();
        }
    }

    private void UpdatePaddingForFontIcon() => Padding = new Thickness(Math.Round(_lastFontSize * -0.2));

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        if (_lastTheme == ActualTheme)
        {
            return;
        }

        _lastTheme = ActualTheme;
        Refresh();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _lastTheme = ActualTheme;
        UpdateLastFontSize();

        if (XamlRoot is not null)
        {
            _lastScale = XamlRoot.RasterizationScale;
            XamlRoot.Changed += OnXamlRootChanged;
        }

        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            XamlRoot.Changed -= OnXamlRootChanged;
        }

        DetachImageSourceFailureHandlers();
        _previousSource = null;
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        var newScale = sender.RasterizationScale;
        var changedLastTheme = _lastTheme != ActualTheme;
        var changedScale = Math.Abs(newScale - _lastScale) > 0.01;

        _lastScale = newScale;
        _lastTheme = ActualTheme;

        if ((changedLastTheme || changedScale) && SourceKey is not null)
        {
            UpdateSourceKey(this, SourceKey);
        }
    }

    private void Refresh()
    {
        UpdateSourceKey(this, SourceKey);
    }

    private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        // Prevent the outgoing source from being GC'd while XAML's async
        // image pipeline may still reference it (see _previousSource comment).
        self._previousSource = e.OldValue as IconSource;
        self.DetachImageSourceFailureHandlers();

        switch (e.NewValue)
        {
            case null:
                // Don't set Content to null — swapping between null and non-null
                // Content on a data-bound IconSourceElement inside a ListViewTemplate
                // causes a XAML crash.  Use an empty BitmapIconSource as a sentinel
                // instead.  See the original native workaround in
                // Microsoft.Terminal.UI/IconPathConverter.cpp (_getIconSource).
                var emptySource = new BitmapIconSource { UriSource = null };
                if (self.Content is IconSourceElement emptyElement)
                {
                    emptyElement.IconSource = emptySource;
                }
                else
                {
                    self.Content = emptySource.CreateIconElement();
                }

                self.Padding = default;
                break;
            case FontIconSource fontIcon:
                self.UpdateLastFontSize();
                fontIcon.FontSize = self._lastFontSize;
                if (self.Content is IconSourceElement iconSourceElement)
                {
                    iconSourceElement.IconSource = fontIcon;
                }
                else
                {
                    self.Content = fontIcon.CreateIconElement();
                }

                self.UpdatePaddingForFontIcon();

                break;
            case ImageIconSource imageIcon:
                if (self.ShouldTrackImageSourceFailures())
                {
                    self.AttachImageSourceFailureHandlers(imageIcon.ImageSource);
                }

                if (self.Content is IconSourceElement iconSourceElement3)
                {
                    iconSourceElement3.IconSource = imageIcon;
                }
                else
                {
                    self.Content = imageIcon.CreateIconElement();
                }

                self.Padding = default;

                break;
            case BitmapIconSource bitmapIcon:
                if (self.Content is IconSourceElement iconSourceElement2)
                {
                    iconSourceElement2.IconSource = bitmapIcon;
                }
                else
                {
                    self.Content = bitmapIcon.CreateIconElement();
                }

                self.Padding = default;

                break;

            case IconSource source:
                self.Content = source.CreateIconElement();
                self.Padding = default;
                break;

            default:
                throw new InvalidOperationException($"New value of {e.NewValue} is not of type IconSource.");
        }
    }

    private static void OnSourceKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        UpdateSourceKey(self, e.NewValue, showLoadingSource: !ReferenceEquals(e.OldValue, e.NewValue));
    }

    private static void OnLoadingSourceKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        if (self.SourceKey is not null && self.Source is null)
        {
            UpdateSourceKey(self, self.SourceKey, showLoadingSource: true);
        }
    }

    private static void OnFallbackSourceKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        if (self.SourceKey is not null)
        {
            UpdateSourceKey(self, self.SourceKey);
        }
    }

    private static void UpdateSourceKey(IconBox iconBox, object? sourceKey, bool showLoadingSource = false)
    {
        iconBox._sourceRevision++;
        iconBox._resolvedSourceRevision = -1;
        iconBox._fallbackLoadingRevision = -1;
        iconBox._isDisplayingFallbackSource = false;
        iconBox._isDisplayingLoadingSource = false;

        if (sourceKey is null)
        {
            iconBox.Source = null;
            return;
        }

        if (showLoadingSource)
        {
            BeginLoadingSourceRequest(iconBox, sourceKey, iconBox.LoadingSourceKey, iconBox._sourceRevision);
        }

        RequestIconFromSource(iconBox, sourceKey, iconBox._sourceRevision);
    }

    private static async void RequestIconFromSource(IconBox iconBox, object? sourceKey, int revision)
    {
        var fallbackSourceKey = iconBox.FallbackSourceKey;

        try
        {
            var iconBoxSourceRequestedHandler = iconBox._sourceRequested;

            if (iconBoxSourceRequestedHandler is null)
            {
                return;
            }

            var result = await ResolveIconSourceAsync(iconBox, iconBoxSourceRequestedHandler, sourceKey, fallbackSourceKey);

            // After the await:
            // Is the icon we're looking up now, the one we still
            // want to find? Since this IconBox might be used in a
            // list virtualization situation, it's very possible we
            // may have already been set to a new icon before we
            // even got back from the await.
            if (!IsCurrentRequest(iconBox, sourceKey, fallbackSourceKey, revision))
            {
                // If the requested icon has changed, then just bail
                return;
            }

            iconBox._isDisplayingFallbackSource = result.UsedFallback;
            iconBox._isDisplayingLoadingSource = false;
            iconBox._resolvedSourceRevision = revision;
            iconBox.Source = result.Source;
        }
        catch (Exception ex)
        {
            // Exception from TryEnqueue bypasses the global error handler,
            // and crashes the app.
            Logger.LogError("Failed to set icon", ex);
        }
    }

    private static async Task<ResolvedIconSource> ResolveIconSourceAsync(
        IconBox iconBox,
        TypedEventHandler<IconBox, SourceRequestedEventArgs> sourceRequestedHandler,
        object? sourceKey,
        object? fallbackSourceKey)
    {
        var tryFallback = fallbackSourceKey is not null && !ReferenceEquals(sourceKey, fallbackSourceKey);

        try
        {
            var primarySource = await RequestSourceAsync(iconBox, sourceRequestedHandler, sourceKey);
            if (primarySource is not null || !tryFallback)
            {
                return new(primarySource, false);
            }
        }
        catch when (tryFallback)
        {
            // Swallow the primary failure and try the fallback source key instead.
        }

        if (tryFallback)
        {
            return new(await RequestSourceAsync(iconBox, sourceRequestedHandler, fallbackSourceKey), true);
        }
        else
        {
            return new(null, false);
        }
    }

    private static async Task<IconSource?> RequestSourceAsync(
        IconBox iconBox,
        TypedEventHandler<IconBox, SourceRequestedEventArgs> sourceRequestedHandler,
        object? sourceKey)
    {
        if (sourceKey is null)
        {
            return null;
        }

        var scale = iconBox._lastScale > 0
            ? iconBox._lastScale
            : (iconBox.XamlRoot?.RasterizationScale > 0 ? iconBox.XamlRoot.RasterizationScale : 1.0);

        var eventArgs = new SourceRequestedEventArgs(sourceKey, iconBox._lastTheme, scale);
        await sourceRequestedHandler.InvokeAsync(iconBox, eventArgs);
        return eventArgs.Value;
    }

    private static bool IsCurrentRequest(IconBox iconBox, object? sourceKey, object? fallbackSourceKey, int revision) =>
        revision == iconBox._sourceRevision &&
        ReferenceEquals(sourceKey, iconBox.SourceKey) &&
        ReferenceEquals(fallbackSourceKey, iconBox.FallbackSourceKey);

    private static void BeginLoadingSourceRequest(IconBox iconBox, object? sourceKey, object? loadingSourceKey, int revision)
    {
        if (loadingSourceKey is null ||
            ReferenceEquals(sourceKey, loadingSourceKey) ||
            iconBox._sourceRequested is not { } sourceRequestedHandler)
        {
            return;
        }

        _ = ApplyLoadingSourceAsync(iconBox, sourceRequestedHandler, sourceKey, loadingSourceKey, revision);
    }

    private static async Task ApplyLoadingSourceAsync(
        IconBox iconBox,
        TypedEventHandler<IconBox, SourceRequestedEventArgs> sourceRequestedHandler,
        object? sourceKey,
        object? loadingSourceKey,
        int revision)
    {
        try
        {
            var loadingSource = await RequestSourceAsync(iconBox, sourceRequestedHandler, loadingSourceKey);
            if (!IsCurrentLoadingRequest(iconBox, sourceKey, loadingSourceKey, revision))
            {
                return;
            }

            iconBox._isDisplayingLoadingSource = true;
            iconBox.Source = loadingSource;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load temporary icon source", ex);
        }
    }

    private static bool IsCurrentLoadingRequest(IconBox iconBox, object? sourceKey, object? loadingSourceKey, int revision) =>
        revision == iconBox._sourceRevision &&
        iconBox._resolvedSourceRevision != revision &&
        ReferenceEquals(sourceKey, iconBox.SourceKey) &&
        ReferenceEquals(loadingSourceKey, iconBox.LoadingSourceKey);

    private bool ShouldTrackImageSourceFailures() =>
        FallbackSourceKey is not null &&
        !ReferenceEquals(SourceKey, FallbackSourceKey);

    private void AttachImageSourceFailureHandlers(ImageSource? imageSource)
    {
        switch (imageSource)
        {
            case BitmapImage bitmapImage:
                bitmapImage.ImageFailed += OnBitmapImageFailed;
                _trackedBitmapImageSource = bitmapImage;
                break;
            case SvgImageSource svgImageSource:
                svgImageSource.OpenFailed += OnSvgImageSourceOpenFailed;
                _trackedSvgImageSource = svgImageSource;
                break;
        }
    }

    private void DetachImageSourceFailureHandlers()
    {
        if (_trackedBitmapImageSource is not null)
        {
            _trackedBitmapImageSource.ImageFailed -= OnBitmapImageFailed;
            _trackedBitmapImageSource = null;
        }

        if (_trackedSvgImageSource is not null)
        {
            _trackedSvgImageSource.OpenFailed -= OnSvgImageSourceOpenFailed;
            _trackedSvgImageSource = null;
        }
    }

    private void OnBitmapImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (!ReferenceEquals(sender, _trackedBitmapImageSource))
        {
            return;
        }

        HandleTrackedImageSourceFailure(e.ErrorMessage);
    }

    private void OnSvgImageSourceOpenFailed(SvgImageSource sender, SvgImageSourceFailedEventArgs args)
    {
        if (!ReferenceEquals(sender, _trackedSvgImageSource))
        {
            return;
        }

        HandleTrackedImageSourceFailure($"SVG load status: {args.Status}");
    }

    private void HandleTrackedImageSourceFailure(string? errorMessage)
    {
        if (_isDisplayingFallbackSource ||
            _isDisplayingLoadingSource ||
            SourceKey is null ||
            FallbackSourceKey is null ||
            ReferenceEquals(SourceKey, FallbackSourceKey) ||
            _fallbackLoadingRevision == _sourceRevision)
        {
            return;
        }

        _fallbackLoadingRevision = _sourceRevision;
        Logger.LogWarning($"Image-backed icon source failed to render. Trying fallback icon source. Error: {errorMessage}");
        _ = ApplyFallbackAfterImageFailureAsync(_sourceRevision, SourceKey, FallbackSourceKey);
    }

    private async Task ApplyFallbackAfterImageFailureAsync(int revision, object? sourceKey, object? fallbackSourceKey)
    {
        try
        {
            if (fallbackSourceKey is null || _sourceRequested is not { } sourceRequestedHandler)
            {
                return;
            }

            var fallbackSource = await RequestSourceAsync(this, sourceRequestedHandler, fallbackSourceKey);
            if (!IsCurrentRequest(this, sourceKey, fallbackSourceKey, revision))
            {
                return;
            }

            _isDisplayingFallbackSource = true;
            Source = fallbackSource;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load fallback icon after image load failure", ex);
        }
        finally
        {
            if (revision == _sourceRevision && !_isDisplayingFallbackSource)
            {
                _fallbackLoadingRevision = -1;
            }
        }
    }

    private readonly record struct ResolvedIconSource(IconSource? Source, bool UsedFallback);
}
