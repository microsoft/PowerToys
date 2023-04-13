// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace MouseJumpUI.Models.Drawing;

/// <summary>
/// Immutable version of a System.Drawing.Point object with some extra utility methods.
/// </summary>
public sealed class PointInfo
{
    public PointInfo(decimal x, decimal y)
    {
        this.X = x;
        this.Y = y;
    }

    public PointInfo(Point point)
        : this(point.X, point.Y)
    {
    }

    public decimal X
    {
        get;
    }

    public decimal Y
    {
        get;
    }

    public SizeInfo ToSize()
    {
        return new((int)this.X, (int)this.Y);
    }

    public PointInfo Scale(decimal scalingFactor) => new(this.X * scalingFactor, this.Y * scalingFactor);

    public PointInfo Offset(PointInfo amount) => new(this.X + amount.X, this.Y + amount.Y);

    public Point ToPoint() => new((int)this.X, (int)this.Y);

    public override string ToString()
    {
        return "{" +
            $"{nameof(this.X)}={this.X}," +
            $"{nameof(this.Y)}={this.Y}" +
            "}";
    }
}
