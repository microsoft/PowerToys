// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.WinUI.Deferred;
using ManagedCommon;
using Microsoft.CmdPal.UI.Deferred;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.Terminal.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is not null)
        {
            XamlRoot.Changed -= OnXamlRootChanged;
        }
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        var newScale = sender.RasterizationScale;
        var changedLastTheme = _lastTheme != ActualTheme;
        _lastTheme = ActualTheme;
        if ((changedLastTheme || Math.Abs(newScale - _lastScale) > 0.01) && SourceKey is not null)
        {
            _lastScale = newScale;
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

        switch (e.NewValue)
        {
            case null:
                self.Content = null;
                self.Padding = default;
                break;
            case FontIconSource fontIcon:
                self.UpdateLastFontSize();
                if (self.Content is IconSourceElement iconSourceElement)
                {
                    fontIcon.FontSize = self._lastFontSize;
                    iconSourceElement.IconSource = fontIcon;
                }
                else
                {
                    fontIcon.FontSize = self._lastFontSize;

                    // For inexplicable reasons, FontIconSource.CreateIconElement
                    // doesn't work, so do it ourselves
                    // TODO: File platform bug?
                    IconSourceElement elem = new()
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IconSource = fontIcon,
                    };
                    self.Content = elem;
                }

                self.UpdatePaddingForFontIcon();

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

        UpdateSourceKey(self, e.NewValue);
    }

    private static void UpdateSourceKey(IconBox iconBox, object? sourceKey)
    {
        if (sourceKey is null)
        {
            iconBox.Source = null;
            return;
        }

        RequestIconFromSource(iconBox, sourceKey);
    }

    private static async void RequestIconFromSource(IconBox iconBox, object? sourceKey)
    {
        try
        {
            var iconBoxSourceRequestedHandler = iconBox._sourceRequested;

            if (iconBoxSourceRequestedHandler is null)
            {
                return;
            }

            var eventArgs = new SourceRequestedEventArgs(sourceKey, iconBox._lastTheme, iconBox._lastScale);
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
                return;
            }

            iconBox.Source = eventArgs.Value;
        }
        catch (Exception ex)
        {
            // Exception from TryEnqueue bypasses the global error handler,
            // and crashes the app.
            Logger.LogError("Failed to set icon", ex);
        }
    }
}
