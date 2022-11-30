// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

public struct Point
{
    public Point()
    {
        X = 0;
        Y = 0;
    }

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Point(Vanara.PInvoke.POINT point)
    {
        X = point.X;
        Y = point.Y;
    }

    public double X { get; init; }

    public double Y { get; init; }

    public static implicit operator Point(Vanara.PInvoke.POINT point) => new Point(point.X, point.Y);

    public static Point operator /(Point point, double divider)
    {
        if (divider == 0)
        {
            throw new DivideByZeroException();
        }

        return new Point(point.X / divider, point.Y / divider);
    }

    public static Point operator /(Point point, Point divider)
    {
        if (divider.X == 0 || divider.Y == 0)
        {
            throw new DivideByZeroException();
        }

        return new Point(point.X / divider.X, point.Y / divider.Y);
    }
}
