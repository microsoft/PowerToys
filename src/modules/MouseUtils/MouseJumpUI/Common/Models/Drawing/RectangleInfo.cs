// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Text.Json.Serialization;
using MouseJumpUI.Common.Models.Styles;
using BorderStyle = MouseJumpUI.Common.Models.Styles.BorderStyle;

namespace MouseJumpUI.Common.Models.Drawing;

/// <summary>
/// Immutable version of a System.Drawing.Rectangle object with some extra utility methods.
/// </summary>
public sealed class RectangleInfo
{
    public static readonly RectangleInfo Empty = new(0, 0, 0, 0);

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

    [JsonIgnore]
    public decimal Left => this.X;

    [JsonIgnore]
    public decimal Top => this.Y;

    [JsonIgnore]
    public decimal Right => this.X + this.Width;

    [JsonIgnore]
    public decimal Bottom => this.Y + this.Height;

    [JsonIgnore]
    public decimal Area => this.Width * this.Height;

    [JsonIgnore]
    public PointInfo Location => new(this.X, this.Y);

    [JsonIgnore]
    public PointInfo Midpoint => new(
        x: this.X + (this.Width / 2),
        y: this.Y + (this.Height / 2));

    [JsonIgnore]
    public SizeInfo Size => new(this.Width, this.Height);

    public RectangleInfo Center(PointInfo point) => new(
        x: point.X - (this.Width / 2),
        y: point.Y - (this.Height / 2),
        width: this.Width,
        height: this.Height);

    /// <summary>
    /// Moves this RectangleInfo inside the specified RectangleInfo.
    /// </summary>
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

    /// <remarks>
    /// Adapted from https://github.com/dotnet/runtime
    /// See https://github.com/dotnet/runtime/blob/dfd618dc648ba9b11dd0f8034f78113d69f223cd/src/libraries/System.Drawing.Primitives/src/System/Drawing/Rectangle.cs
    /// </remarks>
    public bool Contains(decimal x, decimal y) =>
        this.X <= x && x < this.X + this.Width && this.Y <= y && y < this.Y + this.Height;

    /// <remarks>
    /// Adapted from https://github.com/dotnet/runtime
    /// See https://github.com/dotnet/runtime/blob/dfd618dc648ba9b11dd0f8034f78113d69f223cd/src/libraries/System.Drawing.Primitives/src/System/Drawing/Rectangle.cs
    /// </remarks>
    public bool Contains(PointInfo pt) =>
        this.Contains(pt.X, pt.Y);

    /// <remarks>
    /// Adapted from https://github.com/dotnet/runtime
    /// See https://github.com/dotnet/runtime/blob/dfd618dc648ba9b11dd0f8034f78113d69f223cd/src/libraries/System.Drawing.Primitives/src/System/Drawing/Rectangle.cs
    /// </remarks>
    public bool Contains(RectangleInfo rect) =>
        (this.X <= rect.X) && (rect.X + rect.Width <= this.X + this.Width) &&
        (this.Y <= rect.Y) && (rect.Y + rect.Height <= this.Y + this.Height);

    public RectangleInfo Enlarge(BorderStyle border) =>
        new(
            this.X - border.Left,
            this.Y - border.Top,
            this.Width + border.Horizontal,
            this.Height + border.Vertical);

    public RectangleInfo Enlarge(MarginStyle margin) =>
        new(
            this.X - margin.Left,
            this.Y - margin.Top,
            this.Width + margin.Horizontal,
            this.Height + margin.Vertical);

    public RectangleInfo Enlarge(PaddingStyle padding) =>
        new(
            this.X - padding.Left,
            this.Y - padding.Top,
            this.Width + padding.Horizontal,
            this.Height + padding.Vertical);

    public RectangleInfo Offset(SizeInfo amount) => this.Offset(amount.Width, amount.Height);

    public RectangleInfo Offset(decimal dx, decimal dy) => new(this.X + dx, this.Y + dy, this.Width, this.Height);

    public RectangleInfo Scale(decimal scalingFactor) => new(
        this.X * scalingFactor,
        this.Y * scalingFactor,
        this.Width * scalingFactor,
        this.Height * scalingFactor);

    public RectangleInfo Shrink(BorderStyle border) =>
        new(
            this.X + border.Left,
            this.Y + border.Top,
            this.Width - border.Horizontal,
            this.Height - border.Vertical);

    public RectangleInfo Shrink(MarginStyle margin) =>
        new(
            this.X + margin.Left,
            this.Y + margin.Top,
            this.Width - margin.Horizontal,
            this.Height - margin.Vertical);

    public RectangleInfo Shrink(PaddingStyle padding) =>
        new(
            this.X + padding.Left,
            this.Y + padding.Top,
            this.Width - padding.Horizontal,
            this.Height - padding.Vertical);

    public RectangleInfo Truncate() =>
        new(
            (int)this.X,
            (int)this.Y,
            (int)this.Width,
            (int)this.Height);

    /// <remarks>
    /// Adapted from https://github.com/dotnet/runtime
    /// See https://github.com/dotnet/runtime/blob/dfd618dc648ba9b11dd0f8034f78113d69f223cd/src/libraries/System.Drawing.Primitives/src/System/Drawing/Rectangle.cs
    /// </remarks>
    public RectangleInfo Union(RectangleInfo rect)
    {
        var x1 = Math.Min(this.X, rect.X);
        var x2 = Math.Max(this.X + this.Width, rect.X + rect.Width);
        var y1 = Math.Min(this.Y, rect.Y);
        var y2 = Math.Max(this.Y + this.Height, rect.Y + rect.Height);

        return new RectangleInfo(x1, y1, x2 - x1, y2 - y1);
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
