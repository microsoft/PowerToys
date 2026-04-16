// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Xaml;

namespace PowerOCR.Helpers;

/// <summary>
/// Functions to constrain the mouse cursor (typically used when dragging)
/// </summary>
public static class CursorClipper
{
    /// <summary>
    /// Constrain mouse cursor to the area of the specified Window.
    /// </summary>
    /// <param name="window">Target WinUI 3 Window.</param>
    /// <returns>True on success.</returns>
    public static bool ClipCursor(Window window)
    {
        var appWindow = window.AppWindow;
        if (appWindow == null)
        {
            return false;
        }

        var position = appWindow.Position;
        var size = appWindow.Size;

        OSInterop.RECT rect = new OSInterop.RECT
        {
            Left = position.X,
            Top = position.Y,
            Right = position.X + size.Width,
            Bottom = position.Y + size.Height,
        };

        return OSInterop.ClipCursor(ref rect);
    }

    /// <summary>
    /// Remove any mouse cursor constraint.
    /// </summary>
    /// <returns>True on success.</returns>
    public static bool UnClipCursor()
    {
        return OSInterop.ClipCursor(IntPtr.Zero);
    }
}
