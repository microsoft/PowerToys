// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Dock;

[ContentProperty(Name = nameof(Icon))]
public sealed partial class DockItemControl : Control
{
    private const string IconPresenterName = "IconPresenter";
    private const string FrontContentName = "FrontContent";
    private const string BackContentName = "BackContent";

    private const int FlipHalfDurationMs = 150;

    private FrameworkElement? _iconPresenter;
    private FrameworkElement? _frontContentElement;
    private FrameworkElement? _backContentElement;
    private DockControl? _parentDock;
    private ToolTip? _toolTip;
    private long _dockSideCallbackToken = -1;

    // Live tile flip animation state
    private Visual? _frontVisual;
    private Visual? _backVisual;
    private Compositor? _compositor;
    private CubicBezierEasingFunction? _easing;
    private DispatcherTimer? _flipTimer;
    private bool _showingBack;
    private bool _isFlipping;

    public DockItemControl()
    {
        DefaultStyleKey = typeof(DockItemControl);
    }

    public static readonly DependencyProperty ToolTipProperty =
        DependencyProperty.Register(nameof(ToolTip), typeof(string), typeof(DockItemControl), new PropertyMetadata(null, OnToolTipPropertyChanged));

    public string ToolTip
    {
        get => (string)GetValue(ToolTipProperty);
        set => SetValue(ToolTipProperty, value);
    }

