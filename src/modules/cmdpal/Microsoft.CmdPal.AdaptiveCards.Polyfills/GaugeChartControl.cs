// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills;

/// <summary>
/// Renders an Adaptive Cards <c>Chart.Gauge</c> element as a semicircular gauge. The value fills
/// the track in the color of the segment it falls in, and the segments form a thin scale inside it.
/// </summary>
internal sealed partial class GaugeChartControl : AdaptiveVisualControl
{
    private const double MaximumDiameter = 220;
    private const double MinimumDiameter = 96;
    private const double ThicknessRatio = 0.08;
    private const double SegmentGap = 0.008;
    private const double ScaleGap = 4;
    private const double NeedleThickness = 3;
    private const double NeedleHalo = 2;
    private const double NeedleOverhang = 3;

    // Room around the arc for the needle's ends.
    private const double Inset = NeedleOverhang + (NeedleThickness / 2) + NeedleHalo;

    private GaugeChartModel _model;

    public GaugeChartControl(GaugeChartModel model)
    {
        _model = model;
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((GaugeChartControl)candidate)._model;

    protected override void RenderCore()
    {
        var isDarkTheme = IsDarkTheme;
        var isHighContrast = ChartTheme.IsHighContrast;
        var secondary = ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: true));
        var root = new StackPanel { Spacing = 8 };
        if (_model.ShowTitle && !string.IsNullOrWhiteSpace(_model.Title))
        {
            root.Children.Add(ChartShapes.CreateText(_model.Title, ChartShapes.BodyStrongStyle));
        }

        var diameter = Math.Clamp(LayoutWidth, MinimumDiameter, MaximumDiameter);
        var thickness = Math.Round(diameter * ThicknessRatio);
        var radius = (diameter - thickness) / 2;
        var center = new ChartPoint(Inset + (diameter / 2), Inset + (diameter / 2));
        var labelHeight = _model.ShowMinMax ? 18 : 0;
        var canvas = new Canvas
        {
            Width = diameter + (2 * Inset),
            Height = center.Y + (thickness / 2) + labelHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var colors = new ChartColor[_model.Segments.Count];
        var outlines = new bool[colors.Length];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = ChartTheme.Resolve(_model.Segments[i].Color, null, _model.ColorSet, i, isDarkTheme);
            outlines[i] = isHighContrast && ChartPalette.IsHighContrastOutline(i);
        }

        var fraction = _model.Fraction;
        var active = _model.GetActiveSegment();
        var valueColor = _model.Segments.Count == 0
            ? ChartTheme.Resolve(_model.ColorSet is null ? ChartPalette.AccentColorName : null, null, _model.ColorSet, 0, isDarkTheme)
            : active >= 0 ? colors[active] : ChartTheme.GetAccent(isDarkTheme);

