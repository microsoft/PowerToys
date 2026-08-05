// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Dock;

[ContentProperty(Name = nameof(Icon))]
public sealed partial class DockItemControl : Control
{
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

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(DockItemControl), new PropertyMetadata(false, OnIsCompactPropertyChanged));

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    private static void OnIsCompactPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockItemControl control)
        {
            control.UpdateCompactState();
        }
    }

    private void UpdateCompactState()
    {
        VisualStateManager.GoToState(this, IsCompact ? "Compact" : "DefaultLayout", true);
        UpdateSubtitleVisibilityState();
        UpdateInnerMargin();
    }

    private const string IconPresenterName = "IconPresenter";
    private const string BackPlateName = "PART_BackPlate";

    // Gap between the item's bounds and its chrome on the sides that don't touch a
    // dock edge. Applied as padding, so the gap still takes clicks.
    private static readonly Thickness ChromeGap = new(2, 0, 2, 0);

    // On the screen-edge side DockControl drops its own inset so items can reach the
    // edge; the item puts that much back inside its own bounds. These mirror the
    // margin (+ padding, for vertical docks) DockControl keeps on the opposite side.
    private const double HorizontalDockEdgeGap = 2;
    private const double VerticalDockEdgeGap = 4;

    private FrameworkElement? _iconPresenter;
    private FrameworkElement? _backPlate;
    private double _backPlateMinWidth;
    private DockControl? _parentDock;
    private ToolTip? _toolTip;
    private long _dockSideCallbackToken = -1;
    private long _dockSizeCallbackToken = -1;

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

    internal bool IsIconOnly => ShouldShowIcon() && (TextVisibility == Visibility.Collapsed || !HasText);

    private void UpdateTextVisibility()
    {
        UpdateTextVisibilityState();
        UpdateSubtitleVisibilityState();
        UpdateContentSpacingState();
        UpdateSquareChrome();
    }

    private void UpdateTextVisibilityState()
    {
        // When TextVisibility is Collapsed, always hide text and collapse the
        // grid column/spacing so the icon-only layout doesn't waste space.
        if (TextVisibility == Visibility.Collapsed)
        {
            VisualStateManager.GoToState(this, "TextHidden", true);
            return;
        }

        // Determine which visual state to use based on title/subtitle presence
        var stateName = (HasTitle, HasSubtitle) switch
        {
            (true, true) => "TextVisible",
            (true, false) => "TitleOnly",
            (false, true) => "SubtitleOnly",
            (false, false) => "TextHidden",
        };

        VisualStateManager.GoToState(this, stateName, true);
    }

    private void UpdateSubtitleVisibilityState()
    {
        var showSubtitle = HasSubtitle && !IsCompact;
        VisualStateManager.GoToState(this, showSubtitle ? "SubtitleVisible" : "SubtitleHidden", true);
    }

    private void UpdateIconVisibility()
    {
        var shouldShowIcon = ShouldShowIcon();
        if (_iconPresenter is not null)
        {
            _iconPresenter.Visibility = shouldShowIcon ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateIconVisibilityState();
        UpdateContentSpacingState();
        UpdateSquareChrome();
    }

    /// <summary>
    /// Keeps icon-only items square by driving the chrome's width from the height the
    /// dock hands us. MinWidth rather than Width, so an oversized icon can still grow
    /// the item instead of being clipped.
    /// </summary>
    private void UpdateSquareChrome()
    {
        if (_backPlate is null)
        {
            return;
        }

        // Only horizontal docks have an authoritative height to square against - in a
        // vertical dock items stretch to the dock's full width instead. Before the first
        // layout pass there's no height yet, so the template's floor stands.
        var horizontal = _parentDock?.DockSide is not (DockSide.Left or DockSide.Right);
        var square = IsIconOnly && horizontal && _backPlate.ActualHeight > 0;
        var minWidth = square ? _backPlate.ActualHeight : _backPlateMinWidth;

        if (Math.Abs(_backPlate.MinWidth - minWidth) > 0.5)
        {
            _backPlate.MinWidth = minWidth;
        }
    }

    private void BackPlate_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateSquareChrome();

    private void UpdateIconVisibilityState()
    {
        VisualStateManager.GoToState(this, ShouldShowIcon() ? "IconVisible" : "IconHidden", true);
    }

    private void UpdateContentSpacingState()
    {
        var showSpacing = TextVisibility != Visibility.Collapsed && HasText && ShouldShowIcon();
        VisualStateManager.GoToState(this, showSpacing ? "ContentSpacingVisible" : "ContentSpacingHidden", true);
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
        UpdateCompactState();
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

        // Wait until the control is connected to a XamlRoot before creating
        // the tooltip popup; dock items are materialized very early in startup.
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

        if (_backPlate is not null)
        {
            _backPlate.SizeChanged -= BackPlate_SizeChanged;
        }

        // Get template children for visibility updates
        _iconPresenter = GetTemplateChild(IconPresenterName) as FrameworkElement;
        _backPlate = GetTemplateChild(BackPlateName) as FrameworkElement;

        if (_backPlate is not null)
        {
            // Remember the template's floor so non-square items can be put back.
            _backPlateMinWidth = _backPlate.MinWidth;
            _backPlate.SizeChanged += BackPlate_SizeChanged;
        }

        // Set initial visibility
        UpdateAllVisibility();
    }

    private void DockItemControl_Loaded(object sender, RoutedEventArgs e)
    {
        // Walk the visual tree to find our parent DockControl and watch its DockSide.
        // This lets us extend the hit-test area toward the screen edge.
        DependencyObject? parent = VisualTreeHelper.GetParent(this);
        while (parent is not null and not DockControl)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }

        if (parent is DockControl dock)
        {
            _parentDock = dock;
            UpdateInnerMargin();
            UpdateCompactFromParent(dock);
            UpdateAllVisibility();
            _dockSideCallbackToken = dock.RegisterPropertyChangedCallback(
                DockControl.DockSideProperty,
                OnParentDockSideChanged);
            _dockSizeCallbackToken = dock.RegisterPropertyChangedCallback(
                DockControl.DockSizeProperty,
                OnParentDockSizeChanged);
        }

        UpdateToolTip();
    }

    private void DockItemControl_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateIconVisibility();
        UpdateAlignment();
    }

    private void DockItemControl_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_parentDock is not null)
        {
            if (_dockSideCallbackToken >= 0)
            {
                _parentDock.UnregisterPropertyChangedCallback(
                    DockControl.DockSideProperty,
                    _dockSideCallbackToken);
                _dockSideCallbackToken = -1;
            }

            if (_dockSizeCallbackToken >= 0)
            {
                _parentDock.UnregisterPropertyChangedCallback(
                    DockControl.DockSizeProperty,
                    _dockSizeCallbackToken);
                _dockSizeCallbackToken = -1;
            }

            _parentDock = null;
        }

        ToolTipService.SetToolTip(this, null);
        _toolTip = null;
    }

    private void OnParentDockSideChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is DockControl dock)
        {
            UpdateInnerMargin();
            UpdateAlignment();
            UpdateSquareChrome();
        }
    }

    private void OnParentDockSizeChanged(DependencyObject sender, DependencyProperty dp)
    {
        if (sender is DockControl dock)
        {
            UpdateCompactFromParent(dock);
        }
    }

    private void UpdateCompactFromParent(DockControl dock)
    {
        IsCompact = dock.DockSize == DockSize.Compact;
    }

    /// <summary>
    /// Insets the chrome from the item's bounds. The bounds stay transparent and
    /// hit-testable, so the button still reaches the screen edge (Fitts's law) even
    /// though it no longer looks flush against it.
    /// </summary>
    private void UpdateInnerMargin()
    {
        var side = _parentDock?.DockSide ?? DockSide.Top;

        // Compact trades the gap for height - the dock zeroes its own margins there too.
        var edgeGap = IsCompact
            ? 0
            : side is DockSide.Left or DockSide.Right ? VerticalDockEdgeGap : HorizontalDockEdgeGap;

        InnerMargin = new Thickness(
            ChromeGap.Left + (side == DockSide.Left ? edgeGap : 0),
            ChromeGap.Top + (side == DockSide.Top ? edgeGap : 0),
            ChromeGap.Right + (side == DockSide.Right ? edgeGap : 0),
            ChromeGap.Bottom + (side == DockSide.Bottom ? edgeGap : 0));
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

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);

        // The pointer is still over us on release, so hand back to PointerOver;
        // PointerExited takes it to Normal from there.
        if (IsEnabled)
        {
            VisualStateManager.GoToState(this, "PointerOver", true);
        }
    }

    protected override void OnPointerCanceled(PointerRoutedEventArgs e)
    {
        base.OnPointerCanceled(e);
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
    }
}
