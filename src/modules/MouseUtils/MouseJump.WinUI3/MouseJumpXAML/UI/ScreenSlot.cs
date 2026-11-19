// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using MouseJump.Models.Layout;

using Image = Microsoft.UI.Xaml.Controls.Image;

namespace MouseJump.WinUI3.UI;

/// <summary>
/// The UI elements <see cref="PreviewPane"/> maintains for one screen within the current layout.
/// </summary>
internal sealed class ScreenSlot
{
    public ScreenSlot(
        ScreenLayout screenLayout, Image bezelImage, Microsoft.UI.Xaml.Shapes.Rectangle? placeholderRectangle, Image contentImage)
    {
        this.ScreenLayout = screenLayout;
        this.BezelImage = bezelImage;
        this.PlaceholderRectangle = placeholderRectangle;
        this.ContentImage = contentImage;
    }

    public ScreenLayout ScreenLayout
    {
        get;
    }

    public Image BezelImage
    {
        get;
    }

    public Microsoft.UI.Xaml.Shapes.Rectangle? PlaceholderRectangle
    {
        get;
    }

    public Image ContentImage
    {
        get;
    }
}
