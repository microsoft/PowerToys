// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace MouseJumpUI.Common.Models.Styles;

/// <summary>
/// Represents the border style for a drawing object.
/// </summary>
public sealed class BorderStyle
{
    public static readonly BorderStyle Empty = new(Color.Transparent, 0, 0);

    public BorderStyle(Color color, decimal all, decimal depth)
        : this(color, all, all, all, all, depth)
    {
    }

    public BorderStyle(Color color, decimal left, decimal top, decimal right, decimal bottom, decimal depth)
    {
        this.Color = color;
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
        this.Depth = depth;
    }

    public Color Color
    {
        get;
    }

    public decimal Left
    {
        get;
    }

    public decimal Top
    {
        get;
    }

    public decimal Right
    {
        get;
    }

    public decimal Bottom
    {
        get;
    }

    /// <summary>
    /// Gets the "depth" of the 3d highlight and shadow effect on the border.
    /// </summary>
    public decimal Depth
    {
        get;
    }

    public decimal Horizontal => this.Left + this.Right;

    public decimal Vertical => this.Top + this.Bottom;

    public override string ToString()
    {
        return "{" +
           $"{nameof(this.Color)}={this.Color}," +
           $"{nameof(this.Left)}={this.Left}," +
           $"{nameof(this.Top)}={this.Top}," +
           $"{nameof(this.Right)}={this.Right}," +
           $"{nameof(this.Bottom)}={this.Bottom}," +
           $"{nameof(this.Depth)}={this.Depth}" +
           "}";
    }
}
