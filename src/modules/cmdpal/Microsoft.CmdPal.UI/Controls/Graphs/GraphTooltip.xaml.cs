// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls.Graphs;

public sealed partial class GraphTooltip : UserControl
{
    private Row[] _rows = [];
    private Point? _relativeAnchor;

    public GraphTooltip()
    {
        InitializeComponent();
    }

    internal Color? FallbackColor => TooltipBorder.Background switch
    {
        SolidColorBrush brush => brush.Color,
        AcrylicBrush brush => brush.FallbackColor,
        _ => null,
    };

    // The anchor is relative to the overlay's bounds. Values are already formatted
    // by the caller; this control does not interpret timestamps or graph samples.
    internal string? Show(Point relativeAnchor, string heading, ReadOnlySpan<GraphTooltipItem> items)
    {
        _relativeAnchor = relativeAnchor;
        Heading.Text = heading;
        EnsureRows(items.Length);

        var description = new StringBuilder(heading);
        var hasValues = false;
        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            var row = _rows[index];
            if (item.Value is not { } value)
            {
                row.Root.Visibility = Visibility.Collapsed;
                continue;
            }

            hasValues = true;
            row.Root.Visibility = Visibility.Visible;
            row.Name.Text = item.Name;
            row.Value.Text = value;
            row.Swatch.Visibility = item.Color.HasValue ? Visibility.Visible : Visibility.Collapsed;
            if (item.Color is { } color)
            {
                row.Brush.Color = color;
            }

            description.AppendLine().Append(item.Name).Append(": ").Append(value);
        }

        TooltipBorder.Visibility = hasValues ? Visibility.Visible : Visibility.Collapsed;
        UpdatePosition();
        return hasValues ? description.ToString() : null;
    }

    internal void Hide()
    {
        _relativeAnchor = null;
        TooltipBorder.Visibility = Visibility.Collapsed;
    }

    private void EnsureRows(int count)
    {
        if (_rows.Length == count)
        {
            return;
        }

        Rows.Children.Clear();
        _rows = new Row[count];
        var template = (DataTemplate)Resources["RowTemplate"];
        for (var index = 0; index < count; index++)
        {
            var root = (Grid)template.LoadContent();
            var swatch = (Ellipse)root.FindName("Swatch");
            _rows[index] = new(
                root,
                swatch,
                (SolidColorBrush)swatch.Fill,
                (TextBlock)root.FindName("SeriesName"),
                (TextBlock)root.FindName("Value"));
            Rows.Children.Add(root);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args) => UpdatePosition();

    private void UpdatePosition()
    {
        if (_relativeAnchor is not { } anchor || TooltipBorder.Visibility != Visibility.Visible)
        {
            return;
        }

        var width = ActualWidth;
        var height = ActualHeight;
        var tooltipWidth = Math.Min(TooltipBorder.MaxWidth, Math.Max(0, width - 16));
        TooltipBorder.Width = tooltipWidth;
        var x = anchor.X * width;
        var left = x + 12 + tooltipWidth <= width - 8 ? x + 12 : x - tooltipWidth - 12;
        var top = (anchor.Y * height) - (TooltipBorder.ActualHeight / 2);
        Canvas.SetLeft(TooltipBorder, Math.Clamp(left, 8, Math.Max(8, width - tooltipWidth - 8)));
        Canvas.SetTop(TooltipBorder, Math.Clamp(top, 8, Math.Max(8, height - TooltipBorder.ActualHeight - 8)));
    }

    private readonly record struct Row(Grid Root, Ellipse Swatch, SolidColorBrush Brush, TextBlock Name, TextBlock Value);
}
