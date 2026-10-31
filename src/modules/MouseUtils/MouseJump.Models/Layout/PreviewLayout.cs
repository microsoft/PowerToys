// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Drawing;

namespace MouseJump.Models.Layout;

/// <summary>
/// Defines the size of the preview pane's own content - the background/canvas grid only, not
/// the outer border and not a position on the desktop. Deliberately excludes the border:
/// that's a hosting-window concern (drawn by whatever hosts the preview pane, wrapping around
/// <see cref="PreviewSize"/> with its own margin/border box - see
/// <c>LayoutHelper.GetPreviewWindowStyle</c>), not something the preview pane's own layout needs to
/// know about. Deliberately excludes desktop position too - every bounds in
/// <see cref="CanvasLayout"/> (and everything nested under it) is relative to this content's
/// own top-left corner as the origin; deciding where that origin actually lands on the
/// desktop - centering on the activated location, clamping to the activated screen,
/// accounting for the host's own margin/border - is entirely the hosting window's job (see
/// <c>LayoutHelper.PositionOnScreen</c>).
/// </summary>
public sealed class PreviewLayout
{
    public sealed class Builder
    {
        /// <summary>
        /// Gets or sets the size of the preview pane's own content.
        /// </summary>
        public SizeInfo? PreviewSize
        {
            get;
            set;
        }

        public CanvasLayout.Builder? CanvasLayout
        {
            get;
            set;
        }

        public PreviewLayout Build()
        {
            return new PreviewLayout(
                previewSize: this.PreviewSize ?? throw new InvalidOperationException($"{nameof(this.PreviewSize)} must be initialized before calling {nameof(this.Build)}."),
                canvasLayout: (this.CanvasLayout ?? throw new InvalidOperationException($"{nameof(this.CanvasLayout)} must be initialized before calling {nameof(this.Build)}."))
                    .Build());
        }
    }

    public PreviewLayout(
        SizeInfo previewSize,
        CanvasLayout canvasLayout)
    {
        this.PreviewSize = previewSize ?? throw new ArgumentNullException(nameof(previewSize));
        this.CanvasLayout = canvasLayout ?? throw new ArgumentNullException(nameof(canvasLayout));
    }

    /// <summary>
    /// Gets the size of the preview pane's own content.
    /// </summary>
    public SizeInfo PreviewSize
    {
        get;
    }

    public CanvasLayout CanvasLayout
    {
        get;
    }
}
