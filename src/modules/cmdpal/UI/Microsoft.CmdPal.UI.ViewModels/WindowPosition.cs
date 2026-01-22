// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Graphics;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class WindowPosition
{
    /// <summary>
    /// Gets or sets left position in device pixels.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// Gets or sets top position in device pixels.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// Gets or sets width in device pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets height in device pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets width of the screen in device pixels where the window is located.
    /// </summary>
    public int ScreenWidth { get; set; }

    /// <summary>
    /// Gets or sets height of the screen in device pixels where the window is located.
    /// </summary>
    public int ScreenHeight { get; set; }

    /// <summary>
    /// Gets or sets DPI (dots per inch) of the display where the window is located.
    /// </summary>
    public int Dpi { get; set; }

    /// <summary>
    /// Converts the window position properties to a <see cref="RectInt32"/> structure representing the physical window rectangle.
    /// </summary>
    public RectInt32 ToPhysicalWindowRectangle()
    {
        return new RectInt32(X, Y, Width, Height);
    }
}
