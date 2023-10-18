// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace MouseJumpUI.Models.Drawing;

/// <summary>
/// Immutable version of a System.Drawing.Size object with some extra utility methods.
/// </summary>
public sealed class SizeInfo
{
    public SizeInfo(decimal width, decimal height)
    {
        this.Width = width;
        this.Height = height;
    }

    public SizeInfo(Size size)
        : this(size.Width, size.Height)
    {
    }

    public decimal Width
    {
        get;
    }

    public decimal Height
    {
        get;
    }

    public SizeInfo Negate() => new(-this.Width, -this.Height);

    public SizeInfo Shrink(PaddingInfo padding) => new(this.Width - padding.Horizontal, this.Height - padding.Vertical);

    public SizeInfo Intersect(SizeInfo size) => new(
        Math.Min(this.Width, size.Width),
        Math.Min(this.Height, size.Height));

    public RectangleInfo PlaceAt(decimal x, decimal y) => new(x, y, this.Width, this.Height);

    public SizeInfo ScaleToFit(SizeInfo bounds)
    {
        var widthRatio = bounds.Width / this.Width;
        var heightRatio = bounds.Height / this.Height;
        return widthRatio.CompareTo(heightRatio) switch
        {
            < 0 => new(bounds.Width, this.Height * widthRatio),
            0 => bounds,
            > 0 => new(this.Width * heightRatio, bounds.Height),
        };
    }

    /// <summary>
    /// Get the scaling ratio to scale obj by so that it fits inside the specified bounds
    /// without distorting the aspect ratio.
    /// </summary>
    public decimal ScaleToFitRatio(SizeInfo bounds)
    {
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            throw new ArgumentException($"{nameof(bounds.Width)} or {nameof(bounds.Height)} cannot be zero", nameof(bounds));
        }

        var widthRatio = bounds.Width / this.Width;
        var heightRatio = bounds.Height / this.Height;
        var scalingRatio = Math.Min(widthRatio, heightRatio);

        return scalingRatio;
    }

    public Size ToSize() => new((int)this.Width, (int)this.Height);

    public Point ToPoint() => new((int)this.Width, (int)this.Height);

    public override string ToString()
    {
        return "{" +
            $"{nameof(this.Width)}={this.Width}," +
            $"{nameof(this.Height)}={this.Height}" +
            "}";
    }
}
