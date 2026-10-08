// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>Renders an Adaptive Cards <c>Chart.Donut</c> or <c>Chart.Pie</c> element with a legend.</summary>
internal sealed partial class DonutChartControl : AdaptiveVisualControl
{
    private const double MaximumDiameter = 160;
    private const double MinimumDiameter = 88;
    private const double RingRatio = 0.2;
    private const double SliceGap = 0.006;
    private const double SideBySideMinimumWidth = 300;

    private DonutChartModel _model;

    public DonutChartControl(DonutChartModel model)
    {
        _model = model;
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((DonutChartControl)candidate)._model;

    protected override void RenderCore()
    {
        var isDarkTheme = IsDarkTheme;
        var secondary = ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: true));
        var shares = _model.GetShares();
        var colors = new ChartColor[_model.Data.Count];
        for (var i = 0; i < colors.Length; i++)
        {
            colors[i] = ChartTheme.Resolve(_model.Data[i].Color, null, _model.ColorSet, i, isDarkTheme);
        }

        var sideBySide = _model.ShowLegend && LayoutWidth >= SideBySideMinimumWidth;
        var diameter = Math.Clamp(sideBySide ? LayoutWidth * 0.32 : LayoutWidth * 0.5, MinimumDiameter, MaximumDiameter);
        var chart = CreateChart(diameter, shares, colors, isDarkTheme);

        var root = new StackPanel { Spacing = 12 };
        if (_model.ShowTitle && !string.IsNullOrWhiteSpace(_model.Title))
        {
            root.Children.Add(ChartShapes.CreateText(_model.Title, ChartShapes.BodyStrongStyle));
        }

        if (!_model.ShowLegend)
        {
            chart.HorizontalAlignment = HorizontalAlignment.Center;
            root.Children.Add(chart);
        }
        else if (sideBySide)
        {
            var row = new Grid { ColumnSpacing = 24 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var legend = CreateLegend(shares, colors, secondary);
            legend.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(legend, 1);
            row.Children.Add(chart);
            row.Children.Add(legend);
            root.Children.Add(row);
        }
        else
        {
            chart.HorizontalAlignment = HorizontalAlignment.Center;
            root.Children.Add(chart);
            root.Children.Add(CreateLegend(shares, colors, secondary));
        }

        Content = root;
        var name = !string.IsNullOrWhiteSpace(_model.Title)
            ? _model.Title
            : RS_.GetString(_model.IsPie ? "AdaptiveChart_PieChart" : "AdaptiveChart_DonutChart");
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetHelpText(this, CreateSummary(shares));
    }

    private Canvas CreateChart(double diameter, IReadOnlyList<double> shares, IReadOnlyList<ChartColor> colors, bool isDarkTheme)
    {
        var canvas = new Canvas { Width = diameter, Height = diameter };
        var center = new ChartPoint(diameter / 2, diameter / 2);
        var hasData = _model.Total > 0;
        if (_model.IsPie)
        {
            if (!hasData)
            {
                canvas.Children.Add(ChartShapes.CreateCircle(center, diameter, ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme))));
                return canvas;
            }

            var spans = ChartArc.GetSpans(shares, 0);
            for (var i = 0; i < spans.Count; i++)
            {
                var fill = ChartTheme.ToBrush(colors[i]);
                if (shares[i] >= 0.9999)
                {
                    canvas.Children.Add(ChartShapes.CreateCircle(center, diameter, fill));
                }
                else if (spans[i].End > spans[i].Start)
                {
                    canvas.Children.Add(ChartShapes.CreateWedge(center, diameter / 2, ChartArc.DonutAngle(spans[i].Start), ChartArc.DonutAngle(spans[i].End), fill));
                }
            }

            return canvas;
        }

        var thickness = Math.Round(diameter * RingRatio);
        var radius = (diameter - thickness) / 2;

        // An ellipse strokes inside its bounds, while the arcs stroke centered on the radius, so
        // full rings use the outer diameter to line up with the slices.
        canvas.Children.Add(ChartShapes.CreateCircle(center, diameter, null, ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme)), thickness));
        if (hasData)
        {
            var spans = ChartArc.GetSpans(shares, SliceGap);
            for (var i = 0; i < spans.Count; i++)
            {
                var stroke = ChartTheme.ToBrush(colors[i]);
                if (shares[i] >= 0.9999)
                {
                    canvas.Children.Add(ChartShapes.CreateCircle(center, diameter, null, stroke, thickness));
                }
                else if (spans[i].End > spans[i].Start)
                {
                    canvas.Children.Add(ChartShapes.CreateArc(center, radius, ChartArc.DonutAngle(spans[i].Start), ChartArc.DonutAngle(spans[i].End), stroke, thickness, PenLineCap.Flat));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_model.CenterLabel))
        {
            var label = new TextBlock
            {
                Text = _model.CenterLabel,
                FontSize = Math.Max(14, Math.Round(diameter * 0.15)),
                FontWeight = FontWeights.SemiBold,
            };
            var size = ChartShapes.Measure(label);
            ChartShapes.Place(label, center.X - (size.Width / 2), center.Y - (size.Height / 2));
            canvas.Children.Add(label);
        }

        return canvas;
    }

    private Grid CreateLegend(IReadOnlyList<double> shares, IReadOnlyList<ChartColor> colors, Brush secondary)
    {
        var legend = new Grid { ColumnSpacing = 8, RowSpacing = 6 };
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        legend.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (var i = 0; i < _model.Data.Count; i++)
        {
            legend.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var dot = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = ChartTheme.ToBrush(colors[i]),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var label = ChartShapes.CreateText(_model.Data[i].Label ?? string.Empty, ChartShapes.BodyStyle, secondary);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            var share = ChartShapes.CreateText(
                string.Format(CultureInfo.CurrentCulture, "{0:0}%", shares[i] * 100),
                ChartShapes.BodyStrongStyle);
            share.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetRow(dot, i);
            Grid.SetRow(label, i);
            Grid.SetRow(share, i);
            Grid.SetColumn(label, 1);
            Grid.SetColumn(share, 2);
            legend.Children.Add(dot);
            legend.Children.Add(label);
            legend.Children.Add(share);
        }

        return legend;
    }

    private string CreateSummary(IReadOnlyList<double> shares)
    {
        var summary = new StringBuilder();
        for (var i = 0; i < _model.Data.Count; i++)
        {
            if (summary.Length > 0)
            {
                summary.Append(", ");
            }

            summary.Append(_model.Data[i].Label)
                .Append(' ')
                .Append(string.Format(CultureInfo.CurrentCulture, "{0:0}%", shares[i] * 100));
        }

        return summary.ToString();
    }
}
