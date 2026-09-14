// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ScreenTranslator.Core.Translation;

public readonly record struct PhysicalRect(double X, double Y, double Width, double Height)
{
    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static PhysicalRect Empty => new(0, 0, 0, 0);

    public bool Contains(PhysicalPoint point)
    {
        return point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;
    }

    public PhysicalRect Union(PhysicalRect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        double minX = Math.Min(Left, other.Left);
        double minY = Math.Min(Top, other.Top);
        double maxX = Math.Max(Right, other.Right);
        double maxY = Math.Max(Bottom, other.Bottom);

        return new PhysicalRect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }
}
