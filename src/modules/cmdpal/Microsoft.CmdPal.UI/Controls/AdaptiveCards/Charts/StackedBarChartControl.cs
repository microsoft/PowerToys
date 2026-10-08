// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;

/// <summary>
/// Renders an Adaptive Cards <c>Chart.HorizontalBar.Stacked</c> element: one rounded bar per
/// group, with segments in a shared legend color order.
/// </summary>
internal sealed partial class StackedBarChartControl : AdaptiveVisualControl
{
    private const double BarHeight = 12;
    private const double BarCornerRadius = 6;

    private StackedBarChartModel _model;

    public StackedBarChartControl(StackedBarChartModel model)
    {
        _model = model;
        Render();
    }

    public override string IncrementalState => _model.IncrementalState;

    // Star-sized grid columns follow the width, so resizing needs no rebuild.
    protected override bool WidthAffectsLayout => false;

    protected override void ApplyModel(AdaptiveVisualControl candidate) => _model = ((StackedBarChartControl)candidate)._model;

    protected override void RenderCore()
    {
        var isDarkTheme = IsDarkTheme;
        var secondary = ChartTheme.ToBrush(ChartTheme.GetTextColor(isDarkTheme, secondary: true));
        var track = ChartTheme.ToBrush(ChartTheme.GetTrackColor(isDarkTheme));
        var legend = _model.GetLegend();
        var colors = new Dictionary<string, ChartColor>(StringComparer.Ordinal);
        foreach (var entry in legend)
        {
            colors[entry.Legend] = ChartTheme.Resolve(entry.Color, _model.Color, _model.ColorSet, entry.ColorIndex, isDarkTheme);
        }

        var root = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrWhiteSpace(_model.Title))
        {
            root.Children.Add(ChartShapes.CreateText(_model.Title, ChartShapes.BodyStrongStyle));
        }

        var maxTotal = _model.MaxTotal;
        foreach (var group in _model.Groups)
        {
            if (!string.IsNullOrWhiteSpace(group.Title))
            {
                root.Children.Add(ChartShapes.CreateText(group.Title, ChartShapes.CaptionStyle, secondary));
            }

            root.Children.Add(CreateBar(group, maxTotal, colors, track));
        }

        if (_model.ShowLegend && legend.Count > 0)
        {
            var legendPanel = new WrapPanel { HorizontalSpacing = 16, VerticalSpacing = 4 };
            foreach (var entry in legend)
            {
                legendPanel.Children.Add(ChartShapes.CreateLegendEntry(colors[entry.Legend], entry.Legend, null));
            }

            root.Children.Add(legendPanel);
        }

        Content = root;
        AutomationProperties.SetName(this, !string.IsNullOrWhiteSpace(_model.Title) ? _model.Title : RS_.GetString("AdaptiveChart_BarChart"));
        AutomationProperties.SetHelpText(this, CreateSummary());
    }

    private static Grid CreateBar(StackedBarGroup group, double maxTotal, Dictionary<string, ChartColor> colors, Microsoft.UI.Xaml.Media.Brush track)
    {
        var bar = new Grid { Height = BarHeight, ColumnSpacing = 2 };
        var cells = new List<Border>();
        foreach (var point in group.Data)
        {
            if (point.Value <= 0)
            {
                continue;
            }

            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(point.Value, GridUnitType.Star) });
            var cell = new Border { Background = ChartTheme.ToBrush(colors[point.Label ?? string.Empty]) };
            ToolTipService.SetToolTip(
                cell,
                $"{point.Label}: {ChartValueFormatter.FormatCompact(point.Value, CultureInfo.CurrentCulture)}");
            cells.Add(cell);
        }

        // The remainder keeps bars comparable when the groups have different totals.
        var remainder = maxTotal - group.Total;
        if (remainder > 0 || cells.Count == 0)
        {
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cells.Count == 0 ? 1 : remainder, GridUnitType.Star) });
            cells.Add(new Border { Background = track });
        }

        for (var i = 0; i < cells.Count; i++)
        {
            var left = i == 0 ? BarCornerRadius : 0;
            var right = i == cells.Count - 1 ? BarCornerRadius : 0;
            cells[i].CornerRadius = new Microsoft.UI.Xaml.CornerRadius(left, right, right, left);
            Grid.SetColumn(cells[i], i);
            bar.Children.Add(cells[i]);
        }

        return bar;
    }

    private string CreateSummary()
    {
        var summary = new StringBuilder();
        foreach (var group in _model.Groups)
        {
            if (summary.Length > 0)
            {
                summary.Append(". ");
            }

            if (!string.IsNullOrWhiteSpace(group.Title))
            {
                summary.Append(group.Title).Append(": ");
            }

            for (var i = 0; i < group.Data.Count; i++)
            {
                if (i > 0)
                {
                    summary.Append(", ");
                }

                summary.Append(group.Data[i].Label)
                    .Append(' ')
                    .Append(ChartValueFormatter.FormatCompact(group.Data[i].Value, CultureInfo.CurrentCulture));
            }
        }

        return summary.ToString();
    }
}
