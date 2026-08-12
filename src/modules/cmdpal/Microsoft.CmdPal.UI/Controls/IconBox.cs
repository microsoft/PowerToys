// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Deferred;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Controls;

/// <summary>
/// A helper control which takes an <see cref="IconSource"/> and creates the corresponding <see cref="IconElement"/>.
/// </summary>
public partial class IconBox : ContentControl
{
    private const double DefaultIconFontSize = 16.0;
    private static long _nextDiagnosticId;
    private readonly IconPresentationState<IconSource> _presentation = new();

    private double _lastScale;
    private ElementTheme _lastTheme;
    private double _lastFontSize;
    private IconRefreshState _refreshState;
    private long _requestVersion;
    private IconRequestMeasurement _activeRequestDiagnostics;
    private IIconRequestDemand? _activeRequestDemand;

    // ImageIconSource does not render through IconSourceElement. Reassigning Source on
    // one realized Image left recycled rows intermittently blank in testing. Keep a
    // stable Grid and alternate inactive Image slots so Source changes occur only on a
    // collapsed element before it is shown.
    private Grid? _imagePresenter;
    private Image? _firstImageSlot;
    private Image? _secondImageSlot;
    private Image? _activeImageSlot;
    private bool _imagePresenterDisabled;
    private long _diagnosticId;
    private IconRequestSite _derivedRequestSite;
    private bool _hasDerivedRequestSite;

    /// <summary>
    /// Gets or sets the semantic UI surface used to group this control's diagnostic measurements.
    /// </summary>
    public IconRequestSite RequestSite { get; set; }

    /// <summary>
    /// Gets or sets an optional static developer-authored label that distinguishes placements within a <see cref="RequestSite"/>.
    /// Do not bind this property to item or user data.
    /// </summary>
    public string? DiagnosticScope { get; set; }

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
    /// Gets or sets the source displayed while <see cref="SourceKey"/> is being resolved or cannot produce an icon.
    /// A placement fallback takes precedence over a fallback supplied by the source provider.
    /// </summary>
    public IconSource? FallbackSource
    {
        get => (IconSource?)GetValue(FallbackSourceProperty);
        set => SetValue(FallbackSourceProperty, value);
    }

    public static readonly DependencyProperty FallbackSourceProperty =
        DependencyProperty.Register(nameof(FallbackSource), typeof(IconSource), typeof(IconBox), new PropertyMetadata(null, OnFallbackSourcePropertyChanged));

    /// <summary>
    /// Gets or sets a value indicating whether an image-oriented request that resolves to a
    /// font icon should use <see cref="FallbackSource"/> instead. This is intended for image
    /// placements, such as application hero images, where a glyph is not appropriate.
    /// </summary>
    public bool PreferFallbackSourceForFontIcons
    {
        get => (bool)GetValue(PreferFallbackSourceForFontIconsProperty);
        set => SetValue(PreferFallbackSourceForFontIconsProperty, value);
    }

    public static readonly DependencyProperty PreferFallbackSourceForFontIconsProperty =
        DependencyProperty.Register(nameof(PreferFallbackSourceForFontIcons), typeof(bool), typeof(IconBox), new PropertyMetadata(false, OnPreferFallbackSourceForFontIconsPropertyChanged));

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

    private TypedEventHandler<IconBox, SourceRequestedEventArgs>? _sourceRequested;

