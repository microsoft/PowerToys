// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Drawing;

namespace MouseJump.Models.ViewModel;

/// <summary>
/// Defines the preview form size and location.
/// </summary>
public sealed class FormViewModel
{
    public sealed class Builder
    {
        /// <summary>
        /// Gets or sets the coordinates of the preview form on the host computer's desktop.
        /// Coordinates are relative to the actual host desktop.
        /// </summary>
        public RectangleInfo? FormBounds
        {
            get;
            set;
        }

        public CanvasViewModel.Builder? CanvasLayout
        {
            get;
            set;
        }

        public FormViewModel Build()
        {
            return new FormViewModel(
                formBounds: this.FormBounds ?? throw new InvalidOperationException($"{nameof(this.FormBounds)} must be initialized before calling {nameof(this.Build)}."),
                canvasLayout: (this.CanvasLayout ?? throw new InvalidOperationException($"{nameof(this.CanvasLayout)} must be initialized before calling {nameof(this.Build)}."))
                    .Build());
        }
    }

    public FormViewModel(
        RectangleInfo formBounds,
        CanvasViewModel canvasLayout)
    {
        this.FormBounds = formBounds ?? throw new ArgumentNullException(nameof(formBounds));
        this.CanvasLayout = canvasLayout ?? throw new ArgumentNullException(nameof(canvasLayout));
    }

    /// <summary>
    /// Gets the coordinates of the preview form on the host computer's desktop.
    /// Coordinates are relative to the actual host virtual screen / desktop that the preview image will be shown on.
    /// </summary>
    public RectangleInfo FormBounds
    {
        get;
    }

    public CanvasViewModel CanvasLayout
    {
        get;
    }
}
