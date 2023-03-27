// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace MouseJumpUI.Drawing.Models;

/// <summary>
/// Represents a collection of values needed for calculating the MainForm layout.
/// </summary>
public sealed class LayoutConfig
{
    public LayoutConfig(
        Rectangle virtualScreen,
        IEnumerable<Rectangle> screenBounds,
        Point activatedLocation,
        int activatedScreen,
        Size maximumFormSize,
        Padding formPadding,
        Padding previewPadding)
    {
        // make sure the virtual screen entirely contains all of the individual screen bounds
        ArgumentNullException.ThrowIfNull(screenBounds);
        if (screenBounds.Any(screen => !virtualScreen.Contains(screen)))
        {
            throw new ArgumentException($"'{nameof(virtualScreen)}' must contain all of the screens in '{nameof(screenBounds)}'", nameof(virtualScreen));
        }

        this.VirtualScreen = new RectangleInfo(virtualScreen);
        this.ScreenBounds = new(
            screenBounds.Select(screen => new RectangleInfo(screen)).ToList());
        this.ActivatedLocation = new(activatedLocation);
        this.ActivatedScreen = activatedScreen;
        this.MaximumFormSize = new(maximumFormSize);
        this.FormPadding = new(formPadding);
        this.PreviewPadding = new(previewPadding);
    }

    /// <summary>
    /// Gets the coordinates of the entire virtual screen.
    /// </summary>
    /// <remarks>
    /// The Virtual Screen is the bounding rectangle of all the monitors.
    /// https://learn.microsoft.com/en-us/windows/win32/gdi/the-virtual-screen
    /// </remarks>
    public RectangleInfo VirtualScreen
    {
        get;
    }

    /// <summary>
    /// Gets the bounds of all of the screens connected to the system.
    /// </summary>
    public ReadOnlyCollection<RectangleInfo> ScreenBounds
    {
        get;
    }

    /// <summary>
    /// Gets the point where the cursor was located when the form was activated.
    /// </summary>
    /// <summary>
    /// The preview form will be centered on this location unless there are any
    /// constraints such as the being too close to edge of a screen, in which case
    /// the form will be displayed as close as possible to this location.
    /// </summary>
    public PointInfo ActivatedLocation
    {
        get;
    }

    /// <summary>
    /// Gets the index of the screen the cursor was on when the form was activated.
    /// </summary>
    public int ActivatedScreen
    {
        get;
    }

    /// <summary>
    /// Gets the maximum size of the screen preview form.
    /// </summary>
    public SizeInfo MaximumFormSize
    {
        get;
    }

    /// <summary>
    /// Gets the padding border around the screen preview form.
    /// </summary>
    public PaddingInfo FormPadding
    {
        get;
    }

    /// <summary>
    /// Gets the padding border inside the screen preview image.
    /// </summary>
    public PaddingInfo PreviewPadding
    {
        get;
    }
}
