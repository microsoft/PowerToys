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
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Renders an Adaptive Cards <c>Chart.Gauge</c> element as a semicircular gauge.</summary>
internal sealed partial class GaugeChartControl : AdaptiveVisualControl
{
    private const double MaximumDiameter = 220;
    private const double MinimumDiameter = 96;
    private const double ThicknessRatio = 0.1;
    private const double SegmentGap = 0.008;

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
        var secondary = ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: true));
        var root = new StackPanel { Spacing = 8 };
        if (_model.ShowTitle && !string.IsNullOrWhiteSpace(_model.Title))
        {
            root.Children.Add(ChartShapes.CreateText(_model.Title, ChartShapes.BodyStrongStyle));
        }

        var diameter = Math.Clamp(LayoutWidth, MinimumDiameter, MaximumDiameter);
        var thickness = Math.Round(diameter * ThicknessRatio);
        var radius = (diameter - thickness) / 2;
        var center = new ChartPoint(diameter / 2, diameter / 2);
        var labelHeight = _model.ShowMinMax ? 18 : 0;
        var canvas = new Canvas { Width = diameter, Height = center.Y + (thickness / 2) + labelHeight, HorizontalAlignment = HorizontalAlignment.Center };

        var colors = new ChartColor[_model.Segments.Count];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = ChartTheme.Resolve(_model.Segments[i].Color, null, _model.ColorSet, i, isDarkTheme);
        }

        var fraction = _model.Fraction;
        ChartColor needleColor;
        if (_model.Segments.Count == 0)
        {
            // Without segments, the arc fills up to the value.
            var track = ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme));
            canvas.Children.Add(ChartShapes.CreateArc(center, radius, 180, 0, track, thickness, PenLineCap.Round));
            needleColor = ChartTheme.Resolve(
                _model.ColorSet is null ? ChartPalette.AccentColorName : null,
                null,
                _model.ColorSet,
                0,
                isDarkTheme);
            if (fraction > 0.001)
            {
                canvas.Children.Add(ChartShapes.CreateArc(center, radius, 180, ChartArc.GaugeAngle(fraction), ChartTheme.ToBrush(needleColor), thickness, PenLineCap.Round));
            }
        }
        else
        {
            var range = _model.Max - _model.Min;
            var shares = new double[_model.Segments.Count];
            for (var i = 0; i < shares.Length; i++)
            {
                shares[i] = Math.Max(0, _model.Segments[i].Value) / range;
            }

            var spans = ChartArc.GetSpans(shares, SegmentGap, closed: false);
            for (var i = 0; i < spans.Count; i++)
            {
                if (spans[i].End > spans[i].Start)
                {
                    canvas.Children.Add(ChartShapes.CreateArc(
                        center,
                        radius,
                        ChartArc.GaugeAngle(spans[i].Start),
                        ChartArc.GaugeAngle(spans[i].End),
                        ChartTheme.ToBrush(colors[i]),
                        thickness,
                        PenLineCap.Flat));
                }
            }

            var active = _model.GetActiveSegment();
            needleColor = active >= 0 ? colors[active] : ChartTheme.GetAccent(isDarkTheme);
        }

        // The needle is a marker on the arc where the value falls.
        if (_model.ShowNeedle)
        {
            canvas.Children.Add(ChartShapes.CreateCircle(
                ChartArc.PointAt(center, radius, ChartArc.GaugeAngle(fraction)),
                thickness + 6,
                ChartTheme.ToBrush(needleColor),
                ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: false)),
                3));
        }

        // The value and its sub label sit inside the arc.
        var valueText = new TextBlock
        {
            Text = _model.FormatValue(CultureInfo.CurrentCulture),
            FontSize = Math.Max(16, Math.Round(diameter * 0.15)),
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
            AddCenteredLabel(canvas, ChartValueFormatter.FormatCompact(_model.Min, CultureInfo.CurrentCulture), center.X - radius, top, secondary);
            AddCenteredLabel(canvas, ChartValueFormatter.FormatCompact(_model.Max, CultureInfo.CurrentCulture), center.X + radius, top, secondary);
        }

        root.Children.Add(canvas);

        if (_model.ShowLegend && _model.Segments.Count > 0)
        {
            var legend = new WrapPanel { HorizontalSpacing = 12, VerticalSpacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            for (var i = 0; i < _model.Segments.Count; i++)
            {
                legend.Children.Add(ChartShapes.CreateLegendEntry(colors[i], _model.Segments[i].Label ?? string.Empty, null));
            }

            root.Children.Add(legend);
        }

        Content = root;
        AutomationProperties.SetName(this, FirstNonEmpty(_model.Title, _model.SubLabel) ?? RS_.GetString("AdaptiveChart_Gauge"));
        AutomationProperties.SetHelpText(this, valueText.Text);
    }

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