        // High contrast draws full-width solid or outlined segments, and the needle marks the value.
        if (isHighContrast && _model.Segments.Count > 0)
        {
            AddSegments(canvas, center, radius, thickness, colors, outlines, roundEnds: false);
        }
        else
        {
            canvas.Children.Add(ChartShapes.CreateArc(center, radius, 180, 0, ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme)), thickness, PenLineCap.Round));
            if (fraction > 0.001)
            {
                // The needle marks the end of the fill, except at the maximum.
                var fill = ChartShapes.CreateArc(center, radius, 180, ChartArc.GaugeAngle(fraction), ChartTheme.ToBrush(valueColor), thickness, PenLineCap.Round);
                fill.StrokeEndLineCap = _model.ShowNeedle && fraction < 0.999 ? PenLineCap.Flat : PenLineCap.Round;
                canvas.Children.Add(fill);
            }

            var scaleThickness = Math.Max(3, Math.Round(thickness * 0.2));
            var scaleRadius = radius - (thickness / 2) - ScaleGap - (scaleThickness / 2);
            AddSegments(canvas, center, scaleRadius, scaleThickness, colors, new bool[colors.Length], roundEnds: true);
        }

        if (_model.ShowNeedle)
        {
            var angle = ChartArc.GaugeAngle(fraction);
            var inner = ChartArc.PointAt(center, radius - (thickness / 2) - NeedleOverhang, angle);
            var outer = ChartArc.PointAt(center, radius + (thickness / 2) + NeedleOverhang, angle);

            // In high contrast a segment can share the needle's color, so a halo keeps it visible.
            if (isHighContrast)
            {
                canvas.Children.Add(CreateNeedle(inner, outer, ChartTheme.GetHighContrastBackground(), NeedleThickness + (2 * NeedleHalo)));
            }

            canvas.Children.Add(CreateNeedle(inner, outer, ChartTheme.GetTextColor(isDarkTheme, secondary: false), NeedleThickness));
        }

        // The value and its sub label sit inside the arc.
        var valueText = new TextBlock
        {
            Text = _model.FormatValue(CultureInfo.CurrentCulture),
            FontSize = diameter >= 180 ? 28 : diameter >= 130 ? 20 : 16,
            FontWeight = FontWeights.SemiBold,
        };
        var bottom = center.Y - 2;
        if (!string.IsNullOrWhiteSpace(_model.SubLabel))
        {
            var subText = ChartShapes.CreateText(_model.SubLabel, ChartShapes.CaptionStyle, secondary);
            var subSize = ChartShapes.Measure(subText);
            ChartShapes.Place(subText, center.X - (subSize.Width / 2), bottom - subSize.Height);
            canvas.Children.Add(subText);
            bottom -= subSize.Height;
        }

        var valueSize = ChartShapes.Measure(valueText);
        ChartShapes.Place(valueText, center.X - (valueSize.Width / 2), bottom - valueSize.Height + 4);
        canvas.Children.Add(valueText);

        if (_model.ShowMinMax)
        {
            var top = center.Y + (thickness / 2) + 2;
            AddCenteredLabel(canvas, ChartValueFormatter.FormatCompact(_model.Min), center.X - radius, top, secondary);
            AddCenteredLabel(canvas, ChartValueFormatter.FormatCompact(_model.Max), center.X + radius, top, secondary);
        }

        root.Children.Add(canvas);

        if (_model.ShowLegend && _model.Segments.Count > 0)
        {
            var legend = new WrapPanel { HorizontalSpacing = 12, VerticalSpacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            for (var i = 0; i < _model.Segments.Count; i++)
            {
                legend.Children.Add(ChartShapes.CreateLegendEntry(colors[i], _model.Segments[i].Label ?? string.Empty, null, outlines[i]));
            }

            root.Children.Add(legend);
        }

        Content = root;
        AutomationProperties.SetName(this, FirstNonEmpty(_model.Title, _model.SubLabel) ?? ChartStrings.Get("AdaptiveChart_Gauge"));
        AutomationProperties.SetHelpText(this, valueText.Text);
    }

    private void AddSegments(
        Canvas canvas,
        ChartPoint center,
        double radius,
        double thickness,
        IReadOnlyList<ChartColor> colors,
        IReadOnlyList<bool> outlines,
        bool roundEnds)
    {
        var range = _model.Max - _model.Min;
        var shares = new double[_model.Segments.Count];
        for (var i = 0; i < shares.Length; i++)
        {
            shares[i] = Math.Max(0, _model.Segments[i].Value) / range;
        }

        var spans = ChartArc.GetSpans(shares, SegmentGap, closed: false);
        var first = -1;
        var last = -1;
        for (var i = 0; i < spans.Count; i++)
        {
            if (spans[i].End > spans[i].Start)
            {
                first = first < 0 ? i : first;
                last = i;
            }
        }

        for (var i = first; i >= 0 && i <= last; i++)
        {
            if (spans[i].End <= spans[i].Start)
            {
                continue;
            }

            var start = ChartArc.GaugeAngle(spans[i].Start);
            var end = ChartArc.GaugeAngle(spans[i].End);
            var brush = ChartTheme.ToBrush(colors[i]);
            if (outlines[i])
            {
                canvas.Children.Add(ChartShapes.CreateRingSegmentOutline(center, radius, thickness, start, end, brush));
                continue;
            }

            var arc = ChartShapes.CreateArc(center, radius, start, end, brush, thickness, PenLineCap.Flat);
            if (roundEnds && i == first)
            {
                arc.StrokeStartLineCap = PenLineCap.Round;
            }

            if (roundEnds && i == last)
            {
                arc.StrokeEndLineCap = PenLineCap.Round;
            }

            canvas.Children.Add(arc);
        }
    }

    private static Line CreateNeedle(ChartPoint inner, ChartPoint outer, ChartColor color, double thickness) => new()
    {
        X1 = inner.X,
        Y1 = inner.Y,
        X2 = outer.X,
        Y2 = outer.Y,
        Stroke = ChartTheme.ToBrush(color),
        StrokeThickness = thickness,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private static void AddCenteredLabel(Canvas canvas, string text, double centerX, double top, Brush foreground)
    {
        var label = ChartShapes.CreateText(text, ChartShapes.CaptionStyle, foreground);
        var size = ChartShapes.Measure(label);
        ChartShapes.Place(label, Math.Clamp(centerX - (size.Width / 2), 0, Math.Max(0, canvas.Width - size.Width)), top);
        canvas.Children.Add(label);
    }

    private static string? FirstNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first : !string.IsNullOrWhiteSpace(second) ? second : null;
}