    /// <summary>
    /// Gets or sets the <see cref="SourceRequested"/> event handler to provide the value of the <see cref="IconSource"/> for the <see cref="Source"/> property from the provided <see cref="SourceKey"/>.
    /// </summary>
    public event TypedEventHandler<IconBox, SourceRequestedEventArgs>? SourceRequested
    {
        add
        {
            var hadHandler = _sourceRequested is not null;
            _sourceRequested += value;

            if (!hadHandler && _sourceRequested is not null)
            {
                RequestRefresh(IconRequestReason.HandlerAttached);
            }
#if DEBUG
            else if (value is not null)
            {
                Logger.LogWarning("There shouldn't be more than one handler for IconBox.SourceRequested");
            }
#endif
        }

        remove
        {
            _sourceRequested -= value;

            if (_sourceRequested is null)
            {
                AdvanceRequestVersion();
                MarkRefreshPending(IconRequestReason.None);
            }
        }
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
        RequestRefresh(IconRequestReason.ThemeChanged);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Handler attachment can request an icon before this control enters the visual tree.
        // Recompute any derived diagnostic placement now that its parent chain is available.
        _hasDerivedRequestSite = false;
        _derivedRequestSite = IconRequestSite.Unknown;
        var newTheme = ActualTheme;
        var newScale = XamlRoot?.RasterizationScale ?? 1.0;
        var changedTheme = _lastTheme != newTheme;
        var changedScale = Math.Abs(newScale - _lastScale) > 0.01;

        _lastTheme = newTheme;
        _lastScale = newScale;
        UpdateLastFontSize();

        if (XamlRoot is not null)
        {
            XamlRoot.Changed += OnXamlRootChanged;
        }

        if (SourceKey is not null && (changedTheme || changedScale))
        {
            var reason = IconRequestReason.Loaded;
            if (changedTheme)
            {
                reason |= IconRequestReason.ThemeChanged;
            }

            if (changedScale)
            {
                reason |= IconRequestReason.ScaleChanged;
            }

            MarkRefreshPending(reason);
        }

        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // ListView recycling can deliver an Unloaded event after the same template
        // instance has already been rebound, loaded, and arranged for a new item.
        // No matching Loaded event follows that stale notification, so invalidating
        // the new request here would leave the previous icon in the visible row.
        if (_activeRequestDemand is not null && IsLoaded && IsWithinXamlRootBounds())
        {
            return;
        }

        if (XamlRoot is not null)
        {
            XamlRoot.Changed -= OnXamlRootChanged;
        }

        if (_activeRequestDemand is not null)
        {
            AdvanceRequestVersion();
            MarkRefreshPending(IconRequestReason.Loaded);
        }

        _hasDerivedRequestSite = false;
        _derivedRequestSite = IconRequestSite.Unknown;
    }

    private bool IsWithinXamlRootBounds()
    {
        var xamlRoot = XamlRoot;
        if (xamlRoot is null || ActualWidth <= 0 || ActualHeight <= 0)
        {
            return false;
        }

        try
        {
            var bounds = TransformToVisual(null).TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));
            var rootSize = xamlRoot.Size;
            return bounds.Right > 0
                && bounds.Bottom > 0
                && bounds.Left < rootSize.Width
                && bounds.Top < rootSize.Height;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        var newScale = sender.RasterizationScale;
        var changedLastTheme = _lastTheme != ActualTheme;
        var changedScale = Math.Abs(newScale - _lastScale) > 0.01;

        _lastScale = newScale;
        _lastTheme = ActualTheme;

