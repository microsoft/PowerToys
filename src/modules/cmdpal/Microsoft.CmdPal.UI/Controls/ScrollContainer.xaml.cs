// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ScrollContainer : UserControl
{
    public enum ScrollContentAlignment
    {
        Start,
        End,
    }

    public ScrollContainer()
    {
        InitializeComponent();
        Loaded += ScrollContainer_Loaded;
    }

    private void ScrollContainer_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateOrientationState();
    }

    public object Source
    {
        get => (object)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(ScrollContainer), new PropertyMetadata(null));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(ScrollContainer), new PropertyMetadata(Orientation.Horizontal, OnOrientationChanged));

    public ScrollContentAlignment ContentAlignment
    {
        get => (ScrollContentAlignment)GetValue(ContentAlignmentProperty);
        set => SetValue(ContentAlignmentProperty, value);
    }

    public static readonly DependencyProperty ContentAlignmentProperty =
        DependencyProperty.Register(nameof(ContentAlignment), typeof(ScrollContentAlignment), typeof(ScrollContainer), new PropertyMetadata(ScrollContentAlignment.Start, OnContentAlignmentChanged));

    public object ActionButton
    {
        get => (object)GetValue(ActionButtonProperty);
        set => SetValue(ActionButtonProperty, value);
    }

    public static readonly DependencyProperty ActionButtonProperty =
        DependencyProperty.Register(nameof(ActionButton), typeof(object), typeof(ScrollContainer), new PropertyMetadata(null));

    public Visibility ActionButtonVisibility
    {
        get => (Visibility)GetValue(ActionButtonVisibilityProperty);
        set => SetValue(ActionButtonVisibilityProperty, value);
    }

    public static readonly DependencyProperty ActionButtonVisibilityProperty =
        DependencyProperty.Register(nameof(ActionButtonVisibility), typeof(Visibility), typeof(ScrollContainer), new PropertyMetadata(Visibility.Collapsed));

    private static void OnContentAlignmentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollContainer control)
        {
            control.ScrollToAlignment();
        }
    }

    private void ScrollToAlignment()
    {
        // Reset button visibility
        ScrollBackBtn.Visibility = Visibility.Collapsed;
        ScrollForwardBtn.Visibility = Visibility.Collapsed;

        if (ContentAlignment == ScrollContentAlignment.End)
        {
            // Scroll to the end
            if (Orientation == Orientation.Horizontal)
            {
                scroller.ChangeView(scroller.ScrollableWidth, null, null, true);
            }
            else
            {
                scroller.ChangeView(null, scroller.ScrollableHeight, null, true);
            }
        }
        else
        {
            // Scroll to the beginning
            scroller.ChangeView(0, 0, null, true);
        }

        // Defer visibility update until after layout
        void OnLayoutUpdated(object? sender, object args)
        {
            scroller.LayoutUpdated -= OnLayoutUpdated;
            UpdateScrollButtonsVisibility();
        }

        scroller.LayoutUpdated += OnLayoutUpdated;
    }

    private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollContainer control)
        {
            control.UpdateOrientationState();
            control.ScrollToAlignment();
        }
    }

    private void UpdateOrientationState()
    {
        var stateName = Orientation == Orientation.Horizontal ? "HorizontalState" : "VerticalState";
        VisualStateManager.GoToState(this, stateName, true);
    }

    private void Scroller_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
    {
        UpdateScrollButtonsVisibility(e.FinalView.HorizontalOffset, e.FinalView.VerticalOffset);
    }

    private void ScrollBackBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Orientation == Orientation.Horizontal)
        {
            scroller.ChangeView(scroller.HorizontalOffset - scroller.ViewportWidth, null, null);
        }
        else
        {
            scroller.ChangeView(null, scroller.VerticalOffset - scroller.ViewportHeight, null);
        }

        // Manually focus to ScrollForwardBtn since this button disappears after scrolling to the end.
        ScrollForwardBtn.Focus(FocusState.Programmatic);
    }

    private void ScrollForwardBtn_Click(object sender, RoutedEventArgs e)
    {
        if (Orientation == Orientation.Horizontal)
        {
            scroller.ChangeView(scroller.HorizontalOffset + scroller.ViewportWidth, null, null);
        }
        else
        {
            scroller.ChangeView(null, scroller.VerticalOffset + scroller.ViewportHeight, null);
        }

        // Manually focus to ScrollBackBtn since this button disappears after scrolling to the end.
        ScrollBackBtn.Focus(FocusState.Programmatic);
    }

    private void Scroller_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScrollButtonsVisibility();
    }

    private void UpdateScrollButtonsVisibility(double? horizontalOffset = null, double? verticalOffset = null)
    {
        var hOffset = horizontalOffset ?? scroller.HorizontalOffset;
        var vOffset = verticalOffset ?? scroller.VerticalOffset;

        if (Orientation == Orientation.Horizontal)
        {
            ScrollBackBtn.Visibility = hOffset > 1 ? Visibility.Visible : Visibility.Collapsed;
            ScrollForwardBtn.Visibility = scroller.ScrollableWidth > 0 && hOffset < scroller.ScrollableWidth - 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        else
        {
            ScrollBackBtn.Visibility = vOffset > 1 ? Visibility.Visible : Visibility.Collapsed;
            ScrollForwardBtn.Visibility = scroller.ScrollableHeight > 0 && vOffset < scroller.ScrollableHeight - 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
