// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;

namespace Microsoft.CmdPal.UI.Dock;

[ContentProperty(Name = nameof(Icon))]
public sealed partial class DockItemControl : Control
{
    public DockItemControl()
    {
        DefaultStyleKey = typeof(DockItemControl);
    }

    public static readonly DependencyProperty ToolTipProperty =
        DependencyProperty.Register(nameof(ToolTip), typeof(string), typeof(DockItemControl), new PropertyMetadata(null));

    public string ToolTip
    {
        get => (string)GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DockItemControl), new PropertyMetadata(null, OnTextPropertyChanged));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(DockItemControl), new PropertyMetadata(null, OnTextPropertyChanged));

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(object), typeof(DockItemControl), new PropertyMetadata(null, OnIconPropertyChanged));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    private const string IconPresenterName = "IconPresenter";
    private const string TitleTextName = "TitleText";
    private const string SubtitleTextName = "SubtitleText";

    private FrameworkElement? _iconPresenter;
    private FrameworkElement? _titleText;
    private FrameworkElement? _subtitleText;

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateTextVisibility();
        }
    }

    private static void OnIconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateIconVisibility();
        }
    }

    private static bool IsNullOrEmpty(string? value) => string.IsNullOrEmpty(value);

    private void UpdateTextVisibility()
    {
        if (_titleText is not null)
        {
            _titleText.Visibility = IsNullOrEmpty(Title) ? Visibility.Collapsed : Visibility.Visible;
        }

        if (_subtitleText is not null)
        {
            _subtitleText.Visibility = IsNullOrEmpty(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        }

        UpdateTextVisibilityState();
    }

    private void UpdateTextVisibilityState()
    {
        var hasText = !IsNullOrEmpty(Title) || !IsNullOrEmpty(Subtitle);
        VisualStateManager.GoToState(this, hasText ? "TextVisible" : "TextHidden", true);
    }

    private void UpdateIconVisibility()
    {
        if (_iconPresenter is not null)
        {
            _iconPresenter.Visibility = Icon is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private void UpdateAllVisibility()
    {
        UpdateTextVisibility();
        UpdateIconVisibility();
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        IsEnabledChanged -= OnIsEnabledChanged;

        PointerEntered += Control_PointerEntered;
        PointerExited += Control_PointerExited;

        IsEnabledChanged += OnIsEnabledChanged;

        // Get template children for visibility updates
        _iconPresenter = GetTemplateChild(IconPresenterName) as FrameworkElement;
        _titleText = GetTemplateChild(TitleTextName) as FrameworkElement;
        _subtitleText = GetTemplateChild(SubtitleTextName) as FrameworkElement;

        // Set initial visibility
        UpdateAllVisibility();
    }

    private void Control_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void Control_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        if (IsEnabled)
        {
            base.OnPointerPressed(e);
            VisualStateManager.GoToState(this, "Pressed", true);
        }
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
    }
}
