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
    public decimal Left =>
        this.X;

    [JsonIgnore]
    public decimal Top =>
        this.Y;

    [JsonIgnore]
    public decimal Right =>
        this.X + this.Width;

    [JsonIgnore]
    public decimal Bottom =>
        this.Y + this.Height;

    [JsonIgnore]
    public decimal Area =>
        this.Width * this.Height;

    [JsonIgnore]
    public PointInfo Location =>
        new(this.X, this.Y);

    [JsonIgnore]
    public PointInfo Midpoint =>
        new(
            x: this.X + (this.Width / 2),
            y: this.Y + (this.Height / 2));

    [JsonIgnore]
    public SizeInfo Size => new(this.Width, this.Height);

    /// <summary>
    /// Centers the rectangle around a specified point.
    /// </summary>
    /// <param name="point">The <see cref="PointInfo"/> around which the rectangle will be centered.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is centered around the specified point.</returns>
    public RectangleInfo Center(PointInfo point) =>
        new(
            x: point.X - (this.Width / 2),
            y: point.Y - (this.Height / 2),
            width: this.Width,
            height: this.Height);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is moved within the bounds of the specified outer rectangle.
    /// If the current rectangle is larger than the outer rectangle, an exception is thrown.
    /// </summary>
    /// <param name="outer">The outer <see cref="RectangleInfo"/> within which to confine this rectangle.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is the result of moving this rectangle within the bounds of the outer rectangle.</returns>
    /// <exception cref="ArgumentException">Thrown when the current rectangle is larger than the outer rectangle.</exception>
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

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is larger than the current rectangle.
    /// The dimensions of the new rectangle are calculated by enlarging the current rectangle's dimensions by the size of the border.
    /// </summary>
    /// <param name="border">The <see cref="BorderStyle"/> that specifies the amount to enlarge the rectangle on each side.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is larger than the current rectangle by the specified border amounts.</returns>
    public RectangleInfo Enlarge(BorderStyle border) =>
        new(
            this.X - border.Left,
            this.Y - border.Top,
            this.Width + border.Horizontal,
            this.Height + border.Vertical);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is larger than the current rectangle.
    /// The dimensions of the new rectangle are calculated by enlarging the current rectangle's dimensions by the size of the margin.
    /// </summary>
    /// <param name="margin">The <see cref="MarginStyle"/> that specifies the amount to enlarge the rectangle on each side.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is larger than the current rectangle by the specified margin amounts.</returns>
    public RectangleInfo Enlarge(MarginStyle margin) =>
        new(
            this.X - margin.Left,
            this.Y - margin.Top,
            this.Width + margin.Horizontal,
            this.Height + margin.Vertical);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is larger than the current rectangle.
    /// The dimensions of the new rectangle are calculated by enlarging the current rectangle's dimensions by the size of the padding.
    /// </summary>
    /// <param name="padding">The <see cref="PaddingStyle"/> that specifies the amount to enlarge the rectangle on each side.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is larger than the current rectangle by the specified padding amounts.</returns>
    public RectangleInfo Enlarge(PaddingStyle padding) =>
        new(
            this.X - padding.Left,
            this.Y - padding.Top,
            this.Width + padding.Horizontal,
            this.Height + padding.Vertical);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is offset by the specified amount.
    /// </summary>
    /// <param name="amount">The <see cref="SizeInfo"/> representing the amount to offset in both the X and Y directions.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is offset by the specified amount.</returns>
    public RectangleInfo Offset(SizeInfo amount) =>
        this.Offset(amount.Width, amount.Height);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is offset by the specified X and Y distances.
    /// </summary>
    /// <param name="dx">The distance to offset the rectangle along the X-axis.</param>
    /// <param name="dy">The distance to offset the rectangle along the Y-axis.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is offset by the specified X and Y distances.</returns>
    public RectangleInfo Offset(decimal dx, decimal dy) =>
        new(this.X + dx, this.Y + dy, this.Width, this.Height);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is a scaled version of the current rectangle.
    /// The dimensions of the new rectangle are calculated by multiplying the current rectangle's dimensions by the scaling factor.
    /// </summary>
    /// <param name="scalingFactor">The factor by which to scale the rectangle's dimensions.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is a scaled version of the current rectangle.</returns>
    public RectangleInfo Scale(decimal scalingFactor) =>
        new(
            this.X * scalingFactor,
            this.Y * scalingFactor,
            this.Width * scalingFactor,
            this.Height * scalingFactor);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is smaller than the current rectangle.
    /// The dimensions of the new rectangle are calculated by shrinking the current rectangle's dimensions by the size of the border.
    /// </summary>
    /// <param name="border">The <see cref="BorderStyle"/> that specifies the amount to shrink the rectangle on each side.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is smaller than the current rectangle by the specified border amounts.</returns>
    public RectangleInfo Shrink(BorderStyle border) =>
        new(
            this.X + border.Left,
            this.Y + border.Top,
            this.Width - border.Horizontal,
            this.Height - border.Vertical);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is smaller than the current rectangle.
    /// The dimensions of the new rectangle are calculated by shrinking the current rectangle's dimensions by the size of the margin.
    /// </summary>
    /// <param name="margin">The <see cref="MarginStyle"/> that specifies the amount to shrink the rectangle on each side.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is smaller than the current rectangle by the specified margin amounts.</returns>
    public RectangleInfo Shrink(MarginStyle margin) =>
        new(
            this.X + margin.Left,
            this.Y + margin.Top,
            this.Width - margin.Horizontal,
            this.Height - margin.Vertical);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> that is smaller than the current rectangle.
    /// The dimensions of the new rectangle are calculated by shrinking the current rectangle's dimensions by the size of the padding.
    /// </summary>
    /// <param name="padding">The <see cref="PaddingStyle"/> that specifies the amount to shrink the rectangle on each side.</param>
    /// <returns>A new <see cref="RectangleInfo"/> that is smaller than the current rectangle by the specified padding amounts.</returns>
    public RectangleInfo Shrink(PaddingStyle padding) =>
        new(
            this.X + padding.Left,
            this.Y + padding.Top,
            this.Width - padding.Horizontal,
            this.Height - padding.Vertical);

    /// <summary>
    /// Returns a new <see cref="RectangleInfo"/> where the X, Y, Width, and Height properties of the current rectangle are truncated to integers.
    /// </summary>
    /// <returns>A new <see cref="RectangleInfo"/> with the X, Y, Width, and Height properties of the current rectangle truncated to integers.</returns>
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

    public Rectangle ToRectangle() =>
        new(
            (int)this.X,
            (int)this.Y,
            (int)this.Width,
            (int)this.Height);

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
