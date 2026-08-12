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

    private double _lastScale;
    private ElementTheme _lastTheme;
    private double _lastFontSize;
    private long _requestVersion;
    private IconRequestMeasurement _activeRequestDiagnostics;
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
            _sourceRequested += value;
            if (_sourceRequested?.GetInvocationList().Length == 1)
            {
                Refresh(IconRequestReason.HandlerAttached);
            }
#if DEBUG
            if (_sourceRequested?.GetInvocationList().Length > 1)
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
        Refresh(IconRequestReason.ThemeChanged);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Handler attachment can request an icon before this control enters the visual tree.
        // Recompute any derived diagnostic placement now that its parent chain is available.
        _hasDerivedRequestSite = false;
        _derivedRequestSite = IconRequestSite.Unknown;
        _lastTheme = ActualTheme;
        UpdateLastFontSize();

        if (XamlRoot is not null)
        {
            _lastScale = XamlRoot.RasterizationScale;
            XamlRoot.Changed += OnXamlRootChanged;
        }

        Refresh(IconRequestReason.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            XamlRoot.Changed -= OnXamlRootChanged;
        }

        _hasDerivedRequestSite = false;
        _derivedRequestSite = IconRequestSite.Unknown;
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
            var reason = IconRequestReason.None;
            if (changedLastTheme)
            {
                reason |= IconRequestReason.ThemeChanged;
            }

            if (changedScale)
            {
                reason |= IconRequestReason.ScaleChanged;
            }

            UpdateSourceKey(this, SourceKey, reason);
        }
    }

    private void Refresh(IconRequestReason reason = IconRequestReason.None)
    {
        UpdateSourceKey(this, SourceKey, reason);
    }

    private long AdvanceRequestVersion()
    {
        _activeRequestDiagnostics.Invalidate();
        _activeRequestDiagnostics = default;
        return ++_requestVersion;
    }

    private void TrackActiveRequest(long requestVersion, IconRequestMeasurement diagnostics)
    {
        if (requestVersion == _requestVersion)
        {
            _activeRequestDiagnostics = diagnostics;
        }
        else
        {
            diagnostics.Invalidate();
        }
    }

    private void ClearActiveRequest(long requestVersion)
    {
        if (requestVersion == _requestVersion)
        {
            _activeRequestDiagnostics = default;
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
                self.Content = null;
                self.Padding = default;
                break;
            case FontIconSource fontIcon:
                var fontElementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
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

            case IconSource source:
                var sourceElementStartedAt = IconLoadDiagnostics.BeginElementUpdate();
                self.Content = source.CreateIconElement();
                IconLoadDiagnostics.RecordElementUpdate(reused: false, source, sourceElementStartedAt);
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

        UpdateSourceKey(self, e.NewValue, IconRequestReason.SourceChanged);
    }

    private static void UpdateSourceKey(
        IconBox iconBox,
        object? sourceKey,
        IconRequestReason reason = IconRequestReason.None)
    {
        var requestVersion = iconBox.AdvanceRequestVersion();

        if (sourceKey is null)
        {
            iconBox.Source = null;
            return;
        }

        RequestIconFromSource(iconBox, sourceKey, requestVersion, reason);
    }

    private static async void RequestIconFromSource(
        IconBox iconBox,
        object sourceKey,
        long requestVersion,
        IconRequestReason reason)
    {
        var diagnostics = default(IconRequestMeasurement);

        try
        {
            var iconBoxSourceRequestedHandler = iconBox._sourceRequested;

            if (iconBoxSourceRequestedHandler is null)
            {
                return;
            }

            var scale = iconBox._lastScale > 0
                ? iconBox._lastScale
                : (iconBox.XamlRoot?.RasterizationScale > 0 ? iconBox.XamlRoot.RasterizationScale : 1.0);

            diagnostics = IconLoadDiagnostics.IsRecording
                ? IconLoadDiagnostics.BeginRequest(reason, scale, iconBox.GetDiagnosticOrigin())
                : default;
            iconBox.TrackActiveRequest(requestVersion, diagnostics);
            var eventArgs = new SourceRequestedEventArgs(sourceKey, iconBox._lastTheme, scale)
            {
                Diagnostics = diagnostics,
            };
            await iconBoxSourceRequestedHandler.InvokeAsync(iconBox, eventArgs);

            // After the await:
            // Is the icon we're looking up now, the one we still
            // want to find? Since this IconBox might be used in a
            // list virtualization situation, it's very possible we
            // may have already been set to a new icon before we
            // even got back from the await.
            if (!ReferenceEquals(sourceKey, iconBox.SourceKey))
            {
                // If the requested icon has changed, then just bail
                diagnostics.Complete(IconRequestStatus.Stale, eventArgs.Value);
                return;
            }

            iconBox.Source = eventArgs.Value;
            diagnostics.Complete(
                eventArgs.Value is null ? IconRequestStatus.Empty : IconRequestStatus.Applied,
                eventArgs.Value);
        }
        catch (Exception ex)
        {
            diagnostics.Complete(IconRequestStatus.Failed);

            // Exception from TryEnqueue bypasses the global error handler,
            // and crashes the app.
            Logger.LogError($"Failed to set icon ({iconBox.GetDiagnosticDescription()})", ex);
        }
        finally
        {
            iconBox.ClearActiveRequest(requestVersion);
        }
    }
}
