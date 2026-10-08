// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Builds the XAML shapes and text shared by the native chart controls.</summary>
internal static class ChartShapes
{
    public const string CaptionStyle = "CaptionTextBlockStyle";
    public const string BodyStyle = "BodyTextBlockStyle";
    public const string BodyStrongStyle = "BodyStrongTextBlockStyle";

    public static Point ToPoint(ChartPoint point) => new(point.X, point.Y);

    /// <summary>Creates an arc that runs clockwise from <paramref name="startDegrees"/> to <paramref name="endDegrees"/>.</summary>
    public static Path CreateArc(
        ChartPoint center,
        double radius,
        double startDegrees,
        double endDegrees,
        Brush stroke,
        double thickness,
        PenLineCap cap)
    {
        var figure = new PathFigure
        {
            StartPoint = ToPoint(ChartArc.PointAt(center, radius, startDegrees)),
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = ToPoint(ChartArc.PointAt(center, radius, endDegrees)),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = startDegrees - endDegrees > 180,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = cap,
            StrokeEndLineCap = cap,
        };
    }

    /// <summary>Creates a filled pie wedge that runs clockwise between two angles.</summary>
    public static Path CreateWedge(ChartPoint center, double radius, double startDegrees, double endDegrees, Brush fill)
    {
        var figure = new PathFigure { StartPoint = ToPoint(center), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new LineSegment { Point = ToPoint(ChartArc.PointAt(center, radius, startDegrees)) });
        figure.Segments.Add(new ArcSegment
        {
            Point = ToPoint(ChartArc.PointAt(center, radius, endDegrees)),
            Size = new Size(radius, radius),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = startDegrees - endDegrees > 180,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path { Data = geometry, Fill = fill };
    }

    public static Ellipse CreateCircle(ChartPoint center, double diameter, Brush? fill, Brush? stroke = null, double strokeThickness = 0)
    {
        var circle = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = strokeThickness,
        };
        Place(circle, center.X - (diameter / 2), center.Y - (diameter / 2));
        return circle;
    }

    public static TextBlock CreateText(string text, string styleKey, Brush? foreground = null)
    {
        var block = new TextBlock { Text = text, Style = ChartTheme.GetTextStyle(styleKey) };
        if (foreground is not null)
        {
            block.Foreground = foreground;
        }

        return block;
    }

    /// <summary>Measures a detached element at its natural size.</summary>
    public static Size Measure(UIElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize;
    }

    public static void Place(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
    }

    /// <summary>Creates a legend entry: a colored dot followed by its label.</summary>
    public static FrameworkElement CreateLegendEntry(ChartColor color, string label, Brush? foreground)
    {
        var entry = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        entry.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = ChartTheme.ToBrush(color),
            VerticalAlignment = VerticalAlignment.Center,
        });
        var text = CreateText(label, CaptionStyle, foreground);
        text.VerticalAlignment = VerticalAlignment.Center;
        entry.Children.Add(text);
        return entry;
    }
}
