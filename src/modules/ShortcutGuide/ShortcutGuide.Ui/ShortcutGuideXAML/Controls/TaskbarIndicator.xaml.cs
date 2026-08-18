// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Numerics;
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

    // The layout currently applied to the grid. Indicators are pooled and
    // reused across opens, so we only rebuild the row/column layout when the
    // taskbar edge (and therefore the tail direction) actually changes.
    private IndicatorTailDirection? _appliedDirection;

    // The most recently requested direction; drives the one-shot entrance
    // animation even when the layout did not need rebuilding.
    private IndicatorTailDirection _currentDirection = IndicatorTailDirection.Down;

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
    /// Resizes the indicator body so it stays proportional to the actual
    /// taskbar button it labels. Windows shrinks taskbar buttons when icons are
    /// set to the small size (or when many apps are open and buttons are
    /// combined), so the caller derives <paramref name="bodyDip"/> from the
    /// measured button rect; the font scales with it to stay centered.
    /// </summary>
    internal void SetBodySize(double bodyDip)
    {
        IndicatorRectangle.MinWidth = bodyDip;
        IndicatorRectangle.MinHeight = bodyDip;
        IndicatorRectangle.Padding = new Thickness(Math.Max(2, Math.Round(bodyDip * 0.2)));
        IndicatorText.FontSize = Math.Max(11, Math.Round(bodyDip * 0.4));
    }

    /// <summary>
    /// Reflows the body and triangle tail so the tail points toward
    /// <paramref name="direction"/>. Indicators are pooled and reused across
    /// opens, so the (relatively expensive) grid rebuild is skipped when the
    /// direction is unchanged. Replaces the previously hard-coded "tail points
    /// down" layout so the control works for a taskbar on any screen edge.
    /// </summary>
    internal void ApplyTailDirection(IndicatorTailDirection direction)
    {
        _currentDirection = direction;

        if (_appliedDirection == direction)
        {
            return;
        }

        _appliedDirection = direction;

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
    }

    private static Geometry ParseGeometry(string path) =>
        (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), path);

    /// <summary>
    /// Plays the Windows 11 system-flyout entrance motion (slide in from the
    /// taskbar edge + fade, ~367ms, no scale) as a one-shot animation. This
    /// replaces the previous CommunityToolkit <c>Implicit</c> show/hide
    /// animations: those attach persistent composition
    /// <c>ImplicitAnimationCollection</c>s to the element's visual, which —
    /// because indicators used to be recreated on every open — accumulated
    /// composition resources that managed GC never reclaimed. Indicators are
    /// now pooled, and the entrance is replayed explicitly on each open via a
    /// transient <see cref="AnimationBuilder"/> batch that completes and frees
    /// itself.
    /// </summary>
    internal void PlayEntrance()
    {
        Vector3 from = _currentDirection switch
        {
            IndicatorTailDirection.Down => new Vector3(0, 12, 0),
            IndicatorTailDirection.Up => new Vector3(0, -12, 0),
            IndicatorTailDirection.Left => new Vector3(-12, 0, 0),
            IndicatorTailDirection.Right => new Vector3(12, 0, 0),
            _ => new Vector3(0, 12, 0),
        };

        AnimationBuilder.Create()
            .Opacity(to: 1.0, from: 0.0, duration: TimeSpan.FromMilliseconds(367))
            .Translation(
                to: Vector3.Zero,
                from: from,
                duration: TimeSpan.FromMilliseconds(367),
                easingType: EasingType.Cubic,
                easingMode: Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut)
            .Start(this);
    }
}
