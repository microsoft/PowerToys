// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Core;

public struct Rect
{
    public Rect()
    {
        X = 0;
        Y = 0;
        Width = 0;
        Height = 0;
    }

    public Rect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rect(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Rect(Point coord, Size size)
    {
        X = coord.X;
        Y = coord.Y;
        Width = size.Width;
        Height = size.Height;
    }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public static Rect operator /(Rect rect, double divider)
    {
        if (divider == 0)
        {
            throw new DivideByZeroException();
        }

        return new Rect(rect.X / divider, rect.Y / divider, rect.Width / divider, rect.Height / divider);
    }

    public static Rect operator /(Rect rect, Rect divider)
    {
        if (divider.X == 0 || divider.Y == 0)
        {
            throw new DivideByZeroException();
        }

        return new Rect(rect.X / divider.X, rect.Y / divider.Y, rect.Width / divider.Width, rect.Height / divider.Height);
    }
}
