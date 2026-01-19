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
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DockItemControl), new PropertyMetadata(null));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty HasTextProperty =
        DependencyProperty.Register(nameof(HasText), typeof(bool), typeof(DockItemControl), new PropertyMetadata(true));

    public bool HasText
    {
        get => (bool)GetValue(HasTextProperty);
        set => SetValue(HasTextProperty, value);
    }

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(DockItemControl), new PropertyMetadata(null));

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public static readonly DependencyProperty HasSubtitleProperty =
        DependencyProperty.Register(nameof(HasSubtitle), typeof(bool), typeof(DockItemControl), new PropertyMetadata(false));

    public bool HasSubtitle
    {
        get => (bool)GetValue(HasSubtitleProperty);
        set => SetValue(HasSubtitleProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(object), typeof(DockItemControl), new PropertyMetadata(null));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly DependencyProperty HasIconProperty =
        DependencyProperty.Register(nameof(HasIcon), typeof(bool), typeof(DockItemControl), new PropertyMetadata(true));

    public bool HasIcon
    {
        get => (bool)GetValue(HasIconProperty);
        set => SetValue(HasIconProperty, value);
    }

    public event TappedEventHandler? ItemTapped;

    public event RightTappedEventHandler? ItemRightTapped;

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        // Wire up pointer events from the root border
        if (GetTemplateChild("PART_RootGrid") is Border rootBorder)
        {
            rootBorder.Tapped += RootBorder_Tapped;
            rootBorder.RightTapped += RootBorder_RightTapped;
            rootBorder.PointerEntered += RootBorder_PointerEntered;
            rootBorder.PointerExited += RootBorder_PointerExited;
        }
    }

    private void RootBorder_Tapped(object sender, TappedRoutedEventArgs e)
    {
        ItemTapped?.Invoke(this, e);
    }

    private void RootBorder_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        ItemRightTapped?.Invoke(this, e);
    }

    private void RootBorder_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "PointerOver", true);
    }

    private void RootBorder_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);
    }
}
