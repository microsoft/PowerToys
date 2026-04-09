// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Microsoft.CmdPal.UI.ViewModels.Gallery;

public sealed class ExtensionGalleryScreenshotViewModel
{
    public ExtensionGalleryScreenshotViewModel(Uri uri, int index)
    {
        ArgumentNullException.ThrowIfNull(uri);

        Uri = uri;
        Index = index;
        DisplayName = $"Screenshot {index + 1}";
    }

    public Uri Uri { get; }

    public int Index { get; }

    public string DisplayName { get; }

    public ImageSource ImageSource => field ??= CreateImageSource(Uri);

    private static ImageSource CreateImageSource(Uri uri)
    {
        BitmapImage bitmap = new();
        bitmap.DecodePixelWidth = 720;
        bitmap.UriSource = uri;
        return bitmap;
    }
}
