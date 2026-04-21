// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI.Dock;

/// <summary>
/// A control that arranges Start, Center, and End sections in a dock layout
/// with built-in ScrollContainers. When <see cref="IsCenterVisible"/> is false,
/// the center is collapsed, the start section stretches, and the end section
/// auto-sizes. Supports horizontal/vertical orientation and edit mode styling.
/// </summary>
public sealed partial class DockContentControl : UserControl
{
    public static readonly DependencyProperty OrientationProperty =
        DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(DockContentControl), new PropertyMetadata(Orientation.Horizontal));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public static readonly DependencyProperty IsCenterVisibleProperty =
        DependencyProperty.Register(nameof(IsCenterVisible), typeof(bool), typeof(DockContentControl), new PropertyMetadata(true));

    public bool IsCenterVisible
    {
        get => (bool)GetValue(IsCenterVisibleProperty);
        set => SetValue(IsCenterVisibleProperty, value);
    }

    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(nameof(IsEditMode), typeof(bool), typeof(DockContentControl), new PropertyMetadata(false));

    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    public static readonly DependencyProperty StartSourceProperty =
        DependencyProperty.Register(nameof(StartSource), typeof(object), typeof(DockContentControl), new PropertyMetadata(null));

    public object StartSource
    {
        get => GetValue(StartSourceProperty);
        set => SetValue(StartSourceProperty, value);
    }

    public static readonly DependencyProperty StartActionButtonProperty =
        DependencyProperty.Register(nameof(StartActionButton), typeof(object), typeof(DockContentControl), new PropertyMetadata(null));

    public object StartActionButton
    {
        get => GetValue(StartActionButtonProperty);
        set => SetValue(StartActionButtonProperty, value);
    }

    public static readonly DependencyProperty CenterSourceProperty =
        DependencyProperty.Register(nameof(CenterSource), typeof(object), typeof(DockContentControl), new PropertyMetadata(null));

    public object CenterSource
    {
        get => GetValue(CenterSourceProperty);
        set => SetValue(CenterSourceProperty, value);
    }

    public static readonly DependencyProperty CenterActionButtonProperty =
        DependencyProperty.Register(nameof(CenterActionButton), typeof(object), typeof(DockContentControl), new PropertyMetadata(null));

    public object CenterActionButton
    {
        get => GetValue(CenterActionButtonProperty);
        set => SetValue(CenterActionButtonProperty, value);
    }

    public static readonly DependencyProperty EndSourceProperty =
        DependencyProperty.Register(nameof(EndSource), typeof(object), typeof(DockContentControl), new PropertyMetadata(null));

    public object EndSource
    {
        get => GetValue(EndSourceProperty);
        set => SetValue(EndSourceProperty, value);
    }

    public static readonly DependencyProperty EndActionButtonProperty =
        DependencyProperty.Register(nameof(EndActionButton), typeof(object), typeof(DockContentControl), new PropertyMetadata(null));

    public object EndActionButton
    {
        get => GetValue(EndActionButtonProperty);
        set => SetValue(EndActionButtonProperty, value);
    }

    public DockContentControl()
    {
        this.InitializeComponent();
    }
}
