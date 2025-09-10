// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Labs.WinUI.MarkdownTextBlock;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.Helpers;

/// <summary>
/// Provides image loading functionality for local resources.
/// </summary>
public class ImageProvider : IImageProvider
{
    private static readonly HashSet<string> SupportedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "ms-appdata", "ms-appx",
    };

    private static readonly HashSet<string> SvgExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".svg",
    };

    public Task<Image> GetImage(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Task.FromResult<Image>(null!);
        }

        try
        {
            var uri = new Uri(url);
            var ext = Path.GetExtension(uri.AbsolutePath);

            Image image;
            if (SvgExtensions.Contains(ext))
            {
                image = new Image { Source = new SvgImageSource(uri), Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            }
            else
            {
                image = new Image { Source = new BitmapImage(uri), Stretch = Stretch.None, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            }

            return Task.FromResult<Image>(image);
        }
        catch
        {
            return Task.FromResult<Image>(null!);
        }
    }

    public bool ShouldUseThisProvider(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        try
        {
            var uri = new Uri(url);
            return SupportedSchemes.Contains(uri.Scheme);
        }
        catch
        {
            return false;
        }
    }
}
