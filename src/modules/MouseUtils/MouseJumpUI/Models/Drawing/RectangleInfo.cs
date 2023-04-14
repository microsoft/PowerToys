// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace MouseJumpUI.Models.Drawing;

/// <summary>
/// Immutable version of a System.Drawing.Rectangle object with some extra utility methods.
/// </summary>
public sealed class RectangleInfo
{
    public RectangleInfo(decimal x, decimal y, decimal width, decimal height)
    {
        this.X = x;
        this.Y = y;
        this.Width = width;
        this.Height = height;
    }

    public RectangleInfo(Rectangle rectangle)
        : this(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height)
    {
    }

    public RectangleInfo(Point location, SizeInfo size)
        : this(location.X, location.Y, size.Width, size.Height)
    {
    }

    public RectangleInfo(SizeInfo size)
        : this(0, 0, size.Width, size.Height)
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

    public decimal Width
    {
        get;
    }

    public decimal Height
    {
        get;
    }

    public decimal Left => this.X;

    public decimal Top => this.Y;

    public decimal Right => this.X + this.Width;

    public decimal Bottom => this.Y + this.Height;

    public SizeInfo Size => new(this.Width, this.Height);

    public PointInfo Location => new(this.X, this.Y);

    public decimal Area => this.Width * this.Height;

    /// <remarks>
    /// Adapted from https://github.com/dotnet/runtime
    /// See https://github.com/dotnet/runtime/blob/dfd618dc648ba9b11dd0f8034f78113d69f223cd/src/libraries/System.Drawing.Primitives/src/System/Drawing/Rectangle.cs
    /// </remarks>
    public bool Contains(RectangleInfo rect) =>
        (this.X <= rect.X) && (rect.X + rect.Width <= this.X + this.Width) &&
        (this.Y <= rect.Y) && (rect.Y + rect.Height <= this.Y + this.Height);

    public RectangleInfo Enlarge(PaddingInfo padding) => new(
        this.X + padding.Left,
        this.Y + padding.Top,
        this.Width + padding.Horizontal,
        this.Height + padding.Vertical);

    public RectangleInfo Offset(SizeInfo amount) => this.Offset(amount.Width, amount.Height);

    public RectangleInfo Offset(decimal dx, decimal dy) => new(this.X + dx, this.Y + dy, this.Width, this.Height);

    public RectangleInfo Scale(decimal scalingFactor) => new(
        this.X * scalingFactor,
        this.Y * scalingFactor,
        this.Width * scalingFactor,
        this.Height * scalingFactor);

    public RectangleInfo Center(PointInfo point) => new(
        x: point.X - (this.Width / 2),
        y: point.Y - (this.Height / 2),
        width: this.Width,
        height: this.Height);

    public PointInfo Midpoint => new(
        x: this.X + (this.Width / 2),
        y: this.Y + (this.Height / 2));

    public RectangleInfo Clamp(RectangleInfo outer)
    {
        if ((this.Width > outer.Width) || (this.Height > outer.Height))
        {
            throw new ArgumentException($"Value cannot be larger than {nameof(outer)}.");
        }

        return new(
            x: Math.Clamp(this.X, outer.X, outer.Right - this.Width),
            y: Math.Clamp(this.Y, outer.Y, outer.Bottom - this.Height),
            width: this.Width,
            height: this.Height);
    }

    public Rectangle ToRectangle() => new((int)this.X, (int)this.Y, (int)this.Width, (int)this.Height);

    public override string ToString()
    {
        return "{" +
            $"{nameof(this.Left)}={this.Left}," +
            $"{nameof(this.Top)}={this.Top}," +
            $"{nameof(this.Width)}={this.Width}," +
            $"{nameof(this.Height)}={this.Height}" +
            "}";
    }
}
