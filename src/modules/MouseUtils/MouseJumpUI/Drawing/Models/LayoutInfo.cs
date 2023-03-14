// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MouseJumpUI.Drawing.Models;

public sealed class LayoutInfo
{
    public sealed class Builder
    {
        public Builder()
        {
            this.ScreenBounds = new();
        }

        public LayoutConfig? LayoutConfig
        {
            get;
            set;
        }

        public RectangleInfo? FormBounds
        {
            get;
            set;
        }

        public RectangleInfo? PreviewBounds
        {
            get;
            set;
        }

        public List<RectangleInfo> ScreenBounds
        {
            get;
            set;
        }

        public RectangleInfo? ActivatedScreen
        {
            get;
            set;
        }

        public LayoutInfo Build()
        {
            return new LayoutInfo(
                layoutConfig: this.LayoutConfig ?? throw new InvalidOperationException(),
                formBounds: this.FormBounds ?? throw new InvalidOperationException(),
                previewBounds: this.PreviewBounds ?? throw new InvalidOperationException(),
                screenBounds: this.ScreenBounds ?? throw new InvalidOperationException(),
                activatedScreen: this.ActivatedScreen ?? throw new InvalidOperationException());
        }
    }

    public LayoutInfo(
        LayoutConfig layoutConfig,
        RectangleInfo formBounds,
        RectangleInfo previewBounds,
        IEnumerable<RectangleInfo> screenBounds,
        RectangleInfo activatedScreen)
    {
        this.LayoutConfig = layoutConfig ?? throw new ArgumentNullException(nameof(layoutConfig));
        this.FormBounds = formBounds ?? throw new ArgumentNullException(nameof(formBounds));
        this.PreviewBounds = previewBounds ?? throw new ArgumentNullException(nameof(previewBounds));
        this.ScreenBounds = new(
            (screenBounds ?? throw new ArgumentNullException(nameof(screenBounds)))
                .ToList());
        this.ActivatedScreen = activatedScreen ?? throw new ArgumentNullException(nameof(activatedScreen));
    }

    /// <summary>
    /// Gets the original LayoutConfig settings used to calculate coordinates.
    /// </summary>
    public LayoutConfig LayoutConfig
    {
        get;
    }

    /// <summary>
    /// Gets the size and location of the preview form.
    /// </summary>
    public RectangleInfo FormBounds
    {
        get;
    }

    /// <summary>
    /// Gets the size and location of the preview image.
    /// </summary>
    public RectangleInfo PreviewBounds
    {
        get;
    }

    public ReadOnlyCollection<RectangleInfo> ScreenBounds
    {
        get;
    }

    public RectangleInfo ActivatedScreen
    {
        get;
    }
}
