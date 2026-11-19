// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Models.Layout;

/// <summary>
/// The layout of the canvas that <see cref="PreviewLayout"/> displays - the background fill and
/// the device/screen grid. Deliberately excludes the outer border - that's the hosting
/// window's responsibility, not the preview's (see <see cref="PreviewLayout"/> remarks).
/// </summary>
public sealed class CanvasLayout
{
    public sealed class Builder
    {
        public Builder()
        {
            this.DeviceLayouts = new();
            this.CanvasStyle = BoxStyle.Empty;
        }

        /// <summary>
        /// Gets or sets the layout bounds for the canvas.
        /// Coordinates are relative to the origin on the containing Preview.
        /// </summary>
        public BoxBounds? CanvasBounds
        {
            get;
            set;
        }

        public BoxStyle CanvasStyle
        {
            get;
            set;
        }

        public List<DeviceLayout.Builder>? DeviceLayouts
        {
            get;
#pragma warning disable CA2227 // Collection properties should be read only
            // allow writing to collection property in CanvasLayout.Builder
            // (it's a builder designed for convenience, and converted into
            // an immutable CanvasLayout instance once initialised)
            set;
#pragma warning restore CA2227
        }

        public CanvasLayout Build()
        {
            return new CanvasLayout(
                canvasBounds: this.CanvasBounds ?? throw new InvalidOperationException($"{nameof(this.CanvasBounds)} must be initialized before calling {nameof(this.Build)}."),
                canvasStyle: this.CanvasStyle ?? throw new InvalidOperationException($"{nameof(this.CanvasStyle)} must be initialized before calling {nameof(this.Build)}."),
                deviceLayouts: (this.DeviceLayouts ?? throw new InvalidOperationException($"{nameof(this.DeviceLayouts)} must be initialized before calling {nameof(this.Build)}."))
                    .Select(builder => builder.Build()));
        }
    }

    public CanvasLayout(
        BoxBounds canvasBounds,
        BoxStyle canvasStyle,
        IEnumerable<DeviceLayout> deviceLayouts)
    {
        this.CanvasBounds = canvasBounds ?? throw new ArgumentNullException(nameof(canvasBounds));
        this.CanvasStyle = canvasStyle ?? throw new ArgumentNullException(nameof(canvasStyle));
        this.DeviceLayouts = new(
            (deviceLayouts ?? throw new ArgumentNullException(nameof(deviceLayouts)))
                .ToList());
    }

    /// <summary>
    /// Gets the layout bounds for the canvas.
    /// Coordinates are relative to the origin on the containing Preview.
    /// </summary>
    public BoxBounds CanvasBounds
    {
        get;
    }

    public BoxStyle CanvasStyle
    {
        get;
    }

    public ReadOnlyCollection<DeviceLayout> DeviceLayouts
    {
        get;
    }
}
