// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.Models.Styles;

namespace MouseJumpUI.Common.Models.Layout;

public sealed class PreviewLayout
{
    public sealed class Builder
    {
        public Builder()
        {
            this.Screens = new();
            this.ScreenshotBounds = new();
        }

        public PreviewStyle? PreviewStyle
        {
            get;
            set;
        }

        public RectangleInfo? VirtualScreen
        {
            get;
            set;
        }

        public List<RectangleInfo> Screens
        {
            get;
            set;
        }

        public int ActivatedScreenIndex
        {
            get;
            set;
        }

        public RectangleInfo? FormBounds
        {
            get;
            set;
        }

        public BoxBounds? PreviewBounds
        {
            get;
            set;
        }

        public List<BoxBounds> ScreenshotBounds
        {
            get;
            set;
        }

        public PreviewLayout Build()
        {
            return new PreviewLayout(
                previewStyle: this.PreviewStyle ?? throw new InvalidOperationException($"{nameof(this.PreviewStyle)} must be initialized before calling {nameof(this.Build)}."),
                virtualScreen: this.VirtualScreen ?? throw new InvalidOperationException($"{nameof(this.VirtualScreen)} must be initialized before calling {nameof(this.Build)}."),
                screens: this.Screens ?? throw new InvalidOperationException($"{nameof(this.Screens)} must be initialized before calling {nameof(this.Build)}."),
                activatedScreenIndex: this.ActivatedScreenIndex,
                formBounds: this.FormBounds ?? throw new InvalidOperationException($"{nameof(this.FormBounds)} must be initialized before calling {nameof(this.Build)}."),
                previewBounds: this.PreviewBounds ?? throw new InvalidOperationException($"{nameof(this.PreviewBounds)} must be initialized before calling {nameof(this.Build)}."),
                screenshotBounds: this.ScreenshotBounds ?? throw new InvalidOperationException($"{nameof(this.ScreenshotBounds)} must be initialized before calling {nameof(this.Build)}."));
        }
    }

    public PreviewLayout(
        PreviewStyle previewStyle,
        RectangleInfo virtualScreen,
        List<RectangleInfo> screens,
        int activatedScreenIndex,
        RectangleInfo formBounds,
        BoxBounds previewBounds,
        List<BoxBounds> screenshotBounds)
    {
        this.PreviewStyle = previewStyle ?? throw new ArgumentNullException(nameof(previewStyle));
        this.VirtualScreen = virtualScreen ?? throw new ArgumentNullException(nameof(virtualScreen));
        this.Screens = (screens ?? throw new ArgumentNullException(nameof(screens)))
            .ToList().AsReadOnly();
        this.ActivatedScreenIndex = activatedScreenIndex;
        this.FormBounds = formBounds ?? throw new ArgumentNullException(nameof(formBounds));
        this.PreviewBounds = previewBounds ?? throw new ArgumentNullException(nameof(previewBounds));
        this.ScreenshotBounds = (screenshotBounds ?? throw new ArgumentNullException(nameof(screenshotBounds)))
            .ToList().AsReadOnly();
    }

    public PreviewStyle PreviewStyle
    {
        get;
    }

    public RectangleInfo VirtualScreen
    {
        get;
    }

    public ReadOnlyCollection<RectangleInfo> Screens
    {
        get;
    }

    public int ActivatedScreenIndex
    {
        get;
    }

    public RectangleInfo FormBounds
    {
        get;
    }

    public BoxBounds PreviewBounds
    {
        get;
    }

    public ReadOnlyCollection<BoxBounds> ScreenshotBounds
    {
        get;
    }
}
