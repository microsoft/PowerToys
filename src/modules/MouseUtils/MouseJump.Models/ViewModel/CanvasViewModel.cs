// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Models.ViewModel;

public sealed class CanvasViewModel
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
        /// Coordinates are relative to the origin on the containing Form.
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

        public List<DeviceViewModel.Builder>? DeviceLayouts
        {
            get;
            set;
        }

        public CanvasViewModel Build()
        {
            return new CanvasViewModel(
                canvasBounds: this.CanvasBounds ?? throw new InvalidOperationException($"{nameof(this.CanvasBounds)} must be initialized before calling {nameof(this.Build)}."),
                canvasStyle: this.CanvasStyle ?? throw new InvalidOperationException($"{nameof(this.CanvasStyle)} must be initialized before calling {nameof(this.Build)}."),
                deviceLayouts: (this.DeviceLayouts ?? throw new InvalidOperationException($"{nameof(this.DeviceLayouts)} must be initialized before calling {nameof(this.Build)}."))
                    .Select(builder => builder.Build()));
        }
    }

    public CanvasViewModel(
        BoxBounds canvasBounds,
        BoxStyle canvasStyle,
        IEnumerable<DeviceViewModel> deviceLayouts)
    {
        this.CanvasBounds = canvasBounds ?? throw new ArgumentNullException(nameof(canvasBounds));
        this.CanvasStyle = canvasStyle ?? throw new ArgumentNullException(nameof(canvasStyle));
        this.DeviceLayouts = new(
            (deviceLayouts ?? throw new ArgumentNullException(nameof(deviceLayouts)))
                .ToList());
    }

    /// <summary>
    /// Gets the layout bounds for the canvas.
    /// Coordinates are relative to the origin on the containing Form.
    /// </summary>
    public BoxBounds CanvasBounds
    {
        get;
    }

    public BoxStyle CanvasStyle
    {
        get;
    }

    public ReadOnlyCollection<DeviceViewModel> DeviceLayouts
    {
        get;
    }
}
