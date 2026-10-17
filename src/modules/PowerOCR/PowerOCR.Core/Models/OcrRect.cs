// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerOCR.Core.Models;

public readonly record struct OcrRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool Contains(OcrPoint point)
        => point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;

    public bool Intersects(OcrRect other)
        => X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

    public double IntersectionArea(OcrRect other)
    {
        double width = Math.Max(0, Math.Min(Right, other.Right) - Math.Max(X, other.X));
        double height = Math.Max(0, Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y));
        return width * height;
    }

    public OcrRect Union(OcrRect other)
    {
        double left = Math.Min(X, other.X);
        double top = Math.Min(Y, other.Y);
        return new(left, top, Math.Max(Right, other.Right) - left, Math.Max(Bottom, other.Bottom) - top);
    }
}
