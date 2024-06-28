// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace MouseJumpUI.Common.Models.Styles;

/// <summary>
/// Represents the styles to apply to a simple box-layout based drawing object.
/// </summary>
public sealed class BoxStyle
{
    /*

    see https://www.w3schools.com/css/css_boxmodel.asp

    +--------------[bounds]---------------+
    |▒▒▒▒▒▒▒▒▒▒▒▒▒▒[margin]▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒|
    |▒▒▓▓▓▓▓▓▓▓▓▓▓▓[border]▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒|
    |▒▒▓▓░░░░░░░░░░[padding]░░░░░░░░░░▓▓▒▒|
    |▒▒▓▓░░                         ░░▓▓▒▒|
    |▒▒▓▓░░                         ░░▓▓▒▒|
    |▒▒▓▓░░        [content]        ░░▓▓▒▒|
    |▒▒▓▓░░                         ░░▓▓▒▒|
    |▒▒▓▓░░                         ░░▓▓▒▒|
    |▒▒▓▓░░░░░░░░░░░░░░░░░░░░░░░░░░░░░▓▓▒▒|
    |▒▒▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▒▒|
    |▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒▒|
    +-------------------------------------+

    */

    public static readonly BoxStyle Empty = new(MarginStyle.Empty, BorderStyle.Empty, PaddingStyle.Empty, BackgroundStyle.Empty);

    public BoxStyle(
        MarginStyle marginStyle,
        BorderStyle borderStyle,
        PaddingStyle paddingStyle,
        BackgroundStyle backgroundStyle)
    {
        this.MarginStyle = marginStyle ?? throw new ArgumentNullException(nameof(marginStyle));
        this.BorderStyle = borderStyle ?? throw new ArgumentNullException(nameof(borderStyle));
        this.PaddingStyle = paddingStyle ?? throw new ArgumentNullException(nameof(paddingStyle));
        this.BackgroundStyle = backgroundStyle ?? throw new ArgumentNullException(nameof(backgroundStyle));
    }

    /// <summary>
    /// Gets the margin style for this layout box.
    /// </summary>
    public MarginStyle MarginStyle
    {
        get;
    }

    /// <summary>
    /// Gets the border style for this layout box.
    /// </summary>
    public BorderStyle BorderStyle
    {
        get;
    }

    /// <summary>
    /// Gets the padding style for this layout box.
    /// </summary>
    public PaddingStyle PaddingStyle
    {
        get;
    }

    /// <summary>
    /// Gets the background fill style for the content area of this layout box.
    /// </summary>
    public BackgroundStyle BackgroundStyle
    {
        get;
    }
}
