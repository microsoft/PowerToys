// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace MouseJumpUI.Models.Styles;

/// <summary>
/// Represents the background fill style for a drawing object.
/// </summary>
public sealed class BackgroundStyle
{
    public static readonly BackgroundStyle Empty = new(SystemColors.Control, SystemColors.Control);

    public BackgroundStyle(
        Color color1,
        Color color2)
    {
        this.Color1 = color1;
        this.Color2 = color2;
    }

    public Color Color1
    {
        get;
    }

    public Color Color2
    {
        get;
    }

    public override string ToString()
    {
        return "{" +
           $"{nameof(this.Color1)}={this.Color1}," +
           $"{nameof(this.Color2)}={this.Color2}" +
           "}";
    }
}
