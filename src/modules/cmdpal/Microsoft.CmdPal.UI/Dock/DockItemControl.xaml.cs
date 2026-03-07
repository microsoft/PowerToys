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

    private const string IconPresenterName = "IconPresenter";

    private FrameworkElement? _iconPresenter;
    private DockControl? _parentDock;
    private long _dockSideCallbackToken = -1;

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
        UpdateAlignment();
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

        // Get template children for visibility updates
        _iconPresenter = GetTemplateChild(IconPresenterName) as FrameworkElement;

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
            UpdateInnerMarginForDockSide(dock.DockSide);
            UpdateAllVisibility();
            _dockSideCallbackToken = dock.RegisterPropertyChangedCallback(
                DockControl.DockSideProperty,
                OnParentDockSideChanged);
        }
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
        // Push the visual (PART_RootGrid) inward on the screen-edge side so
        // the transparent hit-test area extends all the way to the edge.
        // The values here compensate for the margin/padding removed from the
        // DockControl's ContentGrid on the screen-edge side.
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
