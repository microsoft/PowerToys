// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ShortcutGuide.Controls;

/// <summary>
/// The edge of the indicator body that the triangle tail is attached to,
/// i.e. the direction it points. The tail always points toward the taskbar,
/// so this mirrors the screen edge the taskbar is docked to.
/// </summary>
internal enum IndicatorTailDirection
{
    Down,
    Up,
    Left,
    Right,
}

public sealed partial class TaskbarIndicator : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(TaskbarIndicator), new PropertyMetadata(default(string)));

    public TaskbarIndicator()
    {
        this.InitializeComponent();
        ApplyTailDirection(IndicatorTailDirection.Down);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Reflows the body and triangle tail and (re)configures the slide-in /
    /// slide-out animation so the tail points toward <paramref name="direction"/>
    /// and the indicator animates in from that same edge. Replaces the previously
    /// hard-coded "tail points down" layout so the control works for a taskbar on
    /// any screen edge.
    /// </summary>
    internal void ApplyTailDirection(IndicatorTailDirection direction)
    {
        RootGrid.RowDefinitions.Clear();
        RootGrid.ColumnDefinitions.Clear();

        // The tail is a 12x6 triangle whose two diagonal edges are stroked and
        // whose base (the side touching the body) is left open; a 1px negative
        // margin on the body side overlaps the body border to hide the seam.
        switch (direction)
        {
            case IndicatorTailDirection.Down:
                RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(IndicatorRectangle, 0);
                Grid.SetRow(Tail, 1);
                Tail.HorizontalAlignment = HorizontalAlignment.Center;
                Tail.VerticalAlignment = VerticalAlignment.Top;
                Tail.Margin = new Thickness(0, -1, 0, 0);
                Tail.Data = ParseGeometry("M 0,0 L 6,6 L 12,0");
                break;

            case IndicatorTailDirection.Up:
                RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                RootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(Tail, 0);
                Grid.SetRow(IndicatorRectangle, 1);
                Tail.HorizontalAlignment = HorizontalAlignment.Center;
                Tail.VerticalAlignment = VerticalAlignment.Bottom;
                Tail.Margin = new Thickness(0, 0, 0, -1);
                Tail.Data = ParseGeometry("M 0,6 L 6,0 L 12,6");
                break;

            case IndicatorTailDirection.Left:
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(Tail, 0);
                Grid.SetColumn(IndicatorRectangle, 1);
                Tail.VerticalAlignment = VerticalAlignment.Center;
                Tail.HorizontalAlignment = HorizontalAlignment.Right;
                Tail.Margin = new Thickness(0, 0, -1, 0);
                Tail.Data = ParseGeometry("M 6,0 L 0,6 L 6,12");
                break;

            case IndicatorTailDirection.Right:
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(IndicatorRectangle, 0);
                Grid.SetColumn(Tail, 1);
                Tail.VerticalAlignment = VerticalAlignment.Center;
                Tail.HorizontalAlignment = HorizontalAlignment.Left;
                Tail.Margin = new Thickness(-1, 0, 0, 0);
                Tail.Data = ParseGeometry("M 0,0 L 6,6 L 0,12");
                break;
        }

        ApplySlideAnimations(direction);
    }

    private static Geometry ParseGeometry(string path) =>
        (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), path);

    private void ApplySlideAnimations(IndicatorTailDirection direction)
    {
        // Windows 11 system-flyout motion: slide in from the taskbar edge + fade
        // (~367ms entrance, ~200ms exit, no scale). The slide offset points away
        // from the taskbar (the same edge the tail points to).
        string slideFrom = direction switch
        {
            IndicatorTailDirection.Down => "0,12,0",
            IndicatorTailDirection.Up => "0,-12,0",
            IndicatorTailDirection.Left => "-12,0,0",
            IndicatorTailDirection.Right => "12,0,0",
            _ => "0,12,0",
        };

        var showAnimations = new ImplicitAnimationSet();
        showAnimations.Add(new OpacityAnimation { From = 0, To = 1.0, Duration = TimeSpan.FromMilliseconds(367) });
        showAnimations.Add(new TranslationAnimation
        {
            From = slideFrom,
            To = "0,0,0",
            Duration = TimeSpan.FromMilliseconds(367),
            EasingType = EasingType.Cubic,
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut,
        });

        var hideAnimations = new ImplicitAnimationSet();
        hideAnimations.Add(new OpacityAnimation { From = 1.0, To = 0, Duration = TimeSpan.FromMilliseconds(200) });
        hideAnimations.Add(new TranslationAnimation
        {
            From = "0,0,0",
            To = slideFrom,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingType = EasingType.Cubic,
            EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseIn,
        });

        Implicit.SetShowAnimations(RootGrid, showAnimations);
        Implicit.SetHideAnimations(RootGrid, hideAnimations);
    }
}