        if (SourceKey is not null && (changedLastTheme || changedScale))
        {
            var reason = IconRequestReason.None;
            if (changedLastTheme)
            {
                reason |= IconRequestReason.ThemeChanged;
            }

            if (changedScale)
            {
                reason |= IconRequestReason.ScaleChanged;
            }

            RequestRefresh(reason);
        }
    }

    private void MarkRefreshPending(IconRequestReason reason) =>
        _refreshState.Request(SourceKey is not null, reason);

    private void RequestRefresh(IconRequestReason reason)
    {
        MarkRefreshPending(reason);
        Refresh();
    }

    private void Refresh()
    {
        var sourceKey = SourceKey;
        var sourceRequested = _sourceRequested;
        if (!_refreshState.TryConsume(
                IsLoaded,
                sourceKey is not null,
                sourceRequested is not null,
                out var reason))
        {
            return;
        }

        RequestIconFromSource(this, sourceKey!, sourceRequested!, AdvanceRequestVersion(), reason);
    }

    private long AdvanceRequestVersion()
    {
        _activeRequestDiagnostics.Invalidate();
        _activeRequestDiagnostics = default;
        _activeRequestDemand?.Release();
        _activeRequestDemand = null;
        return ++_requestVersion;
    }

    private void TrackActiveRequest(
        long requestVersion,
        IconRequestMeasurement diagnostics,
        IIconRequestDemand demand)
    {
        if (requestVersion == _requestVersion)
        {
            _activeRequestDiagnostics = diagnostics;
            _activeRequestDemand = demand;
        }
        else
        {
            diagnostics.Invalidate();
            demand.Release();
        }
    }

    private void ClearActiveRequest(long requestVersion, IIconRequestDemand demand)
    {
        demand.Release();
        if (requestVersion == _requestVersion && ReferenceEquals(_activeRequestDemand, demand))
        {
            _activeRequestDiagnostics = default;
            _activeRequestDemand = null;
        }
    }

    private IconRequestOrigin GetDiagnosticOrigin()
    {
        var requestSite = RequestSite == IconRequestSite.Unknown ? GetDerivedRequestSite() : RequestSite;
        var diagnosticId = Volatile.Read(ref _diagnosticId);
        if (diagnosticId == 0)
        {
            var candidate = Interlocked.Increment(ref _nextDiagnosticId);
            var existing = Interlocked.CompareExchange(ref _diagnosticId, candidate, 0);
            diagnosticId = existing == 0 ? candidate : existing;
        }

        return new IconRequestOrigin(diagnosticId, requestSite, DiagnosticScope);
    }

    private IconRequestSite GetDerivedRequestSite()
    {
        if (_hasDerivedRequestSite)
        {
            return _derivedRequestSite;
        }

        for (DependencyObject? current = VisualTreeHelper.GetParent(this); current is not null; current = VisualTreeHelper.GetParent(current))
        {
            _derivedRequestSite = current switch
            {
                MenuFlyoutItemBase or MenuFlyoutPresenter => IconRequestSite.ContextMenu,
                ListViewItem or GridViewItem => IconRequestSite.ListItem,
                _ => IconRequestSite.Unknown,
            };

            if (_derivedRequestSite != IconRequestSite.Unknown)
            {
                break;
            }
        }

        // Unknown is only stable once the control is loaded. Before that, a missing parent merely
        // means the visual tree is not ready yet and should not poison later attribution.
        _hasDerivedRequestSite = _derivedRequestSite != IconRequestSite.Unknown || IsLoaded;
        return _derivedRequestSite;
    }

    private string GetDiagnosticDescription()
    {
        var origin = GetDiagnosticOrigin();
        return string.IsNullOrEmpty(origin.DiagnosticScope)
            ? $"IconBox #{origin.IconBoxId}, site {origin.RequestSite}"
            : $"IconBox #{origin.IconBoxId}, site {origin.RequestSite}/{origin.DiagnosticScope}";
    }

    private static void OnSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        switch (e.NewValue)
        {
            case null:
                var imagePresenterWasActive = ReferenceEquals(self.Content, self._imagePresenter);
                self.ClearImagePresenter();
                if (!imagePresenterWasActive)
                {
                    self.Content = null;
                }

                self.Padding = default;
                break;
            case FontIconSource fontIcon:
                var fontElementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
                self.ClearImagePresenter();
                self.UpdateLastFontSize();
                fontIcon.FontSize = self._lastFontSize;
                if (self.Content is IconSourceElement iconSourceElement)
                {
                    iconSourceElement.IconSource = fontIcon;
                    IconLoadDiagnostics.RecordElementUpdate(reused: true, fontIcon, fontElementStartedAt);
                }
                else
                {
                    self.Content = fontIcon.CreateIconElement();
                    IconLoadDiagnostics.RecordElementUpdate(reused: false, fontIcon, fontElementStartedAt);
                }

                self.UpdatePaddingForFontIcon();

                break;
            case BitmapIconSource bitmapIcon:
                var bitmapElementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
                self.ClearImagePresenter();
                if (self.Content is IconSourceElement iconSourceElement2)
                {
                    iconSourceElement2.IconSource = bitmapIcon;
                    IconLoadDiagnostics.RecordElementUpdate(reused: true, bitmapIcon, bitmapElementStartedAt);
                }
                else
                {
                    self.Content = bitmapIcon.CreateIconElement();
                    IconLoadDiagnostics.RecordElementUpdate(reused: false, bitmapIcon, bitmapElementStartedAt);
                }

                self.Padding = default;

                break;

            case ImageIconSource imageIcon:
                var imageElementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
                var reusedImagePresenter = self.PresentImageIconSource(imageIcon);
                IconLoadDiagnostics.RecordElementUpdate(reusedImagePresenter, imageIcon, imageElementStartedAt);
                self.Padding = default;
                break;

            case IconSource source:
                var sourceElementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
                self.ClearImagePresenter();
                self.Content = source.CreateIconElement();
                IconLoadDiagnostics.RecordElementUpdate(reused: false, source, sourceElementStartedAt);
                self.Padding = default;
                break;

            default:
                throw new InvalidOperationException($"New value of {e.NewValue} is not of type IconSource.");
        }
    }

    private bool PresentImageIconSource(ImageIconSource source)
    {
        if (_imagePresenterDisabled)
        {
            Content = source.CreateIconElement();
            return false;
        }

        if (source.ImageSource is null)
        {
            var imagePresenterWasActive = ReferenceEquals(Content, _imagePresenter);
            ClearImagePresenter();
            if (!imagePresenterWasActive)
            {
                Content = null;
            }

            return _imagePresenter is not null;
        }

        var reused = _imagePresenter is not null;

        try
        {
            var presenter = EnsureImagePresenter();
            var nextSlot = ReferenceEquals(_activeImageSlot, _firstImageSlot)
                ? EnsureSecondImageSlot()
                : _firstImageSlot!;
            var previousSlot = _activeImageSlot;

            if (previousSlot is not null)
            {
                previousSlot.Visibility = Visibility.Collapsed;
            }

            nextSlot.Source = source.ImageSource;
            nextSlot.Visibility = Visibility.Visible;

            if (previousSlot is not null)
            {
                previousSlot.Source = null;
            }

            _activeImageSlot = nextSlot;
            if (!ReferenceEquals(Content, presenter))
            {
                Content = presenter;
            }

            return reused;
        }
        catch (Exception ex)
        {
            DisableImagePresenter();
            Logger.LogError($"Failed to update reusable image presenter ({GetDiagnosticDescription()})", ex);
            Content = source.CreateIconElement();
            return false;
        }
    }

    private Grid EnsureImagePresenter()
    {
        if (_imagePresenter is not null)
        {
            return _imagePresenter;
        }

        var presenter = new Grid
        {
            IsHitTestVisible = false,
        };
        var firstSlot = CreateImageSlot();
        presenter.Children.Add(firstSlot);

        _imagePresenter = presenter;
        _firstImageSlot = firstSlot;
        return presenter;
    }

    private Image EnsureSecondImageSlot()
    {
        if (_secondImageSlot is not null)
        {
            return _secondImageSlot;
        }

        var secondSlot = CreateImageSlot();
        _imagePresenter!.Children.Add(secondSlot);
        _secondImageSlot = secondSlot;
        return secondSlot;
    }

    private static Image CreateImageSlot()
    {
        return new Image
        {
            IsHitTestVisible = false,
            Stretch = Stretch.Uniform,
            Visibility = Visibility.Collapsed,
        };
    }

    private void ClearImagePresenter()
    {
        _activeImageSlot?.Visibility = Visibility.Collapsed;
        _firstImageSlot?.Source = null;
        _secondImageSlot?.Source = null;
        _activeImageSlot = null;
    }

    private void DisableImagePresenter()
    {
        try
        {
            ClearImagePresenter();
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to clear reusable image presenter ({GetDiagnosticDescription()})", ex);
        }

        _imagePresenter = null;
        _firstImageSlot = null;
        _secondImageSlot = null;
        _activeImageSlot = null;
        _imagePresenterDisabled = true;
    }

    private static void OnFallbackSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        self._presentation.PlacementFallback = e.NewValue as IconSource;
        if (self.SourceKey is not null)
        {
            self.UpdatePresentedSource();
        }
    }

    private static void OnPreferFallbackSourceForFontIconsPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is IconBox self && self.SourceKey is not null)
        {
            self.UpdatePresentedSource();
        }
    }

    private static void OnSourceKeyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not IconBox self)
        {
            return;
        }

        self.AdvanceRequestVersion();
        self._presentation.BeginSourceChange();

        if (e.NewValue is null)
        {
            self._refreshState.Clear();
            self.Source = null;
            return;
        }

        // Keep the preceding source only while a replacement has a chance to resolve
        // synchronously in Refresh. RequestIconFromSource presents the fallback before
        // an asynchronous suspension, so the preceding item can never reach another frame.
        // If no request can start now, clear it immediately instead.
        if (!self.IsLoaded || self._sourceRequested is null)
        {
            self.UpdatePresentedSource();
        }

        self.RequestRefresh(IconRequestReason.SourceChanged);
    }

    private static async void RequestIconFromSource(
        IconBox iconBox,
        object sourceKey,
        TypedEventHandler<IconBox, SourceRequestedEventArgs> sourceRequested,
        long requestVersion,
        IconRequestReason reason)
    {
        var diagnostics = default(IconRequestMeasurement);
        SourceRequestedEventArgs? eventArgs = null;

        try
        {
            var scale = iconBox._lastScale > 0
                ? iconBox._lastScale
                : (iconBox.XamlRoot?.RasterizationScale > 0 ? iconBox.XamlRoot.RasterizationScale : 1.0);

            diagnostics = IconLoadDiagnostics.IsRecording
                ? IconLoadDiagnostics.BeginRequest(reason, scale, iconBox.GetDiagnosticOrigin())
                : default;
            eventArgs = new SourceRequestedEventArgs(sourceKey, iconBox._lastTheme, scale)
            {
                Diagnostics = diagnostics,
            };
            iconBox.TrackActiveRequest(requestVersion, diagnostics, eventArgs);
            var invocation = sourceRequested.InvokeAsync(iconBox, eventArgs);
            if (!invocation.IsCompleted)
            {
                iconBox.SetRequestFallback(requestVersion, sourceKey, eventArgs.FallbackSource);
            }

            await invocation;

            // After the await:
            // Is the icon we're looking up now, the one we still
            // want to find? Since this IconBox might be used in a
            // list virtualization situation, it's very possible we
            // may have already been set to a new icon before we
            // even got back from the await.
            if (requestVersion != iconBox._requestVersion || !ReferenceEquals(sourceKey, iconBox.SourceKey))
            {
                // If the requested icon has changed, then just bail
                diagnostics.Complete(IconRequestStatus.Stale, eventArgs.Value);
                return;
            }

            iconBox._presentation.SetRequestFallback(eventArgs.FallbackSource);
            iconBox._presentation.SetResolvedSource(eventArgs.Value, eventArgs.ExpectsImageSource);
            iconBox.UpdatePresentedSource();
            diagnostics.Complete(
                eventArgs.Value is null ? IconRequestStatus.Empty : IconRequestStatus.Applied,
                eventArgs.Value);
        }
        catch (Exception ex)
        {
            diagnostics.Complete(IconRequestStatus.Failed);

            if (requestVersion == iconBox._requestVersion)
            {
                // A synchronous provider failure occurs before the pending-source path
                // gets a chance to clear the preceding item.
                try
                {
                    iconBox.UpdatePresentedSource();
                }
                catch (Exception presentationException)
                {
                    Logger.LogError($"Failed to clear icon after a request failure ({iconBox.GetDiagnosticDescription()})", presentationException);
                }

                // Do not dispatch immediately: a deterministic failure would recurse forever.
                // Keep the request pending for the next external lifecycle or source trigger.
                iconBox.MarkRefreshPending(IconRequestReason.Retry);
            }

            // Exception from TryEnqueue bypasses the global error handler,
            // and crashes the app.
            Logger.LogError($"Failed to set icon ({iconBox.GetDiagnosticDescription()})", ex);
        }
        finally
        {
            if (eventArgs is not null)
            {
                iconBox.ClearActiveRequest(requestVersion, eventArgs);
            }
        }
    }

    private void SetRequestFallback(long requestVersion, object sourceKey, IconSource? fallbackSource)
    {
        if (requestVersion != _requestVersion || !ReferenceEquals(sourceKey, SourceKey))
        {
            return;
        }

        _presentation.SetRequestFallback(fallbackSource);
        UpdatePresentedSource();
    }

    private void UpdatePresentedSource()
    {
        var resolvedSource = _presentation.ResolvedSource;

        // Replacing a valid glyph requires both opt-ins: the placement must prefer
        // an image fallback, and the provider must identify this as an image request.
        // This keeps app hero images image-only without replacing emoji or other glyph heroes.
        var preferFallback = resolvedSource is null
            || (PreferFallbackSourceForFontIcons && _presentation.ResolvedSourceExpectsImage && resolvedSource is FontIconSource)
            || resolvedSource is BitmapIconSource { UriSource: null }
            || resolvedSource is ImageIconSource { ImageSource: null };
        Source = _presentation.SelectSource(preferFallback);
    }
}
