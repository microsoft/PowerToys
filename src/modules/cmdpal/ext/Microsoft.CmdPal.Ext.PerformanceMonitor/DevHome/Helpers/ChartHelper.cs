// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace CoreWidgetProvider.Helpers;

internal sealed class ChartHelper
{
    public enum ChartType
    {
        CPU,
        GPU,
        Mem,
        Net,
    }

    public const int ChartHeight = 86;
    public const int ChartWidth = 268;

    private const string LightGrayBoxStyle = "fill:none;stroke:lightgrey;stroke-width:1";

    private const string CPULineStyle = "fill:none;stroke:rgb(57,184,227);stroke-width:1";
    private const string GPULineStyle = "fill:none;stroke:rgb(222,104,242);stroke-width:1";
    private const string MemLineStyle = "fill:none;stroke:rgb(92,158,250);stroke-width:1";
    private const string NetLineStyle = "fill:none;stroke:rgb(245,98,142);stroke-width:1";

    private const string FillStyle = "fill:url(#gradientId);stroke:transparent";

    private const string CPUBrushStop1Style = "stop-color:rgb(57,184,227);stop-opacity:0.4";
    private const string CPUBrushStop2Style = "stop-color:rgb(0,86,110);stop-opacity:0.25";

    private const string GPUBrushStop1Style = "stop-color:rgb(222,104,242);stop-opacity:0.4";
    private const string GPUBrushStop2Style = "stop-color:rgb(125,0,138);stop-opacity:0.25";

    private const string MemBrushStop1Style = "stop-color:rgb(92,158,250);stop-opacity:0.4";
    private const string MemBrushStop2Style = "stop-color:rgb(0,34,92);stop-opacity:0.25";

    private const string NetBrushStop1Style = "stop-color:rgb(245,98,142);stop-opacity:0.4";
    private const string NetBrushStop2Style = "stop-color:rgb(130,0,47);stop-opacity:0.25";

    private const string SvgElement = "svg";
    private const string RectElement = "rect";
    private const string PolylineElement = "polyline";
    private const string DefsElement = "defs";
    private const string LinearGradientElement = "linearGradient";
    private const string StopElement = "stop";

    private const string HeightAttr = "height";
    private const string WidthAttr = "width";
    private const string StyleAttr = "style";
    private const string PointsAttr = "points";
    private const string OffsetAttr = "offset";
    private const string X1Attr = "x1";
    private const string X2Attr = "x2";
    private const string Y1Attr = "y1";
    private const string Y2Attr = "y2";
    private const string IdAttr = "id";

    private const int MaxChartValues = 34;

    public static string CreateImageUrl(List<float> chartValues, ChartType type)
    {
        var chartStr = CreateChart(chartValues, type);
        return "data:image/svg+xml;utf8," + chartStr;
    }

    /// <summary>
    /// Creates an SVG image for the chart.
    /// </summary>
    /// <param name="chartValues">The values to plot on the chart</param>
    /// <param name="type">The type of chart. Each chart type uses different colors.</param>
    /// <remarks>
    /// The SVG is made of three shapes: <br/>
    /// 1. A colored line, plotting the points on the graph <br/>
    /// 2. A transparent line, outlining the gradient under the graph <br/>
    /// 3. A grey box, outlining the entire image <br/>
    /// The SVG also contains a definition for the fill gradient.
    /// </remarks>
    /// <returns>A string representing the chart as an SVG image.</returns>
    public static string CreateChart(List<float> chartValues, ChartType type)
    {
        // The SVG created by this method will look similar to this:
        /*
        <svg height="102" width="264">
            <defs>
                <linearGradient x1="0%" x2="0%" y1="0%" y2="100%" id="gradientId">
                    <stop offset="0%" style="stop-color:rgb(222,104,242);stop-opacity:0.4" />
                    <stop offset="95%" style="stop-color:rgb(125,0,138);stop-opacity:0.25" />
                </linearGradient>
            </defs>
            <polyline points="1,91 10,71 253,51 262,31 262,101 1,101" style="fill:url(#gradientId);stroke:transparent" />
            <polyline points="1,91 10,71 253,51 262,31" style="fill:none;stroke:rgb(222,104,242);stroke-width:1" />
            <rect height="102" width="264" style="fill:none;stroke:lightgrey;stroke-width:1" />
        </svg>
        */

        // The following code can be uncommented for testing when a static image is desired.
        /* chartValues.Clear();
        chartValues = new List<float>
        {
            10, 30, 20, 40, 30, 50, 40, 60, 50, 100,
            10, 30, 20, 40, 30, 50, 40, 60, 50, 70,
            0, 30, 20, 40, 30, 50, 40, 60, 50, 70,
        };*/

        var chartDoc = new XDocument();

        lock (chartValues)
        {
            var svgElement = CreateBlankSvg(ChartHeight, ChartWidth);

            // Create the line that will show the points on the graph.
            var lineElement = new XElement(PolylineElement);
            var points = TransformPointsToLine(chartValues, out var startX, out var finalX);
            lineElement.SetAttributeValue(PointsAttr, points.ToString());
            lineElement.SetAttributeValue(StyleAttr, GetLineStyle(type));

            // Create the line that will contain the gradient fill.
            TransformPointsToLoop(points, startX, finalX);
            var fillElement = new XElement(PolylineElement);
            fillElement.SetAttributeValue(PointsAttr, points.ToString());
            fillElement.SetAttributeValue(StyleAttr, FillStyle);

            // Add the gradient definition and the three shapes to the svg.
            svgElement.Add(CreateGradientDefinition(type));
            svgElement.Add(fillElement);
            svgElement.Add(lineElement);
            svgElement.Add(CreateBorderBox(ChartHeight, ChartWidth));

            chartDoc.Add(svgElement);
        }

        return chartDoc.ToString();
    }

