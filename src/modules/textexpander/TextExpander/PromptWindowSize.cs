// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.TextExpander;

/// <summary>
/// The prompt window's size math, apart from the window so it can be tested. The window's content
/// reaches into the title bar, so its XAML gets the whole client area, title bar included. Sizing
/// the client with Windows' own rules would add room for a system caption that isn't drawn, and that
/// room showed up as an empty band below the buttons.
/// </summary>
internal static class PromptWindowSize
{
    /// <summary>
    /// Gets the outer width or height, in pixels, that gives the window's content exactly
    /// <paramref name="contentDips"/>. <paramref name="windowPixels"/> and <paramref name="clientPixels"/>
    /// are the window's outer and client size now. Their difference is the frame, which is in pixels
    /// already, so only the content is scaled.
    /// </summary>
    public static int WindowPixels(int windowPixels, int clientPixels, double contentDips, double scale, int maxWindowPixels = int.MaxValue)
    {
        return Math.Min(windowPixels - clientPixels + ContentPixels(contentDips, scale), maxWindowPixels);
    }

    /// <summary>Converts DIPs to pixels at <paramref name="scale"/>, rounding up so the content fits.</summary>
    public static int ContentPixels(double dips, double scale)
    {
        // Layout rounding makes dips * scale whole; rounding first keeps floating-point noise
        // from adding a pixel.
        return (int)Math.Ceiling(Math.Round(dips * scale, 2));
    }
}
