// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Models.ViewModel;

public sealed class DeviceViewModel
{
    public sealed class Builder
    {
        public Builder()
        {
            this.DeviceBounds = BoxBounds.Empty;
            this.DeviceStyle = BoxStyle.Empty;
            this.ScreenLayouts = new();
        }

        public DeviceInfo? DeviceInfo
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the layout bounds for the device.
        /// Coordinates are relative to the origin on the containing Form.
        /// </summary>
        public BoxBounds DeviceBounds
        {
            get;
            set;
        }

        public BoxStyle DeviceStyle
        {
            get;
            set;
        }

        public List<ScreenViewModel.Builder>? ScreenLayouts
        {
            get;
            set;
        }

        public DeviceViewModel Build()
        {
            return new DeviceViewModel(
                deviceInfo: this.DeviceInfo ?? throw new InvalidOperationException($"{nameof(this.DeviceInfo)} must be initialized before calling {nameof(this.Build)}."),
                deviceBounds: this.DeviceBounds ?? throw new InvalidOperationException($"{nameof(this.DeviceBounds)} must be initialized before calling {nameof(this.Build)}."),
                deviceStyle: this.DeviceStyle ?? throw new InvalidOperationException($"{nameof(this.DeviceStyle)} must be initialized before calling {nameof(this.Build)}."),
                screenLayouts: (this.ScreenLayouts ?? throw new InvalidOperationException($"{nameof(this.ScreenLayouts)} must be initialized before calling {nameof(this.Build)}."))
                    .Select(builder => builder.Build()));
        }
    }

    public DeviceViewModel(
        DeviceInfo deviceInfo,
        BoxBounds deviceBounds,
        BoxStyle deviceStyle,
        IEnumerable<ScreenViewModel> screenLayouts)
    {
        this.DeviceInfo = deviceInfo ?? throw new ArgumentNullException(nameof(deviceInfo));
        this.DeviceBounds = deviceBounds ?? throw new ArgumentNullException(nameof(deviceBounds));
        this.DeviceStyle = deviceStyle ?? throw new ArgumentNullException(nameof(deviceStyle));
        this.ScreenLayouts = new(
            (screenLayouts ?? throw new ArgumentNullException(nameof(screenLayouts)))
                .ToList());
    }

    public DeviceInfo DeviceInfo
    {
        get;
    }

    /// <summary>
    /// Gets the layout bounds for the device.
    /// Coordinates are relative to the origin on the containing Form.
    /// </summary>
    public BoxBounds DeviceBounds
    {
        get;
    }

    public BoxStyle DeviceStyle
    {
        get;
    }

    public ReadOnlyCollection<ScreenViewModel> ScreenLayouts
    {
        get;
    }
}