    private static void OnToolTipPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateToolTip();
        }
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

    public static readonly DependencyProperty InnerMarginProperty =
        DependencyProperty.Register(nameof(InnerMargin), typeof(Thickness), typeof(DockItemControl), new PropertyMetadata(new Thickness(0)));

    public Thickness InnerMargin
    {
        get => (Thickness)GetValue(InnerMarginProperty);
        set => SetValue(InnerMarginProperty, value);
    }

    public static readonly DependencyProperty TextVisibilityProperty =
        DependencyProperty.Register(nameof(TextVisibility), typeof(Visibility), typeof(DockItemControl), new PropertyMetadata(null, OnTextPropertyChanged));

    public Visibility TextVisibility
    {
        get => (Visibility)GetValue(TextVisibilityProperty);
        set => SetValue(TextVisibilityProperty, value);
    }

    public static readonly DependencyProperty LiveTileTitleProperty =
        DependencyProperty.Register(nameof(LiveTileTitle), typeof(string), typeof(DockItemControl), new PropertyMetadata(null));

    public string LiveTileTitle
    {
        get => (string)GetValue(LiveTileTitleProperty);
        set => SetValue(LiveTileTitleProperty, value);
    }

    public static readonly DependencyProperty LiveTileSubtitleProperty =
        DependencyProperty.Register(nameof(LiveTileSubtitle), typeof(string), typeof(DockItemControl), new PropertyMetadata(null));

    public string LiveTileSubtitle
    {
        get => (string)GetValue(LiveTileSubtitleProperty);
        set => SetValue(LiveTileSubtitleProperty, value);
    }

    public static readonly DependencyProperty LiveTileIconProperty =
        DependencyProperty.Register(nameof(LiveTileIcon), typeof(object), typeof(DockItemControl), new PropertyMetadata(null));

    public object LiveTileIcon
    {
        get => GetValue(LiveTileIconProperty);
        set => SetValue(LiveTileIconProperty, value);
    }

    public static readonly DependencyProperty FlipIntervalMsProperty =
        DependencyProperty.Register(nameof(FlipIntervalMs), typeof(int), typeof(DockItemControl), new PropertyMetadata(0, OnFlipIntervalMsChanged));

    public int FlipIntervalMs
    {
        get => (int)GetValue(FlipIntervalMsProperty);
        set => SetValue(FlipIntervalMsProperty, value);
    }

    private static void OnFlipIntervalMsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateFlipTimer();
        }
    }

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateTextVisibility();
            control.UpdateAlignment();
        }
    }

    private static void OnIconPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateIconVisibility();
            control.UpdateAlignment();
        }
    }

    internal bool HasTitle => !string.IsNullOrEmpty(Title);

    internal bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);

    internal bool HasText => HasTitle || HasSubtitle;

    private void UpdateTextVisibility()
    {
        UpdateTextVisibilityState();
    }

    private void UpdateTextVisibilityState()
    {
        var stateName = (HasTitle, HasSubtitle) switch
        {
            (true, true) => "TextVisible",
            (true, false) => "TitleOnly",
            (false, true) => "SubtitleOnly",
            (false, false) => "TextHidden",
        };

        VisualStateManager.GoToState(this, stateName, true);
    }

    private void UpdateIconVisibility()
    {
        var shouldShowIcon = ShouldShowIcon();
        if (_iconPresenter is not null)
        {
            _iconPresenter.Visibility = shouldShowIcon ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateIconVisibilityState();
    }

    private void UpdateIconVisibilityState()
    {
        VisualStateManager.GoToState(this, ShouldShowIcon() ? "IconVisible" : "IconHidden", true);
    }

    private void UpdateAlignment()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        UpdateTextAlignmentState();
    }

    private bool ShouldShowIcon()
    {
        if (Icon is IconBox icoBox)
        {
            if (icoBox.SourceKey is IconInfoViewModel icon)
            {
                return icon.HasIcon(ActualTheme == ElementTheme.Light);
            }

            return icoBox.Source is not null;
        }

        return Icon is not null;
    }

    private void UpdateTextAlignmentState()
    {
        var verticalDock = _parentDock?.DockSide is DockSide.Left or DockSide.Right;
        var shouldCenterText = verticalDock && !ShouldShowIcon();
        VisualStateManager.GoToState(this, shouldCenterText ? "TextCentered" : "TextLeftAligned", true);
    }

    private void UpdateAllVisibility()
    {
        UpdateTextVisibility();
        UpdateIconVisibility();
        UpdateToolTip();
        UpdateAlignment();
    }

    private void UpdateToolTip()
    {
        var text = ToolTip;
        if (string.IsNullOrEmpty(text))
        {
            ToolTipService.SetToolTip(this, null);
            _toolTip = null;
            return;
        }

        if (XamlRoot is null)
        {
            return;
        }

        _toolTip ??= new ToolTip();
        _toolTip.Content = text;
        _toolTip.XamlRoot = XamlRoot;
        ToolTipService.SetToolTip(this, _toolTip);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        IsEnabledChanged -= OnIsEnabledChanged;
        ActualThemeChanged -= DockItemControl_ActualThemeChanged;

        PointerEntered -= Control_PointerEntered;
        PointerExited -= Control_PointerExited;
        Loaded -= DockItemControl_Loaded;
        Unloaded -= DockItemControl_Unloaded;

        ActualThemeChanged += DockItemControl_ActualThemeChanged;
        PointerEntered += Control_PointerEntered;
        PointerExited += Control_PointerExited;
        Loaded += DockItemControl_Loaded;
        Unloaded += DockItemControl_Unloaded;

        IsEnabledChanged += OnIsEnabledChanged;

        _iconPresenter = GetTemplateChild(IconPresenterName) as FrameworkElement;
        _frontContentElement = GetTemplateChild(FrontContentName) as FrameworkElement;
        _backContentElement = GetTemplateChild(BackContentName) as FrameworkElement;

        UpdateAllVisibility();
    }

    private void DockItemControl_Loaded(object sender, RoutedEventArgs e)
    {
        DependencyObject? parent = VisualTreeHelper.GetParent(this);
        while (parent is not null and not DockControl)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        if (parent is DockControl dock)
        {
            _parentDock = dock;
            UpdateInnerMarginForDockSide(dock.DockSide);
            UpdateAllVisibility();
            _dockSideCallbackToken = dock.RegisterPropertyChangedCallback(
                DockControl.DockSideProperty,
                OnParentDockSideChanged);
        }

        UpdateToolTip();
        InitializeFlipAnimation();
    }

    private void DockItemControl_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateIconVisibility();
        UpdateAlignment();
    }

    private void DockItemControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_parentDock is not null && _dockSideCallbackToken >= 0)
        {
            _parentDock.UnregisterPropertyChangedCallback(
                DockControl.DockSideProperty,
                _dockSideCallbackToken);
            _dockSideCallbackToken = -1;
            _parentDock = null;
        }

        ToolTipService.SetToolTip(this, null);
        _toolTip = null;

        StopFlipAnimation();
    }

    private void OnParentDockSideChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is DockControl dock)
        {
            UpdateInnerMarginForDockSide(dock.DockSide);
            UpdateAlignment();
        }
    }

    private void UpdateInnerMarginForDockSide(DockSide side)
    {
        InnerMargin = side switch
        {
            DockSide.Top => new Thickness(0, 4, 0, 0),
            DockSide.Bottom => new Thickness(0, 0, 0, 4),
            DockSide.Left => new Thickness(8, 0, 0, 0),
            DockSide.Right => new Thickness(0, 0, 8, 0),
            _ => new Thickness(0),
        };
    }

    private void Control_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
        _flipTimer?.Stop();
    }

    private void Control_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
        if (FlipIntervalMs > 0 && _flipTimer is not null)
        {
            _flipTimer.Start();
        }
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

    // Live Tile Flip Animation
    private void InitializeFlipAnimation()
    {
        if (_frontContentElement is null || _backContentElement is null)
        {
            return;
        }

        _frontVisual = ElementCompositionPreview.GetElementVisual(_frontContentElement);
        _backVisual = ElementCompositionPreview.GetElementVisual(_backContentElement);
        _compositor = _frontVisual.Compositor;

        // Back starts hidden
        _backVisual.Opacity = 0f;

        // Fast-in / slow-out easing matching Windows shell motion language
        _easing = _compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.8f, 0f),
            new Vector2(0.2f, 1f));

        UpdateFlipTimer();
    }

    private void UpdateFlipTimer()
    {
        var interval = FlipIntervalMs;
        if (interval <= 0)
        {
            _flipTimer?.Stop();
            _flipTimer = null;

            // Reset to front if we were showing back
            if (_showingBack)
            {
                ResetToFront();
            }

            return;
        }

        if (_compositor is null)
        {
            return;
        }

        if (_flipTimer is null)
        {
            _flipTimer = new DispatcherTimer();
            _flipTimer.Tick += OnFlipTimerTick;
        }

        _flipTimer.Interval = TimeSpan.FromMilliseconds(interval);
        _flipTimer.Start();
    }

    private void OnFlipTimerTick(object? sender, object e)
    {
        if (_isFlipping || _compositor is null || _frontVisual is null || _backVisual is null)
        {
            return;
        }

        _isFlipping = true;

        if (_showingBack)
        {
            AnimateFlip(_backVisual, _frontVisual);
        }
        else
        {
            AnimateFlip(_frontVisual, _backVisual);
        }

        _showingBack = !_showingBack;
    }

    private void AnimateFlip(Visual outVisual, Visual inVisual)
    {
        if (_compositor is null || _easing is null)
        {
            return;
        }

        UpdateVisualCenterPoints();

        var isVerticalDock = _parentDock?.DockSide is DockSide.Left or DockSide.Right;

        // Phase 1: Squash the outgoing face
        var scaleOut = _compositor.CreateVector3KeyFrameAnimation();
        if (isVerticalDock)
        {
            scaleOut.InsertKeyFrame(0.0f, new Vector3(1f, 1f, 1f));
            scaleOut.InsertKeyFrame(0.4f, new Vector3(0.92f, 1f, 1f), _easing);
            scaleOut.InsertKeyFrame(1.0f, new Vector3(0f, 1f, 1f), _easing);
        }
        else
        {
            scaleOut.InsertKeyFrame(0.0f, new Vector3(1f, 1f, 1f));
            scaleOut.InsertKeyFrame(0.4f, new Vector3(1f, 0.92f, 1f), _easing);
            scaleOut.InsertKeyFrame(1.0f, new Vector3(1f, 0f, 1f), _easing);
        }

        scaleOut.Duration = TimeSpan.FromMilliseconds(FlipHalfDurationMs);

        var fadeOut = _compositor.CreateScalarKeyFrameAnimation();
        fadeOut.InsertKeyFrame(0.0f, 1f);
        fadeOut.InsertKeyFrame(0.6f, 1f);
        fadeOut.InsertKeyFrame(1.0f, 0f);
        fadeOut.Duration = TimeSpan.FromMilliseconds(FlipHalfDurationMs);

        // Phase 2: Expand the incoming face
        var scaleIn = _compositor.CreateVector3KeyFrameAnimation();
        if (isVerticalDock)
        {
            scaleIn.InsertKeyFrame(0.0f, new Vector3(0f, 1f, 1f));
            scaleIn.InsertKeyFrame(0.6f, new Vector3(0.92f, 1f, 1f), _easing);
            scaleIn.InsertKeyFrame(1.0f, new Vector3(1f, 1f, 1f), _easing);
        }
        else
        {
            scaleIn.InsertKeyFrame(0.0f, new Vector3(1f, 0f, 1f));
            scaleIn.InsertKeyFrame(0.6f, new Vector3(1f, 0.92f, 1f), _easing);
            scaleIn.InsertKeyFrame(1.0f, new Vector3(1f, 1f, 1f), _easing);
        }

        scaleIn.Duration = TimeSpan.FromMilliseconds(FlipHalfDurationMs);
        scaleIn.DelayTime = TimeSpan.FromMilliseconds(FlipHalfDurationMs);

        var fadeIn = _compositor.CreateScalarKeyFrameAnimation();
        fadeIn.InsertKeyFrame(0.0f, 0f);
        fadeIn.InsertKeyFrame(0.4f, 1f);
        fadeIn.InsertKeyFrame(1.0f, 1f);
        fadeIn.Duration = TimeSpan.FromMilliseconds(FlipHalfDurationMs);
        fadeIn.DelayTime = TimeSpan.FromMilliseconds(FlipHalfDurationMs);

        // Create a scoped batch to know when the full animation completes
        var batch = _compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

        outVisual.StartAnimation("Scale", scaleOut);
        outVisual.StartAnimation("Opacity", fadeOut);
        inVisual.StartAnimation("Scale", scaleIn);
        inVisual.StartAnimation("Opacity", fadeIn);

        batch.End();
        batch.Completed += (_, _) => _isFlipping = false;
    }

    private void UpdateVisualCenterPoints()
    {
        if (_frontContentElement is null || _backContentElement is null ||
            _frontVisual is null || _backVisual is null)
        {
            return;
        }

        var center = new Vector3(
            (float)_frontContentElement.ActualWidth / 2f,
            (float)_frontContentElement.ActualHeight / 2f,
            0);
        _frontVisual.CenterPoint = center;

        var backCenter = new Vector3(
            (float)_backContentElement.ActualWidth / 2f,
            (float)_backContentElement.ActualHeight / 2f,
            0);
        _backVisual.CenterPoint = backCenter;
    }

    private void ResetToFront()
    {
        if (_frontVisual is not null)
        {
            _frontVisual.Scale = new Vector3(1f, 1f, 1f);
            _frontVisual.Opacity = 1f;
        }

        if (_backVisual is not null)
        {
            _backVisual.Scale = new Vector3(1f, 1f, 1f);
            _backVisual.Opacity = 0f;
        }

        _showingBack = false;
        _isFlipping = false;
    }

    private void StopFlipAnimation()
    {
        _flipTimer?.Stop();
        _flipTimer = null;

        _frontVisual?.StopAnimation("Scale");
        _frontVisual?.StopAnimation("Opacity");
        _backVisual?.StopAnimation("Scale");
        _backVisual?.StopAnimation("Opacity");

        _frontVisual = null;
        _backVisual = null;
        _compositor = null;
        _easing = null;
        _isFlipping = false;
    }
}
