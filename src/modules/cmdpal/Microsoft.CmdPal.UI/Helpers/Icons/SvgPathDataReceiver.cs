// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.Graphics.Canvas.Geometry;

namespace Microsoft.CmdPal.UI.Helpers;

internal sealed partial class SvgPathDataReceiver : ICanvasPathReceiver
{
    private readonly StringBuilder _path = new();
    private bool _includeFigure;

    public string PathData => _path.ToString();

    public bool UseEvenOddFill { get; private set; }

    public void BeginFigure(Vector2 startPoint, CanvasFigureFill figureFill)
    {
        _includeFigure = figureFill != CanvasFigureFill.DoesNotAffectFills;
        if (_includeFigure)
        {
            _path.Append('M');
            AppendPoint(startPoint);
        }
    }

    public void AddArc(
        Vector2 endPoint,
        float radiusX,
        float radiusY,
        float rotationAngle,
        CanvasSweepDirection sweepDirection,
        CanvasArcSize arcSize)
    {
        if (!_includeFigure)
        {
            return;
        }

        _path.Append('A');
        AppendNumber(radiusX);
        _path.Append(' ');
        AppendNumber(radiusY);
        _path.Append(' ');
        AppendNumber(rotationAngle);
        _path.Append(arcSize == CanvasArcSize.Large ? " 1 " : " 0 ");
        _path.Append(sweepDirection == CanvasSweepDirection.Clockwise ? "1 " : "0 ");
        AppendPoint(endPoint);
    }

    public void AddCubicBezier(Vector2 controlPoint1, Vector2 controlPoint2, Vector2 endPoint)
    {
        if (!_includeFigure)
        {
            return;
        }

        _path.Append('C');
        AppendPoint(controlPoint1);
        _path.Append(' ');
        AppendPoint(controlPoint2);
        _path.Append(' ');
        AppendPoint(endPoint);
    }

    public void AddLine(Vector2 endPoint)
    {
        if (_includeFigure)
        {
            _path.Append('L');
            AppendPoint(endPoint);
        }
    }

    public void AddQuadraticBezier(Vector2 controlPoint, Vector2 endPoint)
    {
        if (!_includeFigure)
        {
            return;
        }

        _path.Append('Q');
        AppendPoint(controlPoint);
        _path.Append(' ');
        AppendPoint(endPoint);
    }

    public void SetFilledRegionDetermination(CanvasFilledRegionDetermination filledRegionDetermination) =>
        UseEvenOddFill = filledRegionDetermination == CanvasFilledRegionDetermination.Alternate;

    public void SetSegmentOptions(CanvasFigureSegmentOptions figureSegmentOptions)
    {
        // Segment options only affect stroking. Initials serialize filled geometry.
        _ = figureSegmentOptions;
    }

    public void EndFigure(CanvasFigureLoop figureLoop)
    {
        if (_includeFigure && figureLoop == CanvasFigureLoop.Closed)
        {
            _path.Append('Z');
        }

        _includeFigure = false;
    }

    private void AppendPoint(Vector2 point)
    {
        AppendNumber(point.X);
        _path.Append(' ');
        AppendNumber(point.Y);
    }

    private void AppendNumber(float value)
    {
        if (MathF.Abs(value) < 0.0005f)
        {
            value = 0;
        }

        _path.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }
}
