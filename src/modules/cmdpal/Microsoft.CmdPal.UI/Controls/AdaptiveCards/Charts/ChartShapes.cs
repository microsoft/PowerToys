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

    /// <summary>The stroke width of a part drawn as an outline in high contrast.</summary>
    public const double OutlineThickness = 2;

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

    /// <summary>Creates a legend entry: a colored dot, or a ring for an outlined item, followed by its label.</summary>
    public static FrameworkElement CreateLegendEntry(ChartColor color, string label, Brush? foreground, bool outline = false)
    {
        var entry = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        entry.Children.Add(CreateLegendDot(ChartTheme.ToBrush(color), 8, outline));
        var text = CreateText(label, CaptionStyle, foreground);
        text.VerticalAlignment = VerticalAlignment.Center;
        entry.Children.Add(text);
        return entry;
    }

    /// <summary>Creates a legend dot: filled, or a ring for an item drawn as an outline in high contrast.</summary>
    public static Ellipse CreateLegendDot(Brush brush, double diameter, bool outline) => new()
    {
        Width = diameter,
        Height = diameter,
        Fill = outline ? null : brush,
        Stroke = outline ? brush : null,
        StrokeThickness = outline ? OutlineThickness : 0,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Creates the outline of a band around <paramref name="radius"/>, clockwise between two angles.</summary>
    public static Path CreateRingSegmentOutline(
        ChartPoint center,
        double radius,
        double bandThickness,
        double startDegrees,
        double endDegrees,
        Brush stroke)
    {
        // Keep the stroke inside the band, where a filled segment would be.
        var outer = radius + (bandThickness / 2) - (OutlineThickness / 2);
        var inner = Math.Max(0, radius - (bandThickness / 2) + (OutlineThickness / 2));
        var isLargeArc = startDegrees - endDegrees > 180;
        var figure = new PathFigure
        {
            StartPoint = ToPoint(ChartArc.PointAt(center, outer, startDegrees)),
            IsClosed = true,
            IsFilled = false,
        };
        figure.Segments.Add(new ArcSegment
        {
            Point = ToPoint(ChartArc.PointAt(center, outer, endDegrees)),
            Size = new Size(outer, outer),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = isLargeArc,
        });
        figure.Segments.Add(new LineSegment { Point = ToPoint(ChartArc.PointAt(center, inner, endDegrees)) });
        figure.Segments.Add(new ArcSegment
        {
            Point = ToPoint(ChartArc.PointAt(center, inner, startDegrees)),
            Size = new Size(inner, inner),
            SweepDirection = SweepDirection.Counterclockwise,
            IsLargeArc = isLargeArc,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return new Path
        {
            Data = geometry,
            Stroke = stroke,
            StrokeThickness = OutlineThickness,
            StrokeLineJoin = PenLineJoin.Round,
        };
    }

    /// <summary>Applies a dash pattern to a stroke. An empty pattern leaves it solid.</summary>
    public static void ApplyDashPattern(Shape shape, IReadOnlyList<double> pattern)
    {
        if (pattern.Count == 0)
        {
            return;
        }

        // Each shape needs its own collection.
        var dashes = new DoubleCollection();
        foreach (var length in pattern)
        {
            dashes.Add(length);
        }

        shape.StrokeDashArray = dashes;
        shape.StrokeDashCap = PenLineCap.Round;
    }

    /// <summary>Creates a short line for a legend that shows a line series' color and dash pattern.</summary>
    public static Line CreateLineSwatch(Brush stroke, double thickness, IReadOnlyList<double> dashPattern)
    {
        var swatch = new Line
        {
            X1 = thickness / 2,
            Y1 = thickness / 2,
            X2 = 20 + (thickness / 2),
            Y2 = thickness / 2,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyDashPattern(swatch, dashPattern);
        return swatch;
    }
}
