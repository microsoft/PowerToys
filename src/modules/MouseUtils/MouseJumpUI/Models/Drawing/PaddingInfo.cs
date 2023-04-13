// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;

namespace MouseJumpUI.Models.Drawing;

/// <summary>
/// Immutable version of a System.Windows.Forms.Padding object with some extra utility methods.
/// </summary>
public sealed class PaddingInfo
{
    public PaddingInfo(decimal all)
        : this(all, all, all, all)
    {
    }

    public PaddingInfo(decimal left, decimal top, decimal right, decimal bottom)
    {
        this.Left = left;
        this.Top = top;
        this.Right = right;
        this.Bottom = bottom;
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

    public decimal Horizontal => this.Left + this.Right;

    public decimal Vertical => this.Top + this.Bottom;

    public override string ToString()
    {
        return "{" +
            $"{nameof(this.Left)}={this.Left}," +
            $"{nameof(this.Top)}={this.Top}," +
            $"{nameof(this.Right)}={this.Right}," +
            $"{nameof(this.Bottom)}={this.Bottom}" +
            "}";
    }
}
