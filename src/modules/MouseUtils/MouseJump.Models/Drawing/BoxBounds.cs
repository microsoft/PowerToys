// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Styles;

namespace MouseJump.Models.Drawing;

/// <summary>
/// Represents the bounds of a layout box, including outer, margin, border, padding,
/// and content bounds. This class is used to model the box layout as described
/// in the CSS box model.
/// </summary>
public sealed class BoxBounds
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

    public static readonly BoxBounds Empty = new(
        outerBounds: RectangleInfo.Empty,
        marginBounds: RectangleInfo.Empty,
        borderBounds: RectangleInfo.Empty,
        paddingBounds: RectangleInfo.Empty,
        contentBounds: RectangleInfo.Empty,
        isEmpty: true);

    public BoxBounds(
        RectangleInfo outerBounds,
        RectangleInfo marginBounds,
        RectangleInfo borderBounds,
        RectangleInfo paddingBounds,
        RectangleInfo contentBounds)
        : this(
            outerBounds,
            marginBounds,
            borderBounds,
            paddingBounds,
            contentBounds,
            isEmpty: false)
    {
    }

    private BoxBounds(
        RectangleInfo outerBounds,
        RectangleInfo marginBounds,
        RectangleInfo borderBounds,
        RectangleInfo paddingBounds,
        RectangleInfo contentBounds,
        bool isEmpty)
    {
        this.OuterBounds = outerBounds ?? throw new ArgumentNullException(nameof(outerBounds));
        this.MarginBounds = marginBounds ?? throw new ArgumentNullException(nameof(marginBounds));
        this.BorderBounds = borderBounds ?? throw new ArgumentNullException(nameof(borderBounds));
        this.PaddingBounds = paddingBounds ?? throw new ArgumentNullException(nameof(paddingBounds));
        this.ContentBounds = contentBounds ?? throw new ArgumentNullException(nameof(contentBounds));
        this.IsEmpty = isEmpty;
    }

    /// <summary>
    /// Gets the outer bounds of this layout box.
    /// </summary>
    public RectangleInfo OuterBounds
    {
        get;
    }

    public RectangleInfo MarginBounds
    {
        get;
    }

    public RectangleInfo BorderBounds
    {
        get;
    }

    public RectangleInfo PaddingBounds
    {
        get;
    }

    /// <summary>
    /// Gets the bounds of the content area for this layout box.
    /// </summary>
    public RectangleInfo ContentBounds
    {
        get;
    }

    public bool IsEmpty
    {
        get;
    }

    /// <summary>
    /// Calculates the bounds of the various areas of a box, given the content bounds and the box style.
    /// Starts with the content bounds and works outward, enlarging the content bounds by the padding, border, and margin sizes to calculate the outer bounds of the box.
    /// </summary>
    /// <param name="contentBounds">The content bounds of the box.</param>
    /// <param name="boxStyle">The style of the box, which includes the sizes of the margin, border, and padding areas.</param>
    /// <returns>A <see cref="BoxBounds"/> object that represents the bounds of the different areas of the box.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contentBounds"/> or <paramref name="boxStyle"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the styles in <paramref name="boxStyle"/> is null.</exception>
    public static BoxBounds CreateFromContentBounds(
        RectangleInfo contentBounds,
        BoxStyle boxStyle)
    {
        ArgumentNullException.ThrowIfNull(contentBounds);
        ArgumentNullException.ThrowIfNull(boxStyle);
        if (boxStyle.PaddingStyle == null || boxStyle.BorderStyle == null || boxStyle.MarginStyle == null)
        {
            throw new ArgumentException(null, nameof(boxStyle));
        }

        var paddingBounds = contentBounds.Enlarge(boxStyle.PaddingStyle);
        var borderBounds = paddingBounds.Enlarge(boxStyle.BorderStyle);
        var marginBounds = borderBounds.Enlarge(boxStyle.MarginStyle);
        var outerBounds = marginBounds;
        return new(
            outerBounds, marginBounds, borderBounds, paddingBounds, contentBounds);
    }

    /// <summary>
    /// Calculates the bounds of the various areas of a box, given the outer bounds and the box style.
    /// This method starts with the outer bounds and works inward, shrinking the outer bounds by the margin, border, and padding sizes to calculate the content bounds of the box.
    /// </summary>
    /// <param name="outerBounds">The outer bounds of the box.</param>
    /// <param name="boxStyle">The style of the box, which includes the sizes of the margin, border, and padding areas.</param>
    /// <returns>A <see cref="BoxBounds"/> object that represents the bounds of the different areas of the box.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="outerBounds"/> or <paramref name="boxStyle"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any of the styles in <paramref name="boxStyle"/> is null.</exception>
    public static BoxBounds CreateFromOuterBounds(
        RectangleInfo outerBounds,
        BoxStyle boxStyle)
    {
        ArgumentNullException.ThrowIfNull(outerBounds);
        ArgumentNullException.ThrowIfNull(boxStyle);
        if (outerBounds == null || boxStyle.MarginStyle == null || boxStyle.BorderStyle == null || boxStyle.PaddingStyle == null)
        {
            throw new ArgumentException(null, nameof(boxStyle));
        }

        var marginBounds = outerBounds;
        var borderBounds = marginBounds.Shrink(boxStyle.MarginStyle);
        var paddingBounds = borderBounds.Shrink(boxStyle.BorderStyle);
        var contentBounds = paddingBounds.Shrink(boxStyle.PaddingStyle);
        return new(
            outerBounds, marginBounds, borderBounds, paddingBounds, contentBounds);
    }
}
