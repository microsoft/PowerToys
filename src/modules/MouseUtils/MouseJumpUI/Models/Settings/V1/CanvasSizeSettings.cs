// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJumpUI.Models.Settings.V1;

public sealed class CanvasSizeSettings
{
    public CanvasSizeSettings(
        int width,
        int height)
    {
        this.Width = width;
        this.Height = height;
    }

    public int Width
    {
        get;
    }

    public int Height
    {
        get;
    }
}
