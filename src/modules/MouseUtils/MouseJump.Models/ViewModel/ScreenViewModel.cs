// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Display;
using MouseJump.Models.Drawing;
using MouseJump.Models.Styles;

namespace MouseJump.Models.ViewModel;

public sealed class ScreenViewModel
{
    public sealed class Builder
    {
        public Builder()
        {
            this.ScreenBounds = BoxBounds.Empty;
            this.ScreenStyle = BoxStyle.Empty;
        }

        public ScreenInfo? ScreenInfo
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the layout bounds for the screen.
        /// Coordinates are relative to the origin on the containing Form.
        /// </summary>
        public BoxBounds ScreenBounds
        {
            get;
            set;
        }

        public BoxStyle ScreenStyle
        {
            get;
            set;
        }

        public ScreenViewModel Build()
        {
            return new ScreenViewModel(
                screenInfo: this.ScreenInfo ?? throw new InvalidOperationException($"{nameof(this.ScreenInfo)} must be initialized before calling {nameof(this.Build)}."),
                screenBounds: this.ScreenBounds ?? throw new InvalidOperationException($"{nameof(this.ScreenBounds)} must be initialized before calling {nameof(this.Build)}."),
                screenStyle: this.ScreenStyle ?? throw new InvalidOperationException($"{nameof(this.ScreenStyle)} must be initialized before calling {nameof(this.Build)}."));
        }
    }

    public ScreenViewModel(
        ScreenInfo screenInfo,
        BoxBounds screenBounds,
        BoxStyle screenStyle)
    {
        this.ScreenInfo = screenInfo ?? throw new ArgumentNullException(nameof(screenInfo));
        this.ScreenBounds = screenBounds ?? throw new ArgumentNullException(nameof(screenBounds));
        this.ScreenStyle = screenStyle ?? throw new ArgumentNullException(nameof(screenStyle));
    }

    public ScreenInfo ScreenInfo
    {
        get;
    }

    /// <summary>
    /// Gets the layout bounds for the screen.
    /// Coordinates are relative to the origin on the containing Form.
    /// </summary>
    public BoxBounds ScreenBounds
    {
        get;
    }

    public BoxStyle ScreenStyle
    {
        get;
    }

    public override string ToString()
    {
        return $"bounds: {this.ScreenBounds}, style: {this.ScreenStyle}";
    }
}
