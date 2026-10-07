// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace Microsoft.CmdPal.UI.Controls;

[ContentProperty(Name = nameof(PreviewContent))]
public sealed partial class ScreenPreview : UserControl
{
    private const int WallpaperPreviewDecodePixelWidth = 1024;

    public static readonly DependencyProperty PreviewContentProperty =
        DependencyProperty.Register(nameof(PreviewContent), typeof(object), typeof(ScreenPreview), new PropertyMetadata(null!))!;

    public object PreviewContent
    {
        get => GetValue(PreviewContentProperty)!;
        set => SetValue(PreviewContentProperty, value);
    }

    public ImageSource? WallpaperImageSource => WallpaperImage.Source;

    public Brush? WallpaperBackground => ScreenBorder.Background;

    public ScreenPreview()
    {
        InitializeComponent();

        var wallpaperHelper = new WallpaperHelper();
        WallpaperImage!.Source = wallpaperHelper.GetWallpaperImage(WallpaperPreviewDecodePixelWidth)!;
        ScreenBorder!.Background = new SolidColorBrush(wallpaperHelper.GetWallpaperColor());
    }
}