    private static XElement CreateBlankSvg(int height, int width)
    {
        var svgElement = new XElement(SvgElement);
        svgElement.SetAttributeValue(HeightAttr, height);
        svgElement.SetAttributeValue(WidthAttr, width);
        return svgElement;
    }

    private static XElement CreateGradientDefinition(ChartType type)
    {
        var defsElement = new XElement(DefsElement);
        var gradientElement = new XElement(LinearGradientElement);

        // Vertical gradients are created when x1 and x2 are equal and y1 and y2 differ.
        gradientElement.SetAttributeValue(X1Attr, "0%");
        gradientElement.SetAttributeValue(X2Attr, "0%");
        gradientElement.SetAttributeValue(Y1Attr, "0%");
        gradientElement.SetAttributeValue(Y2Attr, "100%");
        gradientElement.SetAttributeValue(IdAttr, "gradientId");

        string stop1Style;
        string stop2Style;
        switch (type)
        {
            case ChartType.GPU:
                stop1Style = GPUBrushStop1Style;
                stop2Style = GPUBrushStop2Style;
                break;
            case ChartType.Mem:
                stop1Style = MemBrushStop1Style;
                stop2Style = MemBrushStop2Style;
                break;
            case ChartType.Net:
                stop1Style = NetBrushStop1Style;
                stop2Style = NetBrushStop2Style;
                break;
            case ChartType.CPU:
            default:
                stop1Style = CPUBrushStop1Style;
                stop2Style = CPUBrushStop2Style;
                break;
        }

        var stop1 = new XElement(StopElement);
        stop1.SetAttributeValue(OffsetAttr, "0%");
        stop1.SetAttributeValue(StyleAttr, stop1Style);

        var stop2 = new XElement(StopElement);
        stop2.SetAttributeValue(OffsetAttr, "95%");
        stop2.SetAttributeValue(StyleAttr, stop2Style);

        gradientElement.Add(stop1);
        gradientElement.Add(stop2);
        defsElement.Add(gradientElement);

        return defsElement;
    }

    private static XElement CreateBorderBox(int height, int width)
    {
        var boxElement = new XElement(RectElement);
        boxElement.SetAttributeValue(HeightAttr, height);
        boxElement.SetAttributeValue(WidthAttr, width);
        boxElement.SetAttributeValue(StyleAttr, LightGrayBoxStyle);
        return boxElement;
    }

    private static string GetLineStyle(ChartType type)
    {
        var lineStyle = type switch
        {
            ChartType.CPU => CPULineStyle,
            ChartType.GPU => GPULineStyle,
            ChartType.Mem => MemLineStyle,
            ChartType.Net => NetLineStyle,
            _ => CPULineStyle,
        };

        return lineStyle;
    }

    private static StringBuilder TransformPointsToLine(List<float> chartValues, out int startX, out int finalX)
    {
        var points = new StringBuilder();

        // The X value where the graph starts must be adjusted so that the graph is right-aligned.
        // The max available width of the widget is 268. Since there is a 1 px border around the chart, the width of the chart's line must be <=266.
        // To create a chart of exactly the right size, we'll have 34 points with 8 pixels in between:
        // 1 px left border + 1 px for first point + 33 segments * 8 px per segment + 1 px right border = 267 pixels total in width.
        const int pxBetweenPoints = 8;

        // When the chart doesn't have all points yet, move the chart over to the right by increasing the starting X coordinate.
        // For a chart with only 1 point, the svg will not render a polyline.
        // For a chart with 2 points, starting X coordinate ==  2 + (34 -  2) * 8 == 1 + 32 * 8 == 1 + 256 == 257
        // For a chart with 30 points, starting X coordinate == 2 + (34 - 34) * 8 == 1 +  0 * 8 == 1 +   0 ==   2
        startX = 2 + ((MaxChartValues - chartValues.Count) * pxBetweenPoints);
        finalX = startX;

        // Extend graph by one pixel to cover gap on the left when the chart is otherwise full.
        if (startX == 2)
        {
            var invertedHeight = 100 - chartValues[0];
            var finalY = (invertedHeight * (ChartHeight / 100.0)) - 1;
            points.Append(CultureInfo.InvariantCulture, $"1,{finalY} ");
        }

        foreach (var origY in chartValues)
        {
            // We receive the height as a number up from the X axis (bottom of the chart), but we have to invert it
            // since the Y coordinate is relative to the top of the chart.
            var invertedHeight = 100 - origY;

            // Scale the final Y to whatever the chart height is.
            var finalY = (invertedHeight * (ChartHeight / 100.0)) - 1;

            points.Append(CultureInfo.InvariantCulture, $"{finalX},{finalY} ");
            finalX += pxBetweenPoints;
        }

        // Remove the trailing space.
        if (points.Length > 0)
        {
            points.Remove(points.Length - 1, 1);
            finalX -= pxBetweenPoints;
        }

        return points;
    }

    private static void TransformPointsToLoop(StringBuilder points, int startX, int finalX)
    {
        // Close the loop.
        // Add a point at the most recent X value that corresponds with y = 0
        points.Append(CultureInfo.InvariantCulture, $" {finalX},{ChartHeight - 1}");

        // Add a point at the start of the chart that corresponds with y = 0
        points.Append(CultureInfo.InvariantCulture, $" {startX},{ChartHeight - 1}");
    }

    public static void AddNextChartValue(float value, List<float> chartValues)
    {
        if (chartValues.Count >= MaxChartValues)
        {
            chartValues.RemoveAt(0);
        }

        chartValues.Add(value);
    }
}
